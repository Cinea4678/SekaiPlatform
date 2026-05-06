# 开放 API 概览

## 文档定位

正式开放 API 文档维护在 Apifox。

- 开放 API Apifox 项目编号：`8216122`
- 文档站：<https://sekai-platform.apifox.cn/>

当前仓库只保留开放 API 语义概览、安全边界和接口草案，不维护本地 OpenAPI 源文件。

## 全局约定

开放 API 由 OpenApiService 在独立端口承载。

统一约定：

- 开放 API 使用 `/api` 前缀。
- 调用方不需要登录态、Cookie、Bearer Token 或 API Key。
- OpenApiService 不读取外部用户 Cookie。
- OpenApiService 不暴露内部 `/internal/...` endpoint。
- 正常响应直接返回 JSON 结果，不额外包裹。
- 错误响应返回 `{ "code": "...", "message": "...", "trace_id": "..." }`。
- 字段使用 snake_case。
- 所有请求执行 IP 限流。

## 服务边界

```text
External Partner
    |
OpenApiService
```

服务职责：

| 服务 | 职责 |
|---|---|
| OpenApiService | 开放 API 独立监听入口，处理匿名访问、IP 限流、错误格式和开放接口承载。 |

内部调用：

| 场景 | 下游服务 | 内部 scope |
|---|---|---|
| 公开翻译读取 | Asset Service | `public.translation.read` |

## 限流

开放 API 必须限流。

限流规则：

| 维度 | 默认值 |
|---|---:|
| 单 IP | 10 req/min |

IP 来源：

- 优先使用 `X-Forwarded-For` 中的客户端 IP。
- `X-Forwarded-For` 只信任受控反向代理写入的值。
- 多级代理场景取第一个有效 IP。
- 没有可信 `X-Forwarded-For` 时使用连接远端 IP。

超过限制时返回 `429 Too Many Requests`，并尽量返回 `Retry-After`。

## 开放 API

| 模块 | 方法 | 路径 | 说明 |
|---|---|---|---|
| Public Translations | GET | `/api/public/translations/{scenario_id}` | 获取单个剧情的公开翻译信息和翻译内容 |
| Public Translations | POST | `/api/public/translations/batch` | 批量获取多个剧情的公开翻译信息 |

## 对象

| 对象 | 说明 |
|---|---|
| `PublicTranslationResult` | 单个 `scenario_id` 的公开翻译查询结果 |
| `PublicTranslationInfo` | 已公开发布的翻译版本信息 |
| `PublicTranslationLine` | 已公开发布版本下的翻译行 |
| `PublicTenant` | 提供公开翻译的租户信息 |
| `PublicTranslationStaff` | 翻译版本署名人员 |

## 公开翻译

公开翻译接口只返回 `translation_versions.is_published = true` 且未软删除的翻译版本。

查询键：

- 使用 `scenario_id` 定位剧情。
- `scenario_id` 对应 Moe Sekai 剧情阅读页使用的 `scenarioId`。
- 调用方不需要传 `event_id`、`episode_no`、`assetbundleName`、`card_id` 或 `action_set_id`。

Moe Sekai 对接来源：

| 剧情类型 | Moe Sekai 来源 | 平台字段 |
|---|---|---|
| 活动剧情 | `eventStoryEpisodes[].scenarioId` | `stories.scenario_id` |
| 主线剧情 | `unitStories.chapters[].episodes[].scenarioId` | `stories.scenario_id` |
| 卡面剧情 | `cardEpisodes[].scenarioId` | `stories.scenario_id` |
| 区域对话 | `actionSets[].scenarioId` | `stories.scenario_id` |
| 特殊剧情 | `specialStories.episodes[].scenarioId` | `stories.scenario_id` |

`GET /api/public/translations/{scenario_id}`：

- 返回 `PublicTranslationResult`。
- `scenario_id` 未同步或没有公开翻译时返回 `200 OK`，`has_translation` 为 `false`。
- 有公开翻译时 `has_translation` 为 `true`，`translations` 返回一个或多个 `PublicTranslationInfo`。
- 响应包含 `lines`，按 `line_no asc` 排序。

`POST /api/public/translations/batch`：

