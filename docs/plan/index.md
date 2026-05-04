# PJS 字幕组语言资产平台实施计划

## 目标

从空仓库搭建一期平台。

一期交付范围：

- Docker Compose 本地开发环境。
- ASP.NET Core API Service。
- Auth Service、Asset Service、Search Service、Sync Worker。
- PostgreSQL 数据库。
- Elasticsearch 搜索索引。
- 原文同步、历史译文批量导入、搜索、剧情详情 API。

## 工程约定

- .NET 项目初始化、solution 管理、项目引用、包引用优先使用 `dotnet` CLI。
- 不手写可由 `dotnet new`、`dotnet sln`、`dotnet add` 生成或维护的项目骨架文件。
- 从阶段 3 开始，新增或调整业务接口时必须同步编写接口集成测试。
- 接口集成测试优先放在 `tests/integration-tests/`，使用专用测试租户和专用测试超级管理员账号，不复用应用默认 seed 用户。
- 测试数据写入必须幂等，重复运行不得产生重复租户、重复用户或重复租户成员关系。
- 涉及鉴权、租户隔离、权限边界、数据写入或跨服务调用的接口，完成标准必须包含对应集成测试通过。

## 阶段 0：仓库基础

### 任务

- 初始化目录结构。
- 使用 `dotnet new sln` 初始化 .NET solution。
- 使用 `dotnet new webapi` 初始化 API Service、Auth Service、Asset Service、Search Service。
- 使用 `dotnet new worker` 初始化 Sync Worker。
- 使用 `dotnet new classlib` 初始化 shared 项目。
- 使用 `dotnet sln add` 添加所有 .NET 项目。
- 使用 `dotnet add reference` 添加项目引用。
- 添加 Docker Compose。
- 添加统一配置样例。
- 添加基础 README。

### 目录结构

```text
.
├── apps
│   ├── api-service
│   ├── auth-service
│   ├── asset-service
│   ├── search-service
│   └── sync-worker
├── packages
│   └── shared
├── database
│   └── migrations
├── deploy
│   └── elasticsearch
└── docs
    ├── design
    └── plan
```

### 完成标准

- `docker compose up` 能启动 PostgreSQL、Elasticsearch 和空服务容器。
- .NET solution 能编译。

完成记录：见 [Phase 0 完成记录](phase-0-status.md)。

## 阶段 1：共享约定

### 任务

- 定义 JWT 鉴权方式。
- 支持 `SEKAI_PLATFORM_AUTH` Cookie。
- 支持 `Authorization: Bearer <token>`。
- 定义错误响应格式。
- 定义服务间 HTTP 调用约定。
- 定义当前用户、当前租户的传递方式。
- 定义基础健康检查接口。
- 在 Apifox 项目 `8210187` 中维护 API 草案，文档站为 <https://sekai-platform.apifox.cn/>。

### 完成标准

- 所有后端服务提供 `/health`。
- 正常响应直接返回 JSON 结果。
- 错误响应返回 `{ msg, trace_id }`。
- API Service 能调用内部服务的健康检查。

完成记录：见 [Phase 1 完成记录](phase-1-status.md)。

## 阶段 2：数据库

### 任务

- 使用 EF Core 建立数据库模型。
- 使用 EF Core migrations 管理 PostgreSQL schema。
- 创建租户、用户、用户租户关系表。
- 创建剧情集、剧情、原文行表。
- 创建翻译版本、翻译行表。
- 创建同步任务日志表。
- 准备本地 seed 数据。

### 完成标准

- 本地数据库可一键初始化。
- seed 后存在默认租户和默认超级管理员用户。
- 数据模型覆盖 @../design/data-model.md。

完成记录：见 [Phase 2 完成记录](phase-2-status.md)。

## 阶段 3：鉴权和租户

### 任务

- 实现用户名密码登录。
- 实现登出。
- 实现获取登录状态。
- 实现可用租户列表。
- 实现选择和切换租户。
- 实现用户邀请。

