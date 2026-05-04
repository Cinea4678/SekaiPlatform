# sekai-platform

PJS 字幕组语言资产检索平台。

当前已完成 Phase 3 鉴权和租户。一期聚焦后端能力：原文同步、历史译文批量导入、租户隔离检索和剧情详情 API。

## 本地依赖

- .NET SDK 10
- Docker Desktop 或兼容 Docker Compose v2 的运行环境
- 本地 .NET 工具通过 `dotnet tool restore` 安装

## 本地启动

复制本地配置样例：

```bash
cp .env.example .env
```

启动基础设施和服务容器：

```bash
docker compose up --build
```

API Service 在 Docker Compose 的 Development 环境中默认会自动执行 EF Core migration 并写入 seed 数据。可在 `.env` 中关闭：

```bash
DATABASE_AUTO_MIGRATE=false
DATABASE_SEED=false
```

默认 seed 数据：

| 类型 | QQ 号 | 角色 |
|---|---|---|
| 超级管理员用户 | `1650121748` | `super_admin` |

默认不写入可登录密码。如需为本地 seed 用户写入密码哈希，在 `.env` 中设置：

```bash
SEED_ADMIN_PASSWORD=your-local-admin-password
```

默认端口：

| 服务 | 地址 |
|---|---|
| API Service | http://localhost:8080 |
| PostgreSQL | localhost:5432 |
| Elasticsearch | http://localhost:9200 |

API Service 健康检查：

```bash
curl http://localhost:8080/health
```

内部服务健康聚合：

```bash
curl http://localhost:8080/api/internal-services/health
```

如果 Apple Silicon / M 系列机器上 Elasticsearch 因 JVM `SIGILL` 退出，可在 `.env` 中改为：

```bash
ELASTICSEARCH_JAVA_OPTS=-Xms512m -Xmx512m -XX:UseSVE=0
ELASTICSEARCH_CLI_JAVA_OPTS=-XX:UseSVE=0
```

当前 Docker Compose 中 5 个 .NET 服务固定为 `linux/amd64` 平台，用于规避本机 Docker Desktop 下 .NET 10 ARM64 SDK 容器在构建阶段偶发 `Illegal instruction` 的问题。Dockerfile 本身不设置硬件指令兼容开关，避免影响其他部署环境。

如果本机已有进程占用 IPv4 `localhost:8080`，可以使用 IPv6 loopback 访问 API Service：

```bash
curl -g 'http://[::1]:8080/health'
```

## .NET 工程

编译 solution：

```bash
dotnet build SekaiPlatform.sln
```

生成或更新数据库：

```bash
dotnet tool restore
POSTGRES_PASSWORD=sekai_platform dotnet dotnet-ef database update --project database/SekaiPlatform.Database.csproj
```

运行接口集成测试：

```bash
dotnet test tests/integration-tests/SekaiPlatform.IntegrationTests.csproj
```

集成测试会向当前 PostgreSQL 环境 upsert 一组专用测试数据：

| 类型 | 值 |
|---|---|
| 租户 | `集成测试租户` |
| 超级管理员 QQ | `900000000001` |
| 超级管理员密码 | `sekai-integration-test-password` |

这组数据会保留在本地数据库中，供后续接口测试复用。它不属于应用启动 seed，只有运行集成测试时才会写入；重复运行会更新同一组记录，不会重复插入。

项目结构：

```text
apps/
  api-service/
  auth-service/
  asset-service/
  search-service/
  sync-worker/
packages/
  shared/
database/
  migrations/
tests/
  integration-tests/
deploy/
  elasticsearch/
```

## 文档

- [总体设计](docs/design/index.md)
- [接口草案](docs/design/interface.md)
- [数据模型](docs/design/data-model.md)
- [外部数据源](docs/design/external-api.md)
- [实施计划](docs/plan/index.md)

## 约定

- API 文档维护在 Apifox 项目 `8210187`，文档站：<https://sekai-platform.apifox.cn/>。
- 当前仓库不维护本地 OpenAPI 源文件；如需机器可读文档，优先使用 ASP.NET Core 自动生成能力，或从 Apifox 导出/集成。
- .NET 工程初始化和引用管理优先使用 `dotnet` CLI。
- 数据库访问和迁移使用 EF Core。
