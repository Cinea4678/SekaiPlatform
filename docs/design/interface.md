# PJS 字幕组语言资产平台 接口草案

## 文档定位

本文档用于确认 API 草案。正式 API 文档维护在 Apifox。

- Apifox 项目编号：`8210187`
- 文档站：<https://sekai-platform.apifox.cn/>

当前仓库不维护本地 OpenAPI 源文件。接口文档以 Apifox 为准；如需机器可读文档，优先使用 ASP.NET Core 自动生成能力，或从 Apifox 导出/集成。

## 一期 API 总览

API Base URL：

```text
http://localhost:8080
```

一期采用 REST 风格，统一使用 `/api` 前缀。正常响应直接返回 JSON 结果，不额外包裹；错误响应统一返回 `{ "msg": "...", "trace_id": "..." }`。

| 模块 | 方法 | 路径 | 说明 |
|---|---|---|---|
| Auth | POST | `/api/auth/login` | 用户名密码登录 |
| Auth | POST | `/api/auth/logout` | 登出 |
| Auth | GET | `/api/auth/session` | 获取当前登录状态 |
| Auth | GET | `/api/auth/tenants` | 加载当前用户可访问租户 |
| Auth | PUT | `/api/auth/current-tenant` | 选择或切换当前租户 |
| Users | POST | `/api/users/invitations` | 邀请用户加入当前租户 |
| Assets | GET | `/api/story-types` | 查询支持的剧情类型 |
| Assets | GET | `/api/story-groups` | 查询剧情集列表 |
| Assets | GET | `/api/story-groups/{storyGroupId}` | 查询剧情集详情 |
| Assets | GET | `/api/stories` | 查询剧情列表 |
| Assets | GET | `/api/stories/{storyId}` | 查询剧情详情 |
| Assets | GET | `/api/stories/{storyId}/source-lines` | 查询剧情原文行 |
| Assets | GET | `/api/stories/{storyId}/translation-versions` | 查询当前租户翻译版本列表 |
| Assets | GET | `/api/translation-versions/{translationVersionId}` | 查询翻译版本详情 |
| Assets | GET | `/api/translation-versions/{translationVersionId}/lines` | 查询翻译行 |
| Search | GET | `/api/search` | 搜索原文和当前租户译文 |
| Sync | POST | `/api/sync/jobs` | 手动触发外部数据源同步 |
| Sync | GET | `/api/sync/jobs` | 查询同步任务列表 |
| Sync | GET | `/api/sync/jobs/{syncJobId}` | 查询同步任务详情 |
| Import | POST | `/api/import/translation-versions` | 批量导入历史译文 |

## 核心对象

接口对象命名与数据模型保持一致，字段使用 snake_case。

| 对象 | 来源/说明 |
|---|---|
| `Tenant` | 对应 `tenants` |
| `UserProfile` | 对应 `users` 的对外可见字段 |
| `TenantMembership` | 对应 `user_tenants` |
| `StoryGroup` | 对应 `story_groups` |
| `Story` | 对应 `stories` |
| `StorySourceLine` | 对应 `story_source_lines` |
| `TranslationVersion` | 对应 `translation_versions`，按当前租户隔离，包含版本级 `metadata` |
| `TranslationLine` | 对应 `translation_lines`，按当前租户隔离 |
| `SearchHit` | 搜索服务返回的行级命中结果 |
| `SyncJob` | 外部数据源同步任务 |

## 全局约定

### 鉴权

外部 API 支持两种外部用户 token 传递方式：

- Cookie：`SEKAI_PLATFORM_AUTH`
- Header：`Authorization: Bearer <token>`

外部用户 token 表示前端用户登录状态，只用于前端到 API Service 的外部身份边界。API Service 验证外部用户 token 后，按 @security-model.md 为内部服务调用签发面向目标服务的内部 token。

