# 接口概览

## 文档定位

正式 API 文档维护在 Apifox。

- Apifox 项目编号：`8210187`
- 文档站：<https://sekai-platform.apifox.cn/>

当前仓库只保留接口语义概览，不维护本地 OpenAPI 源文件。

## 全局约定

API Base URL：

```text
http://localhost:8080
```

统一约定：

- 外部 API 使用 `/api` 前缀。
- 正常响应直接返回 JSON 结果，不额外包裹。
- 错误响应返回 `{ "msg": "...", "trace_id": "..." }`。
- 字段使用 snake_case。
- 外部用户 token 支持 `SEKAI_PLATFORM_AUTH` Cookie 和 `Authorization: Bearer <token>`。
- 业务接口使用登录态中的当前租户。
- 当前租户为空时拒绝访问业务接口。
- 内部 `/internal/...` endpoint 不作为前端公开 API。

## 外部 API

| 模块 | 方法 | 路径 | 说明 |
|---|---|---|---|
| Auth | POST | `/api/auth/login` | 用户名密码登录 |
| Auth | POST | `/api/auth/logout` | 登出 |
| Auth | GET | `/api/auth/session` | 当前登录状态 |
| Auth | GET | `/api/auth/tenants` | 当前用户可访问租户 |
| Auth | PUT | `/api/auth/current-tenant` | 选择或切换当前租户 |
| Users | POST | `/api/users/invitations` | 邀请用户加入当前租户 |
| Assets | GET | `/api/story-types` | 剧情类型 |
| Assets | GET | `/api/story-groups` | 剧情集列表 |
| Assets | GET | `/api/story-groups/{storyGroupId}` | 剧情集详情 |
| Assets | GET | `/api/stories` | 剧情列表 |
| Assets | GET | `/api/stories/{storyId}` | 剧情详情 |
| Assets | GET | `/api/stories/{storyId}/source-lines` | 原文行 |
| Assets | GET | `/api/stories/{storyId}/translation-versions` | 当前租户翻译版本列表 |
| Assets | GET | `/api/translation-versions/{translationVersionId}` | 翻译版本详情 |
| Assets | GET | `/api/translation-versions/{translationVersionId}/lines` | 翻译行 |
| Search | GET | `/api/search` | 搜索原文和当前租户译文 |
| Search | POST | `/api/search/index/rebuild` | 异步触发搜索索引重建 |
| Sync | POST | `/api/sync/jobs` | 手动触发外部数据源同步 |
| Sync | GET | `/api/sync/jobs` | 同步任务列表 |
| Sync | GET | `/api/sync/jobs/{syncJobId}` | 同步任务详情 |
| Import | POST | `/api/import/translation-versions` | 批量导入历史译文 |

## 对象

| 对象 | 说明 |
|---|---|
| `Tenant` | 租户 |
| `UserProfile` | 用户对外可见信息 |
| `TenantMembership` | 租户成员关系 |
| `StoryGroup` | 剧情集 |
| `Story` | 剧情 |
| `StorySourceLine` | 原文行 |
| `TranslationVersion` | 当前租户翻译版本，包含 `metadata.staff` |
| `TranslationLine` | 当前租户翻译行 |
| `SearchHit` | 行级搜索命中 |
| `SyncJob` | 同步任务 |

## 登录和租户

登录输入：

- `username`：QQ 号。
- `password`：登录密码。

登录响应包含：

- `access_token`
- `expires_at`
- `user`
- `current_tenant`
- `tenants`

处理规则：

- 用户没有 active 租户时拒绝登录。
- 用户只有一个 active 租户时直接选中。
- 用户有多个 active 租户时进入租户选择。
- 登录成功后写入 `SEKAI_PLATFORM_AUTH` HttpOnly Cookie。
- 切换租户后重新签发 token 并刷新 Cookie。

邀请规则：

- `admin` 和 `super_admin` 可以邀请普通用户。
- 只有 `super_admin` 可以授予 `admin` 或 `super_admin`。
- 新用户默认密码为 QQ 号后六位。
- 重复邀请同一用户且角色相同时保持幂等。
- 用户已属于当前租户但角色不同的，返回冲突错误。

## 资产读取

列表接口支持分页：

- `page` 从 1 开始。
- `page_size` 范围 1 到 100。
- 结果窗口不得超过 10000。

过滤能力：

| 接口 | 过滤 |
|---|---|
| `/api/story-groups` | `story_type`、`keyword` |
| `/api/stories` | `story_group_id`、`story_type`、`keyword` |
| `/api/stories/{storyId}/translation-versions` | 当前租户 |
| `/api/translation-versions/{translationVersionId}` | 当前租户 |
| `/api/translation-versions/{translationVersionId}/lines` | 当前租户 |

跨租户翻译版本和翻译行返回 404。

## 搜索

请求：

```text
GET /api/search?keyword=...&page=1&page_size=20
```

规则：

- `keyword` 去除首尾空白后不能为空。
- 同时搜索共享原文和当前租户译文。
- 原文不绑定租户。
- 译文必须按当前 `tenant_id` 过滤。
- 结果按行返回。
- 每条结果返回剧情、剧情集、剧情类型、说话人、行号、命中文本、高亮文本。
- 命中原文时返回当前租户同一原文行的译文上下文。
- 命中译文时返回对应原文上下文。

## 搜索索引重建

`POST /api/search/index/rebuild` 由当前租户 `super_admin` 触发。

规则：

- API Service 校验用户角色。
- Search Service 写入后台队列。
- 接口返回 `202 Accepted`。
- 后台串行执行索引重建。
- 执行结果写入服务日志。

## 同步

`POST /api/sync/jobs` 由当前租户 `admin` 或 `super_admin` 触发。

规则：

- 同一时间只允许一个原文同步任务运行。
- 单条 scenario 下载或解析失败时记录失败样本并继续。
- 如果没有任何 scenario 成功同步，任务标记为失败。
- 失败后不自动重试。

## 历史译文导入

`POST /api/import/translation-versions` 只接受 JSON。

导入项字段：

- `story_type`
- `scenario_id`
- `title`
- `metadata.staff`
- `lines[].line_no`
- `lines[].text`
- `lines[].speaker`
- `lines[].metadata`

规则：

- 写入当前租户。
- 一次请求可导入多个剧情。
- 剧情通过 `story_type + scenario_id` 匹配。
- 翻译行通过 `line_no` 匹配原文行。
- 同一租户同一剧情每次导入创建新的 `version_no`。
- 任意项校验失败时整批不写入。
- 事务提交后刷新对应译文搜索索引。
