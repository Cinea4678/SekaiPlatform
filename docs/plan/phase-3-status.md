# Phase 3 完成记录

## 状态

Phase 3：鉴权和租户已完成。

## 已完成内容

- Auth Service 实现内部鉴权接口：
  - 用户名密码登录。
  - 获取登录状态。
  - 查询可用租户。
  - 选择和切换当前租户。
  - 邀请用户加入当前租户。
- API Service 实现外部 Phase 3 API：
  - `POST /api/auth/login`
  - `POST /api/auth/logout`
  - `GET /api/auth/session`
  - `GET /api/auth/tenants`
  - `PUT /api/auth/current-tenant`
  - `POST /api/users/invitations`
- 登录成功后同时返回 `access_token` 并写入 `SEKAI_PLATFORM_AUTH` HttpOnly Cookie。
- JWT claims 使用 `user_id` 和可空 `tenant_id`。
- 用户只有一个 active 租户时自动选中该租户；多个 active 租户时登录后进入租户选择阶段。
- API Service 会剥离外部请求自带的 `X-Sekai-User-Id` 和 `X-Sekai-Tenant-Id`，不信任客户端传入的上下文 Header。
- API Service 和 Auth Service 对登录接口启用固定窗口限速，降低在线撞库风险。
- 非 Development 环境写入鉴权 Cookie 时默认强制 `Secure`。
- 新增共享授权策略：
  - `sekai.logged_in`：要求登录用户。
  - `sekai.tenant_selected`：要求登录且已选择当前租户。
- 用户邀请规则：
  - `admin` 和 `super_admin` 可以邀请普通用户。
  - 只有 `super_admin` 可以授予 `admin` 或 `super_admin`。
  - 新用户默认密码为 QQ 号后六位。
  - 重复邀请同一用户且角色相同时保持幂等，不创建重复成员关系。
  - 用户已属于当前租户但角色不同的，邀请接口返回冲突错误，不隐式修改角色。

## 安全说明

- 新用户默认密码为 QQ 号后六位是当前产品约束，安全性弱于随机临时密码或邀请 token；后续增加密码重置、首次登录改密或邀请 token 后应替换该规则。
- `sekai.tenant_selected` 当前验证 JWT 中存在当前租户 claim；租户级写接口仍需要在业务逻辑中二次校验 active 成员关系和角色。Phase 4 及之后新增业务接口必须延续该规则。

## 验证结果

已验证通过：

- `dotnet build apps/auth-service/SekaiPlatform.AuthService.csproj --no-restore -v minimal -nr:false /p:UseSharedCompilation=false`
- `dotnet build apps/api-service/SekaiPlatform.ApiService.csproj --no-restore -v minimal -nr:false /p:UseSharedCompilation=false`
- `dotnet build tests/integration-tests/SekaiPlatform.IntegrationTests.csproj --no-restore -v minimal -nr:false /p:UseSharedCompilation=false`
- `dotnet build SekaiPlatform.sln --no-restore -v minimal -nr:false /p:UseSharedCompilation=false`
- `dotnet test tests/integration-tests/SekaiPlatform.IntegrationTests.csproj --no-build -v minimal -nr:false`，11 个测试通过

## 后续衔接

Phase 4 及之后新增业务接口时，必须使用 `sekai.tenant_selected` 或等效租户策略，确保业务 API 只从登录状态获取当前租户，不信任客户端传入的租户 ID。
