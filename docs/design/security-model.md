# PJS 字幕组语言资产平台 安全模型

## 定位

本文定义平台统一安全模型。后续涉及登录态、内部服务调用、维护接口、租户上下文和跨服务授权的设计与实现，都必须遵循本文约束。

平台安全模型分为两个边界：

- 外部身份边界：前端和外部调用方只访问 API Service，并使用外部用户 token 表示登录用户。
- 内部调用边界：API Service、Auth Service、Asset Service、Search Service 和 Sync Worker 之间只使用内部 token 表示调用方、调用目标、授权范围和可选用户上下文。

内部 token 使用非对称签名。被调用服务只依赖公钥验证 token；需要主动发起内部调用的服务必须使用与自身服务身份绑定的签发私钥，或从受信 token service 换取短期内部 token。

## 安全假设

- API Service 是唯一外部入口。
- Auth Service、Asset Service、Search Service 和 Sync Worker 不直接暴露给公网、宿主机公开端口或外部 Ingress。
- Docker Compose 内部网络是受控部署边界，但不是强安全边界。
- 当前模型不声称在容器被完全攻破、数据库连接串泄露、签发私钥泄露或运行时 secret 全量泄露后仍能保持数据安全。
- 所有生产环境 secret 必须替换本地默认值，并由部署环境的 secret 管理机制注入。

## Token 类型

### 外部用户 token

外部用户 token 表示前端用户登录状态，只允许出现在外部调用链路：

- Cookie：`SEKAI_PLATFORM_AUTH`
- Header：`Authorization: Bearer <token>`

外部用户 token 由 Auth Service 在登录或切换租户时签发。API Service 负责验证外部用户 token，并将验证后的用户上下文转换为内部 token。

内部服务不得把外部用户 token 作为服务间调用凭证。迁移完成后，Auth Service、Asset Service 和 Search Service 的 `/internal/...` endpoint 不接受外部用户 token。

### 内部 token

内部 token 表示一次服务间调用。内部 token 必须使用非对称签名，并且必须是短期 token。

内部 token 至少包含以下语义：

| Claim | 说明 |
|---|---|
| `iss` | 内部 token 签发方。 |
| `aud` | 目标服务，例如 `auth-service`、`asset-service`、`search-service`。 |
| `exp` | 过期时间，默认不超过 5 分钟。 |
| `iat` | 签发时间。 |
| `jti` | token 唯一标识，用于日志、审计和必要时的重放排查。 |
| `actor` | 发起调用的服务身份，例如 `api-service`、`asset-service`、`sync-worker`。 |
| `scope` | 本次调用允许执行的内部能力。 |
| `subject_user_id` | 可选。用户代理调用时表示被代理的用户 ID。 |
| `tenant_id` | 可选。用户代理调用时表示当前租户 ID。 |

内部服务必须校验 `iss`、`aud`、签名、有效期和所需 `scope`。涉及租户资源的 endpoint 还必须校验 `tenant_id`，并在本服务领域内继续检查成员关系、角色、资源归属和业务不变量。

## Endpoint 分类

所有 endpoint 必须归入以下类别之一。

| 类别 | 路径示例 | 凭证 | 说明 |
|---|---|---|---|
| 外部 API | `/api/auth/session`、`/api/sync/jobs` | 外部用户 token 或登录请求体 | 只由 API Service 对前端暴露。 |
| 用户代理内部接口 | `/internal/auth/session`、`/internal/sync/jobs` | 内部 token | 代表某个用户和租户执行业务操作。 |
| 系统内部接口 | `/internal/search/index/rebuild` | 内部 token | 由服务或 worker 执行维护任务，不绑定用户。 |
| 健康检查 | `/health` | 无应用层凭证 | 只返回健康状态，不返回敏感数据。部署层负责限制暴露范围。 |

不得新增不属于以上类别的鉴权方式。

## 内部 Scope 约定

内部 scope 使用 `<domain>.<resource>.<action>` 形式，按最小能力授权。

初始 scope 约定：

