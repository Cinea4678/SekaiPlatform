# sekai-platform

PJS 字幕组语言资产检索平台。

当前已完成 Phase 1 共享约定。一期聚焦后端能力：原文同步、历史译文批量导入、租户隔离检索和剧情详情 API。

## 本地依赖

- .NET SDK 10
- Docker Desktop 或兼容 Docker Compose v2 的运行环境

## 本地启动

复制本地配置样例：

```bash
cp .env.example .env
```

启动基础设施和空服务容器：

```bash
docker compose up --build
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

- API 文档维护在 Apifox 项目 `8210187`。
- .NET 工程初始化和引用管理优先使用 `dotnet` CLI。
- 数据库访问和迁移使用 EF Core。
