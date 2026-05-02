# sekai-platform

PJS 字幕组语言资产检索平台。

当前处于 Phase 0 仓库基础阶段。一期聚焦后端能力：原文同步、历史译文批量导入、租户隔离检索和剧情详情 API。

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

如果 Apple Silicon / M 系列机器上 Elasticsearch 因 JVM `SIGILL` 退出，可在 `.env` 中改为：

```bash
ELASTICSEARCH_JAVA_OPTS=-Xms512m -Xmx512m -XX:UseSVE=0
ELASTICSEARCH_CLI_JAVA_OPTS=-XX:UseSVE=0
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

- [总体设计](design-docs/index.md)
- [接口草案](design-docs/interface.md)
- [数据模型](design-docs/data-model.md)
- [外部数据源](design-docs/external-api.md)
- [实施计划](plans/index.md)

## 约定

- API 文档维护在 Apifox 项目 `8210187`。
- .NET 工程初始化和引用管理优先使用 `dotnet` CLI。