| Scope | 目标服务 | 说明 |
|---|---|---|
| `auth.login` | Auth Service | API Service 代理用户登录。 |
| `auth.session.read` | Auth Service | API Service 读取当前用户登录态。 |
| `auth.tenants.read` | Auth Service | API Service 读取当前用户可访问租户。 |
| `auth.tenant.switch` | Auth Service | API Service 代理用户切换当前租户。 |
| `users.invitations.write` | Auth Service | API Service 代理租户管理员邀请用户。 |
| `sync.jobs.write` | Asset Service | API Service 代理租户管理员触发同步。 |
| `sync.jobs.read` | Asset Service | API Service 代理租户管理员查询同步任务。 |
| `search.index.rebuild` | Search Service | Asset Service 或 Sync Worker 刷新搜索索引。 |

新增内部 endpoint 时必须先定义 scope，再实现鉴权检查。

## 上下文 Header 约束

`X-Sekai-Trace-Id`、`traceparent` 和相关追踪字段只用于日志、链路追踪和排障。

`X-Sekai-User-Id` 和 `X-Sekai-Tenant-Id` 不再作为鉴权或授权输入。迁移期内如果仍保留这些 Header，只能作为日志兼容字段；业务权限必须来自已验证的 token claims 和服务本地数据查询。

API Service 必须继续剥离外部请求自带的 `X-Sekai-User-Id` 和 `X-Sekai-Tenant-Id`，避免客户端伪造上下文进入日志或兼容路径。

## 维护 token 约束

`X-Sekai-Maintenance-Token` 是旧的轻量内部维护凭证。统一模型不再新增使用 maintenance token 的接口。

Search Service 索引重建接口使用内部 token，不再接受 `X-Sekai-Maintenance-Token`。

## 签发与密钥约束

- 内部 token 必须使用非对称签名。
- 被调用服务只需要验证公钥，不得为了验证请求而配置签发私钥。
- 只有需要主动发起内部调用的组件可以持有自身服务身份对应的签发私钥，或通过受信 token service 换取短期内部 token。
- 签发私钥不得提交到仓库配置文件或镜像默认配置中，必须由本地 `.env`、用户机密或生产 secret 管理机制注入。
- `actor` 必须与签名密钥身份绑定。服务不得使用一个私钥签发其他服务身份的内部 token。
- 目标服务必须按 `actor + aud + scope` 校验调用是否被允许。
- 单个服务泄露后，不应因为持有验证材料而获得伪造其他服务调用的能力。
- 外部用户 token 和内部 token 使用不同的 issuer、audience、密钥材料和校验配置。
- 禁止用所有服务共享的 HMAC secret 实现内部 token。

## 调用链规则

前端调用业务 API：

```text
Frontend
  -> API Service: external user token
  -> Internal Service: internal token with actor, aud, scope, subject_user_id, tenant_id
```

服务执行系统维护：

```text
Asset Service / Sync Worker
  -> Search Service: internal token with actor, aud, scope
```

内部服务收到请求后必须按以下顺序处理：

1. 验证内部 token。
2. 校验 `aud` 是否指向当前服务。
3. 校验 endpoint 所需 `scope`。
4. 对用户代理调用读取 `subject_user_id` 和 `tenant_id`。
5. 在本服务内执行资源归属、成员关系、角色和业务规则校验。

## 禁止事项

- 禁止内部 endpoint 只依赖 `X-Sekai-User-Id` 或 `X-Sekai-Tenant-Id` 做权限判断。
- 禁止内部 endpoint 同时接受多套等价鉴权方式。
- 禁止新增共享明文 secret 作为长期服务间身份凭证。
- 禁止业务服务持有内部 token 签发私钥，除非该服务被设计为签发方。
- 禁止在日志中输出 token、私钥、maintenance token 或完整 Authorization Header。

## 迁移约束

当前实现已经将内部 HTTP 调用迁移到内部 token。后续新增或调整内部调用按以下顺序检查：

1. 为 `/internal/...` endpoint 标注类别和 scope。
2. 调用方签发面向目标服务 audience 的内部 token。
3. 目标服务校验 `actor + aud + scope`。
4. 用户代理调用通过 `subject_user_id` 和 `tenant_id` 传递上下文。
5. 内部服务继续执行本领域的资源归属、成员关系、角色和业务规则校验。

迁移过程中不得新增第四种内部鉴权机制。
