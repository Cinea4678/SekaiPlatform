# Phase 0 完成记录

## 状态

Phase 0：仓库基础已完成。

对应提交：

- `8c0eeda feat: initialize phase 0 platform foundation`

## 已完成内容

- 初始化 `SekaiPlatform.sln`。
- 初始化 4 个 ASP.NET Core Web API 项目：
  - `apps/api-service`
  - `apps/auth-service`
  - `apps/asset-service`
  - `apps/search-service`
- 初始化 1 个 Worker 项目：
  - `apps/sync-worker`
- 初始化 shared class library：
  - `packages/shared`
- 将所有 .NET 项目加入 solution。
- 让 5 个服务项目引用 `packages/shared`。
- 为 4 个 Web API 提供 `/health`。
- 添加 Dockerfile、Docker Compose、`.env.example`、`.gitignore`、`.dockerignore`。
- 添加目录占位：
  - `database/migrations`
  - `deploy/elasticsearch`
- 更新 README，补充本地启动、端口、编译和 Apple Silicon Elasticsearch JVM 参数说明。

## 验证结果

已验证通过：

- `dotnet restore SekaiPlatform.sln --disable-parallel -v minimal`
- `dotnet build SekaiPlatform.sln --no-restore`
- `docker compose config`
- `docker compose up --build -d`
- `GET http://localhost:8080/health` 返回 `Healthy`
- `GET http://localhost:9200/_cluster/health?pretty` 返回 `green`
- `docker compose ps` 显示 PostgreSQL、Elasticsearch 和 5 个后端服务容器运行中

## 本地注意事项

- `.env` 不提交；本机已按 `.env.example` 创建。
- Apple Silicon / M 系列机器上，如果 Elasticsearch 因 JVM `SIGILL` 退出，需要在 `.env` 中设置：

```bash
ELASTICSEARCH_JAVA_OPTS=-Xms512m -Xmx512m -XX:UseSVE=0
ELASTICSEARCH_CLI_JAVA_OPTS=-XX:UseSVE=0
```

- `bin/`、`obj/`、`.env` 均已被 `.gitignore` 忽略。

## 后续衔接

Phase 1 可在当前基础上继续：

- 固化所有后端服务的 `/health` 约定。
- 定义 JWT、Cookie、Bearer Token 和错误响应格式。
- 定义 API Service 到内部服务的 HTTP 调用约定。
- 定义当前用户、当前租户在服务间传递的 header 或上下文格式。
- 在 Apifox 项目 `8210187` 中维护 API 草案，文档站为 <https://sekai-platform.apifox.cn/>。
