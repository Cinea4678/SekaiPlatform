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

## 翻译版本表

Table: translation_versions

| Field | Type | Nullable | Description |
|---|---|---:|---|
| id | BIGINT | No | 主键 |
| tenant_id | BIGINT | No | 租户表的主键。 |
| story_id | BIGINT | No | 剧情表的主键。 |
| version_no | INT | No | 版本号，从 1 开始 |
| title | VARCHAR(255) | Yes | 版本标题 |
| created_by | BIGINT | No | 创建用户 ID |
| created_at | DATETIME | No | 创建时间 |
| updated_at | DATETIME | No | 更新时间 |
| deleted_at | DATETIME | Yes | 软删除时间 |

约束：

```sql
UNIQUE(tenant_id, story_id, version_no)
FOREIGN KEY(tenant_id, created_by) REFERENCES user_tenants(tenant_id, user_id)
```

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