内部服务的 `/internal/...` endpoint 不作为前端公开 API。迁移完成后，内部 endpoint 只接受内部 token，不接受外部用户 token、`X-Sekai-User-Id`、`X-Sekai-Tenant-Id` 或 `X-Sekai-Maintenance-Token` 作为授权依据。

### 租户上下文

- 业务接口使用登录状态中的当前租户。
- 客户端传入的租户 ID 不作为权限依据。
- 当前租户为空时，业务接口返回 401 或 403。
- 译文、翻译版本、导入结果只在当前租户内可见。
- 内部服务从已验证的内部 token claims 获取用户和租户上下文，并继续在本服务内校验成员关系、角色和资源归属。

### 正常响应

正常响应直接返回 JSON 结果，不包裹。

### 错误响应

错误响应格式：

```json
{
  "msg": "错误信息",
  "trace_id": "请求追踪 ID"
}
```

HTTP 状态码按业务语义返回 4xx 或 5xx。

### 参数校验

参数校验按每个接口的实际情况确定。

## 用户管理

### 用户登录状态

- 用户 ID
- 当前租户 ID，可空

仅当当前租户 ID 不为空时，用户可以访问业务接口。

### 用户登录接口

用户使用用户名和密码登录。

输入：

- `username`：QQ 号，对应 `users.qq_id`
- `password`：登录密码

响应：

- `access_token`：JWT，可用于 `Authorization: Bearer <token>`
- `expires_at`：Token 过期时间
- `user`：当前用户资料
- `current_tenant`：当前租户，未选择租户时为 `null`
- `tenants`：当前用户 active 租户列表

处理规则：

- 用户没有可访问租户时，拒绝登录。
- 用户只有一个可访问租户时，登录后直接设置该租户。
- 用户有多个可访问租户时，当前租户为空，客户端进入租户选择阶段。
- 登录成功后 API Service 同时写入 `SEKAI_PLATFORM_AUTH` HttpOnly Cookie。

### 加载可用租户

加载当前用户有权限访问的租户列表。

租户权限条件：

- `user_tenants.status == active`

### 用户选择租户 / 切换租户

设置当前登录状态中的当前租户。

输入：

- `tenant_id`

处理规则：

- 只能选择当前用户 active 租户。
- 切换成功后重新签发 JWT，并刷新 `SEKAI_PLATFORM_AUTH` Cookie。

### 用户登出接口

清除当前登录状态。

### 获取用户登录状态

返回当前用户 ID 和当前租户 ID。

### 邀请用户接口

租户管理员邀请已有用户或新用户。

输入：

- `qq_id`：被邀请用户 QQ 号
- `role`：`normal` / `admin` / `super_admin`

响应：

- `user`：被邀请用户资料
- `membership`：当前租户成员关系
- `created_user`：是否创建了新用户
- `created_membership`：是否创建了新成员关系
- `default_password`：新用户默认密码；既有用户为 `null`

处理规则：

- 用户已存在时，添加用户租户关系。
- 用户不存在时，创建默认用户，默认密码为 QQ 号后六位，再添加用户租户关系。
- 当前租户 `admin` 或 `super_admin` 可以邀请普通用户。
- 只有 `super_admin` 可以授予 `admin` 或 `super_admin` 角色。
- 重复邀请同一用户且角色相同时不会创建重复成员关系。
- 用户已属于当前租户但角色不同的，邀请接口返回冲突错误；角色调整留给后续显式用户管理接口。

## 语言资产

### 查询剧情类型

返回平台支持的剧情类型。

### 查询剧情集列表

输入：

- 剧情类型
- 关键词
- 分页参数

### 查询剧情集详情

输入：

- 剧情集 ID

### 查询剧情列表

输入：

- 剧情集 ID
- 剧情类型
- 关键词
- 分页参数

### 查询剧情详情

输入：

- 剧情 ID

### 查询剧情原文行

输入：

- 剧情 ID

### 查询翻译版本列表

输入：

- 剧情 ID
- 分页参数

响应中的翻译版本包含：

- `metadata.staff`：版本级署名信息，可空。

