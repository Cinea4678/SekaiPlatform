# PJS 字幕组语言资产平台 设计文档

## 总述

本平台用于管理字幕组语言资产。一期以语言资产检索为核心。

主要使用方是字幕组组员。

一期核心能力：在多租户隔离前提下，对全平台共享的剧情原文和租户内译文进行统一检索和查看。

## 一期范围

### 目标

- 支持多租户能力，多个字幕组可以共用同一平台。
- 原文剧情作为全平台共享资产存储和索引。
- 译文和翻译版本按租户隔离存储和索引。
- 检索数据库内已有的所有原文和译文。
- 支持关键词搜索。
- 搜索结果按“行”返回，并能定位到对应剧情、剧情集和翻译版本。
- 提供外部数据源定时同步能力，将上游剧情数据同步到平台。
- 提供导入接口，用于后续逐步迁移已有译文资产。
- 提供基础的用户登录、租户选择、用户邀请、日志和监控能力。

### 非目标

- 不做实时协同编辑。
- 不做复杂权限矩阵。
- 不做翻译、校对、合意等协作工作流。
- 不做 AI 翻译。
- 不做自动发布。
- 不做移动端适配。
- 不把 `SekaiText` 作为运行时依赖。

## 用户和权限

一期只区分以下角色：

- 普通用户：可以检索、查看租户内可访问的语言资产。
- 管理员：除普通用户能力外，可以邀请用户，并使用导入接口迁移译文资产。
- 超级管理员：除管理员能力外，可以管理租户级管理员。

一期不区分翻译、校对、合意、导入员、只读成员等更细角色。

## 语言资产范围

一期检索范围包括：

- 剧情原文。
- 数据库内已导入的所有译文。
- 剧情元信息，例如剧情类型、剧情集、章节标题、剧情标题、说话人、行号。

剧情类型至少覆盖活动剧情和卡面剧情。

## 数据来源和同步

原文剧情数据来自公共外部数据源，由平台定时同步。外部数据源的具体信息见 @external-api.md。

同步触发方式：

- 每天自动同步一次。
- 管理员手动触发同步。

同步任务负责：

- 拉取上游剧情列表、剧情详情和原文行。
- 将外部数据转换为平台内部数据模型。
- 写入 PostgreSQL。
- 更新 Elasticsearch 索引。
- 记录同步日志、开始时间、结束时间、结果状态和错误信息。
- 失败后不自动重试。

译文数据通过导入接口进入平台。导入输入只支持 JSON。

## SekaiText 参考关系

`/Users/zhangyao/build6/SekaiText` 作为格式和逻辑参考。本平台不直接依赖该项目。

可参考内容包括：

- `SourceTalk` / `DstTalk` 的文本行结构。
- `\N` 换行保存规则。
- 剧情类型、索引、章节和 `scenarioId` 的处理方式。

## 搜索设计

搜索必须满足租户隔离：

- 原文是全平台共享资产，所有有权限的租户用户均可检索。
- 译文、翻译版本是租户资产，只能在当前登录租户内检索。
- 所有搜索请求必须带当前租户上下文。
- 查询译文索引时必须按 `tenant_id` 过滤。

搜索能力：

- 关键词搜索原文和译文。
- 支持日文、中文分词。
- 支持大小写、全半角兼容。

搜索结果粒度为“行”。每条结果至少包含：

- 命中文本。
- 命中字段：原文或译文。
- 说话人。
- 行号。
- 剧情 ID、剧情标题。
- 剧情集 ID、剧情集标题。
- 剧情类型。
- 翻译版本信息（命中译文时）。

Elasticsearch 一期使用统一索引，原文和译文通过字段区分。索引文档需要携带资产类型、租户 ID、剧情 ID、翻译版本 ID 等字段：

- 原文文档不绑定租户。
- 译文文档必须绑定 `tenant_id`。
- 查询时根据当前租户同时检索共享原文和当前租户译文。

分词组件：

