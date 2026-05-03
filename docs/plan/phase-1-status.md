# Phase 1 完成记录

## 状态

Phase 1：共享约定已完成。

## 已完成内容

- 在 `packages/shared` 中定义后端共享 Web 约定：
  - JWT 配置结构。
  - 鉴权 Cookie 名称 `SEKAI_PLATFORM_AUTH`。
  - 内部上下文 Header。
  - 统一错误响应 `{ msg, trace_id }`。
  - 当前请求上下文读取器。
- 4 个 Web API 服务接入统一默认配置：
  - API Service。
  - Auth Service。
  - Asset Service。
  - Search Service。
- JWT Bearer 支持两种 token 传递方式：
  - `Authorization: Bearer <token>`。
  - `SEKAI_PLATFORM_AUTH` Cookie。
- 所有 Web API 服务保留 `/health`。
- API Service 新增内部服务健康聚合接口：
  - `GET /api/internal-services/health`
- 服务间 HTTP 调用约定：
  - 使用 named `HttpClient`。
  - 自动透传 `X-Sekai-Trace-Id`、`X-Sekai-User-Id`、`X-Sekai-Tenant-Id`。
  - 依赖 .NET Activity 传播标准 W3C `traceparent`。
- 日志和追踪约定已落到 `docs/design/index.md`。
- API 文档由 Apifox 项目 `8210187` 维护，本阶段不新增本地 OpenAPI 文件。

## 验证结果

已验证通过：

- `dotnet build SekaiPlatform.sln`
- `docker compose config`
- `docker compose up --build -d`
- `GET http://[::1]:8080/health` 返回 `Healthy`，响应包含 `X-Sekai-Trace-Id`
- `GET http://[::1]:8080/api/internal-services/health` 返回 Auth、Asset、Search 均为 `healthy`
- `API_BASE_URL='http://[::1]:8080' bash scripts/phase1-smoke.sh`
- API Service 日志输出 `TraceId`、`SpanId` 和 scope 字段 `trace_id`、`user_id`、`tenant_id`

本机验证时发现 IPv4 `localhost:8080` 被本机 nginx 占用，使用 IPv6 loopback `http://[::1]:8080` 可访问 Docker 暴露的 API Service。

## 后续衔接

Phase 2 可在当前基础上继续：

- 引入 EF Core 和 PostgreSQL schema。
- 建立租户、用户、剧情、原文、译文等数据模型。
- 后续业务接口可复用当前请求上下文读取器获取当前用户和租户。

Phase 3 实现登录和租户选择时需要补齐：

- JWT 签发逻辑。
- 登录成功后写入 `SEKAI_PLATFORM_AUTH` Cookie。
- 用户 ID 和当前租户 ID 的 claims。

## 本地注意事项

- 5 个 .NET 服务在 Docker Compose 中固定 `platform: linux/amd64`，用于规避当前本机 Docker Desktop 下 .NET 10 ARM64 SDK 容器在 restore/publish 阶段偶发 `Illegal instruction` 的问题。
- Dockerfile 不设置 .NET ARM64 intrinsic 兼容环境变量，避免影响其他部署环境的运行性能和行为。