约束：

- 只返回当前租户内的翻译版本。

### 查询翻译版本详情

输入：

- 翻译版本 ID

响应中的翻译版本包含：

- `metadata.staff`：版本级署名信息，可空。

约束：

- 翻译版本必须属于当前租户。

### 查询翻译行

输入：

- 翻译版本 ID

约束：

- 翻译版本必须属于当前租户。

## 搜索

### 搜索原文和译文

输入：

- 关键词
- 分页参数

请求：

```text
GET /api/search?keyword=...&page=1&page_size=20
```

参数：

| 参数 | 说明 |
|---|---|
| `keyword` | 搜索关键词，去除首尾空白后不能为空。 |
| `page` | 页码，从 1 开始，默认 1。 |
| `page_size` | 每页结果数，默认 20，范围 1 到 100，超出时返回参数错误。 |

响应：

```json
{
  "items": [
    {
      "asset_type": "source",
      "text": "こんにちは",
      "highlight_text": "<mark>こんにちは</mark>",
      "speaker": "ミク",
      "line_no": 1,
      "story_id": 301,
      "story_title": "第1話",
      "story_type": "event_story",
      "story_group_id": 201,
      "story_group_title": "テストイベント",
      "source_line_id": 401,
      "translation_line_id": null,
      "translation_version_id": null,
      "staff": null,
      "source": {
        "source_line_id": 401,
        "text": "こんにちは",
        "speaker": "ミク"
      },
      "translations": [
        {
          "translation_line_id": 601,
          "translation_version_id": 501,
          "version_no": 1,
          "translation_version_title": "历史译文",
          "staff": {
            "translator": "翻译A",
            "proofreader": "校对B",
            "approver": "合意C"
          },
          "text": "你好",
          "speaker": "初音未来"
        }
      ]
    }
  ],
  "total": 1,
  "page": 1,
  "page_size": 20
}
```

约束：

- 原文为全平台共享数据。
- 译文只搜索当前租户内的数据。
- 当前一期只对原文/译文正文进行关键词匹配；剧情、剧情集和说话人作为结果上下文返回。
- 搜索结果按行返回。
- 每条结果返回同一原文行的 `source` 和当前租户内的 `translations`。命中原文时可直接展示译文行 ID、译文内容和译文版本 `metadata.staff`；命中译文时可直接展示原文内容。
- `page` 和 `page_size` 对应的结果窗口不得超过 10000，超出时返回参数错误。
- `highlight_text` 优先使用搜索引擎命中高亮；没有高亮时回退为完整 `text`。

## 同步

### 手动同步接口

管理员手动触发外部数据源同步。

处理规则：

- 启动一次同步任务。
- 失败后不自动重试。

## 历史译文导入

### 批量导入历史译文接口

以 JSON 形式批量上传历史译文。

输入：

- `items`：导入项数组。
- `items[].story_type`：剧情类型。
- `items[].scenario_id`：scenario ID。
- `items[].title`：翻译版本标题，可空。
- `items[].metadata`：翻译版本扩展信息，可空。
- `items[].metadata.staff`：版本级署名信息，可空。
- `items[].metadata.staff.translator`：翻译人员展示名称，可空。
- `items[].metadata.staff.proofreader`：校对人员展示名称，可空。
- `items[].metadata.staff.approver`：合意人员展示名称，可空。
- `items[].lines`：译文行数组。
- `items[].lines[].line_no`：原文行号。
- `items[].lines[].text`：译文文本。
- `items[].lines[].speaker`：译文说话人，可空。
- `items[].lines[].metadata`：译文行扩展信息，可空。

处理规则：

- 写入当前租户的译文数据。
- 导入后更新 PostgreSQL。
- 导入后更新 Elasticsearch。
- 一次请求可导入多个剧情。
- 剧情通过 `story_type + scenario_id` 匹配。
- 翻译行通过 `line_no` 匹配原文行。
- 任意项校验失败时整批不写入。