| 语言/能力 | 组件 |
|---|---|
| 中文分词 | Elasticsearch `analysis-smartcn` 插件 |
| 日文分词 | Elasticsearch `analysis-kuromoji` 插件 |
| Unicode 规范化、大小写折叠、全半角兼容 | Elasticsearch `analysis-icu` 插件 |

## 历史译文导入

一期提供历史译文批量导入接口。

导入接口应支持：

- 指定租户。
- 指定剧情或通过外部标识匹配剧情。
- 以 JSON 形式上传批量导入目标结构体。
- 导入后更新 PostgreSQL 和 Elasticsearch。

一期不提供 TXT 导入功能。

## 技术选型

| 层级 | 技术 | 说明 |
|---|---|---|
| 后端 | .NET / ASP.NET Core | 用于构建 Web API、后台任务和微服务。 |
| 前端 | Vue | 用于构建检索、查看、导入和管理页面。 |
| 数据库 | PostgreSQL | 作为主存储，保存租户、用户、剧情、原文、译文和任务状态。 |
| ORM | EF Core | 用于 .NET 服务访问 PostgreSQL 和管理数据库迁移。 |
| 搜索引擎 | Elasticsearch | 用于全文搜索、分词和租户隔离查询。 |
| 部署 | Docker Compose | 一期以低成本、易部署、易备份为优先。 |
| API 层 | ASP.NET Core Web API | 作为外部请求入口，负责鉴权、验签、参数校验和服务编排。 |

## 服务间通信方案

- 前端只访问 API Service。
- API Service 使用 ASP.NET Core Web API 实现。
- API Service 完成外部请求的登录校验、权限校验、请求验签、参数校验和返回格式统一。
- API Service 通过内部通信方式调用 Auth Service、Asset Service、Search Service 等内部容器服务。
- 内部服务之间使用 HTTP REST 同步调用。
- Docker Compose 下使用服务名作为内部网络地址。
- 通过 ASP.NET Core 的配置、健康检查、日志和依赖注入管理服务连接。

## 日志和追踪设计

后端服务使用 .NET `System.Diagnostics.Activity` 作为请求追踪上下文，日志使用 ASP.NET Core `ILogger`。

追踪约定：

- `trace_id` 优先使用 `Activity.Current.TraceId`。
- 没有 Activity 时，回退到 `HttpContext.TraceIdentifier`。
- 错误响应中的 `trace_id` 与当前请求日志 scope 中的 `trace_id` 保持一致。
- 服务间 HTTP 调用传播标准 W3C Trace Context，即 `traceparent`。
- 同时透传平台自定义排障 Header：`X-Sekai-Trace-Id`。

日志约定：

- 每个请求进入服务后开启 logging scope。
- scope 字段至少包含 `trace_id`、`user_id`、`tenant_id`。
- 控制台日志输出 scope。
- ASP.NET Core logging 启用 `ActivityTrackingOptions.TraceId`、`SpanId`、`ParentId`。
- 同一次外部请求跨多个服务时，`TraceId` 保持一致，`SpanId` 表示每个服务内的当前调用片段。

上下文 Header：

| Header | 说明 |
|---|---|
| `traceparent` | W3C Trace Context 标准 Header。 |
| `X-Sekai-Trace-Id` | 平台可读追踪 ID，用于错误响应和人工排障。 |
| `X-Sekai-User-Id` | 当前用户 ID，由 API Service 传递给内部服务。 |
| `X-Sekai-Tenant-Id` | 当前租户 ID，由 API Service 传递给内部服务。 |

Header 信任边界：

- 外部入口不得信任客户端传入的 `X-Sekai-User-Id`、`X-Sekai-Tenant-Id`。
- API Service 必须基于已验证登录状态覆盖或剥离客户端自带的同名上下文 Header。
- 只有 API Service 到内部服务的受控链路可以写入用户和租户上下文 Header。
- 内部服务读取这些 Header 仅作为服务间上下文传递结果，不能将其作为绕过鉴权的独立依据。
- 如果内部服务未来暴露到公网、跨网络边界或绕过 API Service 访问，必须补充服务间认证或网关隔离。

## API 文档