- 请求体字段为 `scenario_ids`。
- `scenario_ids` 不能为空，单次最多 100 个。
- 响应保持请求 `scenario_ids` 的顺序。
- 每项返回 `PublicTranslationResult`。
- 批量接口不返回 `lines`。

公开版本排序：

- 同一个 `scenario_id` 存在多个公开版本时全部返回。
- 排序为 `updated_at desc, id desc`。

### PublicTranslationResult

| Field | Type | Nullable | Description |
|---|---|---:|---|
| `scenario_id` | string | No | Moe Sekai 剧情阅读页使用的 `scenarioId`。 |
| `has_translation` | boolean | No | 是否存在公开翻译。 |
| `translations` | PublicTranslationInfo[] | No | 公开翻译版本列表。 |

### PublicTranslationInfo

| Field | Type | Nullable | Description |
|---|---|---:|---|
| `translation_version_id` | number | No | 翻译版本 ID。 |
| `version_no` | number | No | 租户内该剧情的版本号。 |
| `title` | string | Yes | 翻译版本标题。 |
| `tenant` | PublicTenant | No | 提供该翻译的租户。 |
| `staff` | PublicTranslationStaff | No | 署名人员。 |
| `line_count` | number | No | 翻译行数量。 |
| `created_at` | string | No | 翻译版本创建时间，ISO 8601。 |
| `updated_at` | string | No | 翻译版本更新时间，ISO 8601。 |
| `lines` | PublicTranslationLine[] | Yes | 翻译行。批量接口不返回该字段。 |

### PublicTranslationLine

| Field | Type | Nullable | Description |
|---|---|---:|---|
| `line_no` | number | No | 行号，从 1 开始。 |
| `line_type` | string | No | 原文行类型：`dialogue` / `scene` / `upper_scene` / `choice` / `separator`。 |
| `speaker` | string | Yes | 译文说话人。 |
| `text` | string | No | 译文文本。 |

### PublicTenant

| Field | Type | Nullable | Description |
|---|---|---:|---|
| `id` | number | No | 租户 ID。 |
| `name` | string | No | 租户名称。 |
| `avatar_url` | string | Yes | 租户头像。 |

### PublicTranslationStaff

| Field | Type | Nullable | Description |
|---|---|---:|---|
| `translator` | string | Yes | 翻译人员。 |
| `proofreader` | string | Yes | 校对人员。 |
| `approver` | string | Yes | 合意人员。 |

`staff` 从 `translation_versions.metadata.staff` 读取，缺失字段返回 `null`。

## 公开数据边界

允许返回：

- 已公开发布的翻译版本。
- 已公开发布版本下的翻译行。
- 提供公开翻译的租户公开信息。
- 翻译版本署名人员。

不得返回：

- 未公开发布的翻译版本。
- 未公开发布版本下的翻译行。
- 未公开发布版本的租户、人员和 metadata。
- 用户、租户成员、内部权限、内部 token 和内部 trace 细节。

## 未开放接口

- 剧情类型读取。
- 剧情集读取。
- 剧情读取。
- 原文行读取。
- 搜索。
- 用户、租户、登录、邀请、租户切换。
- 导入、同步、索引重建等写操作和维护操作。
- 内部服务路径、内部 token、内部 trace 细节。

## 错误格式

开放 API 使用稳定错误码，避免把内部错误直接透传给合作伙伴。

```json
{
  "code": "not_found",
  "message": "Not found",
  "trace_id": "..."
}
```

常用错误码：

| HTTP | code | 说明 |
|---:|---|---|
| 400 | `bad_request` | 请求参数格式错误。 |
| 404 | `not_found` | 当前路径未开放。 |
| 429 | `rate_limited` | 超出限流。 |
| 500 | `internal_error` | 服务内部错误。 |

公开翻译接口对“剧情存在但没有公开翻译”和“平台未同步该 `scenario_id`”均返回 `200 OK` + `has_translation: false`。

## 暂不设计的能力

当前开放 API 草案不包含：

- API Key。
- 合作伙伴账号。
- 合作伙伴级 scope。
- 合作伙伴级配额。
- API Key 签发、吊销和管理接口。

后续如果开放 API 需要分合作伙伴授权，再新增对应鉴权、数据模型和管理接口。
