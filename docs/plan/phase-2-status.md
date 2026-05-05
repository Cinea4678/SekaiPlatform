# Phase 2 完成记录

## 状态

Phase 2：数据库已完成。

## 已完成内容

- 新增 `database/SekaiPlatform.Database.csproj` 共享数据库项目。
- 引入 EF Core 和 Npgsql PostgreSQL Provider。
- 建立 `SekaiPlatformDbContext`、实体和 Fluent API 映射。
- 使用 EF Core migration 管理 PostgreSQL schema。
- 创建以下表：
  - `tenants`
  - `users`
  - `user_tenants`
  - `user_oauthes`
  - `story_groups`
  - `stories`
  - `story_source_lines`
  - `translation_versions`
  - `translation_lines`
  - `sync_jobs`
- API Service、Auth Service、Asset Service、Sync Worker 已接入 DbContext。
- API Service 在 Docker Compose 的 Development 环境中默认自动执行 migration 和 seed。
- seed 后会创建默认租户和默认超级管理员用户。
- 默认 seed 用户密码为 `121748`；本地可通过 `SEED_ADMIN_PASSWORD` 覆盖初始密码。

## Seed 数据

默认租户：

- `PJS 字幕组`

默认用户：

| QQ 号 | 角色 | 说明 |
|---|---|---|
| `1650121748` | `super_admin` | 本地超级管理员用户 |

## 验证结果

已验证通过：

- `dotnet build SekaiPlatform.sln`
- `dotnet ef migrations add InitialCreate --project database/SekaiPlatform.Database.csproj --output-dir migrations`
- `POSTGRES_PASSWORD=sekai_platform dotnet dotnet-ef migrations add ReviewPhase2Constraints --project database/SekaiPlatform.Database.csproj --output-dir migrations`
- `docker compose config`
- `docker compose up --build -d`
- `GET http://[::1]:8080/health` 返回 `Healthy`
- `GET http://[::1]:8080/api/internal-services/health` 返回 Auth、Asset、Search 均为 `healthy`
- PostgreSQL 已创建 Phase 2 表和 `__EFMigrationsHistory`
- PostgreSQL 已应用 `ReviewPhase2Constraints` 迁移，包含枚举 CHECK、租户成员创建者约束、翻译行剧情一致性约束
- seed 后存在 1 个默认租户、1 个默认超级管理员用户、1 条用户租户关系
- 默认 seed 用户 `password_hash` 为空，未写入公开固定密码
- API Service 重启后不会覆盖已有用户租户关系状态，确认 seed 幂等且不重置权限

## 后续衔接

Phase 3 可在当前基础上继续：

- 使用 `users.qq_id` 和 `users.password_hash` 实现用户名密码登录。
- 登录成功后根据 `user_tenants.status == active` 加载可用租户。
- 用户只有一个可用租户时直接写入当前租户 claim。
- 用户有多个可用租户时进入租户选择流程。