- API 文档使用 Apifox 编写和维护。
- Apifox 项目编号：`8210187`。

## 总体架构

一期采用微服务架构，使用 Docker Compose 部署。

系统由以下服务组成：

1. API Service：统一暴露前端调用入口，处理验签、鉴权、参数校验、服务编排和返回格式统一。
2. Auth Service：负责用户名密码登录、登出、登录状态、租户选择和用户邀请。
3. Asset Service：负责剧情、原文、译文、翻译版本和导入。
4. Search Service：负责封装 Elasticsearch 查询和索引写入。
5. Sync Worker：负责定时调用外部数据源，同步原文剧情数据。
6. Web Frontend：Vue 前端应用。
7. PostgreSQL：主数据库。
8. Elasticsearch：搜索引擎。

模块关系：

```text
Web Frontend
    |
API Service
    |
    +-- Auth Service ---- PostgreSQL
    |
    +-- Asset Service --- PostgreSQL
    |        |
    |        +---------- Search Service --- Elasticsearch
    |
    +-- Search Service --- Elasticsearch

Sync Worker -------- External Data Source
    |
    +-------------- PostgreSQL
    |
    +-------------- Search Service --- Elasticsearch
```

## 服务边界

### API Service

- 对前端提供统一 API 入口。
- 使用 ASP.NET Core Web API 实现。
- 完成请求验签、登录校验、权限校验和参数校验。
- 将当前用户 ID、当前租户 ID 传递给后端服务。
- 调用内部服务并聚合简单查询结果。
- 统一错误格式和响应格式。

### Auth Service

- 用户名密码登录。
- 保留 OAuth 用户绑定模型，但一期不实现 QQ OAuth 登录。
- 加载用户可访问租户。
- 选择和切换租户。
- 邀请用户。

### Asset Service

- 管理剧情集、剧情、原文行。
- 管理翻译版本和翻译行。
- 提供译文导入接口。
- 在资产变更后触发索引更新。

### Search Service

- 封装 Elasticsearch 查询 DSL。
- 提供关键词搜索。
- 确保译文查询始终带租户过滤。
- 提供索引创建、重建和增量更新能力。

### Sync Worker

- 按计划调用外部数据源。
- 同步原文剧情数据。
- 写入数据库。
- 更新搜索索引。
- 记录同步日志和错误。

## 鉴权设计

一期优先支持用户名密码登录。

登录成功后的用户状态包括：

- 用户 ID。
- 当前租户 ID，可空。

仅当当前租户 ID 不为空时，登录状态才算可以访问业务 API。

如果用户只有一个可访问租户，登录后直接进入该租户。如果用户有多个可访问租户，登录后进入租户选择阶段。

QQ OAuth 作为后续主要登录方式保留设计，不在一期实现。

## 数据隔离

平台采用“共享原文、租户隔离译文”的模型：

- `story_groups`、`stories`、`story_source_lines` 等原文相关数据全平台共享。
- `translation_versions`、`translation_lines` 等译文相关数据按 `tenant_id` 隔离。
- 用户通过 `user_tenants` 获得租户访问权限。
- 所有业务 API 必须从登录状态中获取当前租户，不能信任客户端直接传入的租户 ID。

## 部署设计

一期使用 Docker Compose 部署，至少包含：

- Vue 前端容器。
- API Service 容器。
- Auth Service 容器。
- Asset Service 容器。
- Search Service 容器。
- Sync Worker 容器。
- PostgreSQL 容器。
- Elasticsearch 容器。

部署目标：

- 单机可运行。
- 服务之间通过 Docker Compose 网络通信。
- PostgreSQL 和 Elasticsearch 数据目录持久化。
- 支持基础健康检查。
- 支持日志输出到容器标准输出。

## 前端页面

一期前端至少包含：

- 登录页。
- 租户选择页。
- 搜索页。
- 剧情详情页。
- 导入页。
- 用户邀请页。
- 同步任务状态页。

## 相关文档

- 接口设计文档：@interface.md
- 数据模型设计文档：@data-model.md
- 外部数据源设计文档：@external-api.md
