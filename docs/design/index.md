# PJS 字幕组语言资产平台设计文档

## 定位

平台用于管理字幕组语言资产。一期后端已经完成共享原文同步、租户译文导入、统一搜索和剧情读取能力。后续开放 API 通过独立 OpenApiService 提供单独监听端口，当前暂不开放业务接口。

## 核心模型

- 原文剧情是平台共享资产。
- 译文、翻译版本和翻译行是租户资产。
- 用户通过租户成员关系获得访问权限。
- 业务接口只使用登录态中的当前租户，不信任客户端传入的租户 ID。
- 搜索按行返回结果，原文和当前租户译文在同一索引中检索。

## 能力范围

已实现：

- 用户名密码登录、登出、会话读取、租户选择、用户邀请。
- Moe Sekai / Exmeaning master JSON 与 scenario JSON 同步。
- 活动剧情、主线剧情、卡面剧情、区域对话、特殊剧情原文入库。
- Elasticsearch 原文和译文索引。
- 原文和当前租户译文关键词搜索。
- 历史译文 JSON 批量导入。
- 剧情类型、剧情集、剧情、原文行、翻译版本、翻译行读取接口。
- Docker Compose 本地运行和服务器部署基线。

不处理：

- TXT 导入和导出。
- 实时协同编辑。
- 翻译、校对、合意工作流。
- AI 翻译。
- 自动发布。
- 移动端适配。

## 架构

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

External Partner
    |
OpenApiService

Sync Worker -------- Moe Sekai / Exmeaning
    |
    +-------------- PostgreSQL
    |
    +-------------- Search Service --- Elasticsearch
```

服务职责：

| 服务 | 职责 |
|---|---|
| API Service | 外部统一入口，处理登录态、权限、参数校验、服务编排和错误格式。 |
| OpenApiService | 开放 API 独立监听入口，处理匿名访问、IP 限流和后续开放接口承载。 |
| Auth Service | 用户登录、会话、租户选择、可访问租户、用户邀请。 |
| Asset Service | 剧情资产读取、原文同步落库、译文版本和译文行、历史译文导入。 |
| Search Service | Elasticsearch mapping、索引维护、关键词查询和租户过滤。 |
| Sync Worker | 定时同步 Moe Sekai / Exmeaning 数据源。 |

## 数据隔离

- `story_groups`、`stories`、`story_source_lines` 全平台共享。
- `translation_versions`、`translation_lines` 按 `tenant_id` 隔离。
- 搜索原文不绑定租户。
- 搜索译文必须按当前 `tenant_id` 过滤。
- Asset Service 和 Search Service 在内部接口中继续校验用户仍是当前租户 active 成员。

## 搜索

Elasticsearch 统一索引名为 `sekai-language-assets-v1`。

文档类型：

| 类型 | 文档 ID | 租户 |
|---|---|---|
| 原文 | `source:{source_line_id}` | 不绑定租户 |
| 译文 | `translation:{translation_line_id}` | 必须绑定 `tenant_id` |

索引字段包含资产类型、租户 ID、剧情 ID、剧情类型、scenario ID、剧情标题、剧情集 ID、剧情集标题、翻译版本 ID、原文行 ID、行号、说话人和正文。

分词能力：

| 能力 | Elasticsearch 插件 |
|---|---|
| 中文分词 | `analysis-smartcn` |
| 日文分词 | `analysis-kuromoji` |
| Unicode 规范化、大小写折叠、全半角兼容 | `analysis-icu` |

索引刷新规则：

- 原文同步成功后刷新对应 story 的原文索引和相关译文剧情元信息。
- 历史译文导入成功后触发 `search.translation.refresh`。
- 刷新失败不回滚 PostgreSQL 结果，可通过重建接口修复。

## 鉴权

平台采用统一安全模型，详见 `security-model.md`。

- 外部用户 token 只用于前端到 API Service 的登录态。
- 开放 API 当前允许匿名访问，只建立合作伙伴到 OpenApiService 的独立监听链路，暂不开放业务接口。
- 内部服务调用只接受内部 token。
- 内部 token 使用非对称签名，携带调用方、目标服务、scope、可选用户和租户上下文。
- 内部服务不得使用外部 JWT、上下文 Header 或 maintenance token 作为授权依据。

## 数据源

原文剧情来自 Moe Sekai / Exmeaning 公共静态数据源：

- master JSON 提供活动、主线、卡牌、区域对话、特殊剧情等元数据。
- scenario JSON 提供具体剧情内容。
- 同步结果写入 `story_groups -> stories -> story_source_lines`。

详细规则见 `external-api.md`。

## API 文档

- 正式 API 文档维护在 Apifox。
- 平台业务 API Apifox 项目编号：`8210187`。
- 开放 API Apifox 项目编号：`8216122`。
- 文档站：<https://sekai-platform.apifox.cn/>。
- 当前仓库不维护本地 OpenAPI 源文件。

## 相关文档

- `data-model.md`：核心数据模型。
- `interface.md`：仓内接口概览。
- `open-api.md`：开放 API 草案和匿名访问设计。
- `external-api.md`：Moe Sekai / Exmeaning 数据源同步规则。
- `security-model.md`：统一安全模型。
