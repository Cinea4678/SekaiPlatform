# 安全模型

## 定位

平台安全模型分为三个边界：

- 外部身份边界：前端只访问 API Service，使用外部用户 token 表示登录用户。
- 合作伙伴开放 API 边界：外部合作伙伴只访问 OpenApiService，当前允许匿名读取公开翻译。
- 内部调用边界：API Service、Auth Service、Asset Service、Search Service、Sync Worker 之间只使用内部 token。

内部 token 使用非对称签名。被调用服务只持有验证公钥；需要主动发起内部调用的服务持有自身服务身份对应的签发私钥，或从受信 token service 换取短期 token。

## 安全假设

- API Service 是前端业务 API 入口。
- OpenApiService 是合作伙伴开放 API 入口，部署在独立端口或独立 Ingress。
- Auth Service、Asset Service、Search Service 和 Sync Worker 不直接暴露给公网、宿主机公开端口或外部 Ingress。
- Docker Compose 内部网络是受控部署边界，但不是强安全边界。
- 生产环境 secret 必须替换本地默认值，并由部署环境注入。

## Token 类型

### 外部用户 token

外部用户 token 只允许出现在前端到 API Service 的链路：

- Cookie：`SEKAI_PLATFORM_AUTH`
- Header：`Authorization: Bearer <token>`

API Service 验证外部用户 token 后，将用户上下文转换为内部 token。内部服务不得把外部用户 token 作为服务间凭证。

### 内部 token

内部 token 表示一次服务间调用，必须是短期 token。

必要 claim：

| Claim | 说明 |
|---|---|
| `iss` | 签发方 |
| `aud` | 目标服务 |
| `exp` | 过期时间，默认不超过 5 分钟 |
| `iat` | 签发时间 |
| `jti` | token 唯一标识 |
| `actor` | 发起调用的服务身份 |
| `scope` | 本次调用允许执行的能力 |
| `subject_user_id` | 用户代理调用中的用户 ID |
| `tenant_id` | 用户代理调用中的当前租户 ID |

目标服务必须校验 `iss`、`aud`、签名、有效期、`actor` 和所需 `scope`。涉及租户资源的 endpoint 继续校验成员关系、角色、资源归属和业务不变量。

### 开放 API 匿名访问

开放 API 当前允许匿名访问。OpenApiService 不接受前端 Cookie、外部用户 token 或 API Key 作为开放 API 凭证。

OpenApiService 校验监听端口、开放路径和限流规则。读取公开翻译时，OpenApiService 使用 `public.translation.read` scope 调用 Asset Service，不向内部服务传递外部用户或租户上下文。

## Endpoint 分类

| 类别 | 路径示例 | 凭证 |
|---|---|---|
| 外部 API | `/api/auth/session`、`/api/search` | 外部用户 token 或登录请求体 |
| 开放 API | `/api/public/translations/{scenario_id}`、`/api/public/translations/batch` | 匿名访问 |
| 用户代理内部接口 | `/internal/assets/stories`、`/internal/search` | 内部 token，携带用户和租户上下文 |
| 系统内部接口 | `/internal/search/index/rebuild` | 内部 token |
| 健康检查 | `/health` | 无应用层凭证 |

不得新增不属于以上类别的鉴权方式。

## Scope

内部 scope 使用 `<domain>.<resource>.<action>` 形式，按最小能力授权。

| Scope | 目标服务 | 说明 |
|---|---|---|
| `auth.login` | Auth Service | API Service 代理用户登录 |
| `auth.session.read` | Auth Service | 读取当前用户登录态 |
| `auth.tenants.read` | Auth Service | 读取当前用户可访问租户 |
| `auth.tenant.switch` | Auth Service | 切换当前租户 |
| `users.invitations.write` | Auth Service | 邀请用户 |
| `assets.read` | Asset Service | 读取剧情资产和当前租户译文 |
| `public.translation.read` | Asset Service | 读取公开发布的译文 |
| `sync.jobs.write` | Asset Service | 触发同步任务 |
| `sync.jobs.read` | Asset Service | 查询同步任务 |
| `translations.import.write` | Asset Service | 导入历史译文 |
| `search.query` | Search Service | 查询共享原文和当前租户译文 |
| `search.index.rebuild` | Search Service | 重建索引 |
| `search.translation.refresh` | Search Service | 刷新导入后的译文索引 |

新增内部 endpoint 时先定义 scope，再实现鉴权检查。

## 上下文 Header

`X-Sekai-Trace-Id`、`traceparent` 和相关追踪字段只用于日志、链路追踪和排障。

`X-Sekai-User-Id` 和 `X-Sekai-Tenant-Id` 不作为鉴权或授权输入。迁移兼容期如果仍保留这些 Header，只能作为日志兼容字段。业务权限必须来自已验证的 token claims 和服务本地数据查询。

API Service 必须剥离外部请求自带的 `X-Sekai-User-Id` 和 `X-Sekai-Tenant-Id`。

## 密钥约束

- 内部 token 必须使用非对称签名。
- 被调用服务只配置验证公钥。
- 签发私钥不得提交到仓库配置文件或镜像默认配置。
- `actor` 必须与签名密钥身份绑定。
- 目标服务必须按 `actor + aud + scope` 校验调用是否被允许。
- 外部用户 token 和内部 token 使用不同 issuer、audience、密钥材料和校验配置。
- 禁止用所有服务共享的 HMAC secret 实现内部 token。

## 调用链规则

前端调用业务 API：

```text
Frontend
  -> API Service: external user token
  -> Internal Service: internal token with actor, aud, scope, subject_user_id, tenant_id
```

合作伙伴调用开放 API：

```text
External Partner
  -> OpenApiService: anonymous request
  -> Asset Service: internal token with actor, aud, scope
```

服务执行系统维护：

```text
Asset Service / Sync Worker
  -> Search Service: internal token with actor, aud, scope
```

内部服务处理顺序：

1. 验证内部 token。
2. 校验 `aud` 指向当前服务。
3. 校验 endpoint 所需 `scope`。
4. 用户代理调用读取 `subject_user_id` 和 `tenant_id`。
5. 执行资源归属、成员关系、角色和业务规则校验。

## 禁止事项

- 禁止内部 endpoint 只依赖 `X-Sekai-User-Id` 或 `X-Sekai-Tenant-Id` 做权限判断。
- 禁止内部 endpoint 同时接受多套等价鉴权方式。
- 禁止内部 endpoint 接受开放 API 匿名请求。
- 禁止 OpenApiService 接受前端 Cookie、外部用户 token 或 API Key 作为开放 API 凭证。
- 禁止新增共享明文 secret 作为长期服务间身份凭证。
- 禁止业务服务持有其他服务身份的签发私钥。
- 禁止在日志中输出 token、私钥、maintenance token 或完整 Authorization Header。
- 禁止新增 `X-Sekai-Maintenance-Token` 鉴权接口。
