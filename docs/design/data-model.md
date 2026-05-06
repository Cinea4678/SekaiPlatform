# PJS 字幕组语言资产平台 数据模型设计文档

## 租户表

Table: tenants

| Field | Type | Nullable | Description |
|---|---|---:|---|
| id | BIGINT | No | 主键 |
| name | VARCHAR(64) | No | 租户名称 |
| avatar_url | VARCHAR(512) | Yes | 头像地址 |

约束：

```sql
UNIQUE(name)
```

## 用户表

Table: users

| Field | Type | Nullable | Description |
|---|---|---:|---|
| id | BIGINT | No | 主键 |
| qq_id | VARCHAR(32) | Yes | 用户的QQ号 |
| display_name | VARCHAR(128) | Yes | 用户展示名称（一般来自 OAuth 授权，也可自行设置） |
| avatar_url | VARCHAR(512) | Yes | 头像地址（一般来自 OAuth 授权，也可自行设置） |
| password_hash | VARCHAR(255) | Yes | 密码哈希，第三方登录用户可为空 |
| created_at | DATETIME | No | 创建时间 |
| updated_at | DATETIME | No | 更新时间 |

## 用户 - 租户表

Table: user_tenants

| Field | Type | Nullable | Description |
|---|---|---:|---|
| tenant_id | BIGINT | No | 租户表的主键。 |
| user_id | BIGINT | No | 用户表的主键。 |
| role | VARCHAR(32) | No | 用户角色：normal / admin / super_admin |
| status | VARCHAR(32) | No | 用户状态：active / disabled / deleted |
| created_at | DATETIME | No | 创建时间 |
| updated_at | DATETIME | No | 更新时间 |
| deleted_at | DATETIME | Yes | 软删除时间 |

### 用户角色和权限

#### 普通用户

- 可以查看所有语言资产。
- 可以新建翻译版本。
- 只能修改自己创建的翻译版本。

#### 管理员

- 可以邀请用户，包括新用户。
- 可以管理用户。
- 可以查看系统配置。
- 可以查看操作日志。
- 可以新建翻译版本。
- 只能修改自己创建的翻译版本。

#### 超级管理员

- 除管理员的权限外，还可以调整其他用户为管理员。

## OAuth - 用户表

Table: user_oauthes

| Field | Type | Nullable | Description |
|---|---|---:|---|
| user_id | BIGINT | No | 用户表的主键。 |
| oauth_type | VARCHAR(32) | No | OAuth类型。 |
| oauth_id | VARCHAR(512) | No | OAuth ID。 |

## 剧情集表

Table: story_groups

| Field | Type | Nullable | Description |
|---|---|---:|---|
| id | BIGINT | No | 主键 |
| story_type | VARCHAR(32) | No | 剧情类型：event_story / card_story / main_story / area_talk / special_story |
| external_type | VARCHAR(32) | Yes | 外部实体类型：sekai_event / sekai_card / sekai_unit / action_set / special |
| external_id | VARCHAR(128) | Yes | 外部实体 ID |
| display_no | INT | Yes | 展示编号（习惯编号） |
| title | VARCHAR(255) | No | 标题 |
| subtitle | VARCHAR(255) | Yes | 副标题 |
| metadata | JSON | Yes | 扩展信息 |
| created_at | DATETIME | No | 创建时间 |
| updated_at | DATETIME | No | 更新时间 |
| deleted_at | DATETIME | Yes | 软删除时间 |

约束：

```sql
UNIQUE NULLS NOT DISTINCT (story_type, external_type, external_id)
```

### 剧情集建模约定

`story_groups` 表示一个可导航的剧情集合，用于承载游戏侧的一级入口或分类。同步逻辑按剧情类型稳定生成 `external_type` 和 `external_id`，避免后续重新同步时产生重复集合。