### 完成标准

- 未登录用户不能访问业务 API。
- 未选择租户用户不能访问业务 API。
- API Service 从登录状态获取当前租户。
- 客户端传入的租户 ID 不作为权限依据。

完成记录：见 [Phase 3 完成记录](phase-3-status.md)。

## 阶段 4：外部数据源同步

### 任务

- 实现 Haruki master JSON 下载。
- 实现 master JSON 解析。
- 实现 catalog 数据入库。
- 实现 scenario URL 生成。
- 实现 scenario JSON 下载。
- 实现 Unity scenario JSON 解析为原文行。
- 实现同步日志。
- 实现每天一次自动同步。
- 实现管理员手动同步。

### 完成标准

- 能同步活动剧情和卡面剧情。
- 同步成功后数据库存在剧情集、剧情和原文行。
- 同步失败记录错误日志。
- 同步失败不自动重试。

## 阶段 5：搜索索引

### 任务

- 准备 Elasticsearch Docker 镜像。
- 安装 `analysis-smartcn`。
- 安装 `analysis-kuromoji`。
- 安装 `analysis-icu`。
- 创建统一索引 mapping。
- 实现原文索引写入。
- 实现译文索引写入。
- 实现索引重建接口。

### 完成标准

- 原文文档不绑定租户。
- 译文文档必须绑定 `tenant_id`。
- 索引字段包含资产类型、剧情 ID、翻译版本 ID、说话人、行号、文本。
- 支持中文分词、日文分词、大小写折叠、全半角兼容。

## 阶段 6：搜索 API

### 任务

- 实现关键词搜索。
- 实现分页。

### 完成标准

- 搜索结果按行返回。
- 当前租户用户能搜共享原文。
- 当前租户用户只能搜当前租户译文。
- 返回剧情、剧情集、剧情类型、行号、说话人和命中文本。

## 阶段 7：历史译文批量导入

### 任务

- 定义批量导入目标结构体。
- 实现历史译文批量导入接口。
- 实现剧情匹配。
- 写入翻译版本和翻译行。
- 写入译文搜索索引。

### 完成标准

- 导入接口只接受 JSON。
- 不提供 TXT 导入。
- 导入后能在当前租户内搜索到译文。

## 阶段 8：剧情详情

### 任务

- 实现剧情详情 API。
- 实现原文行查询 API。
- 实现翻译版本列表 API。
- 实现翻译行查询 API。

### 完成标准

- 剧情详情能展示原文和译文版本。
- 翻译版本和翻译行只返回当前租户内的数据。

## 阶段 9：本地交付

### 任务

- 完善 Docker Compose。
- 添加本地启动脚本。
- 添加数据库初始化说明。
- 添加 Elasticsearch 插件构建说明。
- 添加基础测试。
- 添加接口冒烟测试。

### 完成标准

- 新环境按 README 可启动完整本地系统。
- 本地 seed 数据可登录。
- 冒烟测试覆盖登录、租户选择、同步、搜索、导入、剧情详情。

## 实施顺序

1. 仓库基础。
2. 数据库 migration。
3. API Service 和 Auth Service。
4. Asset Service 和 Sync Worker。
5. Search Service 和 Elasticsearch 索引。
6. 历史译文批量导入。
7. Docker Compose 联调。
8. 冒烟测试。

## 首个可运行里程碑

最小可运行版本包含：

- Docker Compose。
- PostgreSQL。
- Elasticsearch。
- API Service。
- Auth Service。
- Asset Service。
- Search Service。
- Sync Worker。
- 默认租户和管理员账号。
- 手动同步活动剧情。
- 搜索原文。

## 暂不处理

- TXT 导入。
- 实时协同。
- 翻译工作流。
- AI 翻译。
- 自动发布。
- 移动端适配。
- 消息队列。
- gRPC。
- Kubernetes。
- 前端页面。
- TXT 导出。