| 剧情类型 | `story_groups` 映射 | `external_type` | `external_id` | 说明 |
|---|---|---|---|---|
| 活动剧情 | 一个活动一个剧情集 | `sekai_event` | `events.id` | `title` 使用活动名，`display_no` 可使用活动 ID。 |
| 主线剧情 | 一个 unit 一个剧情集 | `sekai_unit` | `unitProfiles.unit` 或 `unitStories.seq` | `title` 使用组合名，保持同一来源内唯一且稳定。 |
| 卡面剧情 | 一张卡一个剧情集 | `sekai_card` | `cards.id` | `title` 使用卡牌标题或前缀。 |
| 区域对话 | 一个区域对话分类一个剧情集 | `action_set_category` | 分类键 | 分类键包括 `event_{eventId}`、`grade1`、`grade2`、`theater`、`limited_{areaId}`、`aprilfool{year}` 等。 |
| 特殊剧情 | 一个特殊剧情条目一个剧情集 | `special` | `specialStories.id` | `title` 使用特殊剧情标题或首话标题。 |

`metadata` 保存导航和同步所需的外部补充字段，例如 assetbundleName、活动时间、活动类型、角色 ID、区域 ID、来源版本等。

## 剧情表

Table: stories

| Field | Type | Nullable | Description |
|---|---|---:|---|
| id | BIGINT | No | 主键 |
| group_id | BIGINT | Yes | 剧情集 ID |
| story_type | VARCHAR(32) | No | 剧情类型 |
| scenario_id | VARCHAR(255) | No | 游戏侧 scenarioId |
| title | VARCHAR(255) | No | 标题 |
| sort_order | INT | No | 展示排序值 |
| metadata | JSON | Yes | 扩展信息 |
| created_at | DATETIME | No | 创建时间 |
| updated_at | DATETIME | No | 更新时间 |
| deleted_at | DATETIME | Yes | 软删除时间 |

约束：

```sql
UNIQUE(story_type, scenario_id)
```

### 剧情建模约定

`stories` 表示一个具体可打开、可解析和可检索的剧情单元。通常一条 `stories` 对应一个游戏侧 scenario JSON。

| 剧情类型 | `stories` 映射 | `scenario_id` | `sort_order` | `metadata` 保留 |
|---|---|---|---:|---|
| 活动剧情 | 一话一个 story | `eventStoryEpisodes[].scenarioId` | `episodeNo` | `eventStoryId`、`episodeNo`、`episodeAssetbundleName`、`releaseConditionId`、`assetbundleName`、`eventId` |
| 主线剧情 | 一话一个 story | `unitStories.chapters[].episodes[].scenarioId` | `episodeNo` 或章节内顺序 | `unit`、`unitSeq`、`chapterAssetbundleName`、`unitStoryEpisodeGroupId`、`episodeNoLabel`、`releaseConditionId` |
| 卡面剧情 | 前篇/后篇各一个 story | `cardEpisodes[].scenarioId` | `episode_1 = 1`，`episode_2 = 2` | `cardId`、`cardEpisodePartType`、`assetbundleName` |
| 区域对话 | 一条 actionSet 对话一个 story | `actionSets[].scenarioId` | 分类内稳定顺序或 `actionSets.id` | `actionSetId`、`areaId`、`category`、`actionSetType`、`releaseConditionId`、`groupId` |
| 特殊剧情 | 一话一个 story | `specialStories.episodes[].scenarioId` | `episodeNo` | `specialStoryId`、`episodeNo`、`assetbundleName` |

同步时先 upsert `story_groups`，再按外部章节或对话明细 upsert `stories`，最后写入 `story_source_lines`。对于同一个 `story_type + scenario_id`，重新同步更新标题、排序和 metadata，并重建对应原文行。

## 剧情原文行表

Table: story_source_lines

| Field | Type | Nullable | Description |
|---|---|---:|---|
| id | BIGINT | No | 主键 |
| story_id | BIGINT | No | 剧情表的主键。 |
| line_no | INT | No | 行号，从 1 开始 |
| line_type | VARCHAR(32) | No | 行类型：dialogue / scene / upper_scene / choice / separator |
| speaker | VARCHAR(128) | Yes | 说话人 |
| text | TEXT | No | 原文文本 |
| metadata | JSON | Yes | 扩展信息 |
| created_at | DATETIME | No | 创建时间 |
| updated_at | DATETIME | No | 更新时间 |

约束：

```sql
UNIQUE(story_id, line_no)
```

### 原文行建模约定

`story_source_lines` 只保存已解析、适合检索和对照翻译的文本行。Unity scenario 原始 JSON 作为同步缓存或调试材料保存，但不直接替代本表。

映射规则：

- `dialogue`：`Snippets[].Action == Talk`，文本来自 `TalkData[].Body`，说话人来自 `WindowDisplayName` 或角色映射。
- `scene`：场景、背景或全屏文本等普通特殊效果。
- `upper_scene`：左上角地点或场景提示。
- `choice`：选项文本。
- `separator`：原始剧情中需要保留段落间隔的位置。

`metadata` 保留 voiceId、volume、character2dId、gameCharacterId、snippet index、effect type、背景/BGM/SE 资源名等信息，方便后续重建阅读体验或定位原始资源。

## 翻译版本表

Table: translation_versions

| Field | Type | Nullable | Description |
|---|---|---:|---|
| id | BIGINT | No | 主键 |
| tenant_id | BIGINT | No | 租户表的主键。 |
| story_id | BIGINT | No | 剧情表的主键。 |
| version_no | INT | No | 版本号，从 1 开始 |
| title | VARCHAR(255) | Yes | 版本标题 |
| is_published | BOOLEAN | No | 是否公开发布。公开发布后可通过开放 API 免鉴权查询，默认 false。 |
| metadata | JSON | Yes | 版本级扩展信息，包括署名人员 |
| created_by | BIGINT | No | 创建用户 ID |
| created_at | DATETIME | No | 创建时间 |
| updated_at | DATETIME | No | 更新时间 |
| deleted_at | DATETIME | Yes | 软删除时间 |

约束：

```sql
UNIQUE(tenant_id, story_id, version_no)
FOREIGN KEY(tenant_id, created_by) REFERENCES user_tenants(tenant_id, user_id)
```

### 翻译版本 metadata 约定

`translation_versions.metadata` 保存版本级扩展信息。当前固定使用 `staff` 记录署名人员：

| JSON Path | Type | Nullable | Description |
|---|---|---:|---|
| `staff` | object | Yes | 署名人员信息 |
| `staff.translator` | string | Yes | 翻译人员 |
| `staff.proofreader` | string | Yes | 校对人员 |
| `staff.approver` | string | Yes | 合意人员 |

人员字符串用于保存展示名称，不关联平台用户。`staff` 只用于展示和归档，不参与租户权限、版本可编辑性或协作工作流判断。

## 翻译行表

Table: translation_lines

| Field | Type | Nullable | Description |
|---|---|---:|---|
| id | BIGINT | No | 主键 |
| version_id | BIGINT | No | 翻译版本表的主键。 |
| source_line_id | BIGINT | No | 剧情原文行表的主键。 |
| story_id | BIGINT | No | 剧情表的主键，用于保证翻译版本和原文行属于同一剧情。 |
| line_no | INT | No | 行号，从 1 开始 |
| speaker | VARCHAR(128) | Yes | 说话人 |
| text | TEXT | No | 译文文本 |
| metadata | JSON | Yes | 扩展信息 |
| created_at | DATETIME | No | 创建时间 |
| updated_at | DATETIME | No | 更新时间 |

约束：

```sql
UNIQUE(version_id, line_no)
UNIQUE(version_id, source_line_id)
FOREIGN KEY(version_id, story_id) REFERENCES translation_versions(id, story_id)
FOREIGN KEY(source_line_id, story_id) REFERENCES story_source_lines(id, story_id)
```

## 同步任务日志表

Table: sync_jobs

| Field | Type | Nullable | Description |
|---|---|---:|---|
| id | BIGINT | No | 主键 |
| job_type | VARCHAR(64) | No | 同步任务类型，例如 source_story_sync |
| trigger_type | VARCHAR(32) | No | 触发方式：manual / scheduled |
| status | VARCHAR(32) | No | 任务状态：pending / running / succeeded / failed |
| started_at | DATETIME | Yes | 开始时间 |
| ended_at | DATETIME | Yes | 结束时间 |
| error_message | TEXT | Yes | 失败错误信息 |
| metadata | JSON | Yes | 扩展信息 |
| created_at | DATETIME | No | 创建时间 |
| updated_at | DATETIME | No | 更新时间 |
