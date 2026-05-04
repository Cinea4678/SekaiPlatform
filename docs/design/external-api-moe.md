# Moe Sekai 对接设计文档

## 结论

Moe Sekai 不作为传统业务 REST API 对接。它的源码显示，剧情相关能力主要依赖两类公共静态数据：

1. Exmeaning master JSON，用于获取活动、主线、卡牌、区域对话、特殊剧情等元数据。
2. Exmeaning 资源镜像，用于按 master JSON 中的 `assetbundleName`、`scenarioId`、`actionSetId` 等字段拼接并下载 scenario JSON。

本平台对接 Moe Sekai 时实现 `moesekai` 数据源适配器：先同步 master JSON，再生成 scenario 下载任务，最后解析 scenario JSON 写入 `story_groups`、`stories`、`story_source_lines`。

参考源码目录：

- `/Users/zhangyao/build6/Moesekai/web/src/lib/fetch.ts`
- `/Users/zhangyao/build6/Moesekai/web/src/lib/storyAsset.ts`
- `/Users/zhangyao/build6/Moesekai/web/src/app/story/event/`
- `/Users/zhangyao/build6/Moesekai/web/src/app/story/unit/`
- `/Users/zhangyao/build6/Moesekai/web/src/app/story/card/`
- `/Users/zhangyao/build6/Moesekai/web/src/app/story/area/`

## Master JSON 数据源

Moe Sekai 运行时使用以下 master 数据源：

| 用途 | 主地址 | 备用地址 |
|---|---|---|
| JP master | `https://sekaimaster.exmeaning.com/master/{file}` | `https://sk.exmeaning.com/master/{file}` |
| CN master | `https://sekaimaster-cn.exmeaning.com/master/{file}` | `https://sk-cn.exmeaning.com/master/{file}` |
| 当前版本 | `https://sekaimaster.exmeaning.com/versions/current_version.json` | `https://sk.exmeaning.com/versions/current_version.json` |

当前对接范围同步 JP master。CN master 作为补充来源，不阻塞原文检索落地。

## 同步范围

对接 Moe Sekai 时同步这些 master 文件：

| 文件 | 用途 |
|---|---|
| `events.json` | 活动基础信息，生成活动剧情 group 标题和活动 metadata。 |
| `eventStories.json` | 活动剧情章节、`scenarioId`、活动剧情 `assetbundleName`。 |
| `unitStories.json` | 主线剧情章节、`scenarioId`、主线章节 `assetbundleName`。 |
| `unitStoryEpisodeGroups.json` | 主线章节分组和大段标题/简介。 |
| `cards.json` | 卡牌基础信息，生成卡面剧情 group 标题和卡牌 assetbundleName。 |
| `cardEpisodes.json` | 卡面剧情前后篇、`scenarioId`、篇章类型。 |
| `actionSets.json` | 区域对话 `scenarioId`、分类推导和 actionSet 分组。 |
| `character2ds.json` | scenario 中 `character2dId` 到游戏角色或 mob 的映射。 |
| `mobCharacters.json` | mob 角色名称映射。 |
| `specialStories.json` | 特殊剧情条目、章节和 `scenarioId`。 |

同步流程下载并解析 event / unit / card / area / special 对应的 scenario JSON。

## 资源镜像

Moe Sekai 使用的主要资源镜像：

| 区域 | Base URL |
|---|---|
| JP | `https://storage.exmeaning.com/sekai-jp-assets/` |
| CN | `https://storage.exmeaning.com/sekai-cn-assets/` |
| JP 备用 | `https://storage2.exmeaning.com/sekai-jp-assets/` |
| CN 备用 | `https://storage2.exmeaning.com/sekai-cn-assets/` |
| 海外 JP | `https://storage.pjsk.moe/sekai-jp-assets/` |
| 海外 CN | `https://storage.pjsk.moe/sekai-cn-assets/` |

默认使用 JP 主镜像，失败时尝试备用镜像。资源镜像返回的是直接 JSON，不需要按 `.asset` 解析。

## Scenario URL 生成规则

### 活动剧情

输入：

- `eventStories.assetbundleName`
- `eventStoryEpisodes[].scenarioId`

URL：

```text
{base}event_story/{assetbundleName}/scenario/{scenarioId}.json
```

数据库映射：

- `story_groups.story_type = event_story`
- `story_groups.external_type = sekai_event`
- `story_groups.external_id = events.id`
- `stories.scenario_id = eventStoryEpisodes[].scenarioId`
- `stories.sort_order = eventStoryEpisodes[].episodeNo`

### 主线剧情

输入：

- `unitStories.seq` 或 `unitStories.unit`
- `unitStories.chapters[].assetbundleName`
- `unitStories.chapters[].episodes[].scenarioId`

URL：

```text
{base}scenario/unitstory/{chapterAssetbundleName}/{scenarioId}.json
```

数据库映射：

- `story_groups.story_type = main_story`
- `story_groups.external_type = sekai_unit`
- `story_groups.external_id = unitProfiles.unit` 或 `unitStories.seq`
- `stories.scenario_id = episodes[].scenarioId`
- `stories.sort_order = episodes[].episodeNo`

### 卡面剧情

输入：

- `cards.id`
- `cards.assetbundleName`
- `cardEpisodes[].scenarioId`
- `cardEpisodes[].cardEpisodePartType`

URL：

```text
{base}character/member/{cardAssetbundleName}/{scenarioId}.json
```

数据库映射：

- `story_groups.story_type = card_story`
- `story_groups.external_type = sekai_card`
- `story_groups.external_id = cards.id`
- `stories.scenario_id = cardEpisodes[].scenarioId`
- `stories.sort_order = 1` for `episode_1`，`2` for `episode_2`

### 区域对话

输入：

- `actionSets[].id`
- `actionSets[].scenarioId`
- `actionSets[].releaseConditionId`
- `actionSets[].actionSetType`
- `actionSets[].isNextGrade`
- `actionSets[].areaId`

URL：

```text
{base}scenario/actionset/group{groupId}/{scenarioId}.json
```

其中：

```text
groupId = floor(actionSetId / 100)
```

分类键推导：

| 条件 | 分类键 |
|---|---|
| `releaseConditionId` 为 6 位且以 `1` 开头 | `event_{eventId}`，其中 `eventId = int(releaseConditionId[1:4]) + 1` |
| `actionSetType = normal` 且 `isNextGrade = false` 且 `releaseConditionId = 1` | `grade1` |
| `actionSetType = normal` 且 `isNextGrade = true` 且 `releaseConditionId = 1` | `grade2` |
| `actionSetType = limited` | `limited_{areaId}` |
| `scenarioId` 包含 `aprilfool` | `aprilfool{year}` |
| `releaseConditionId` 在剧场版条件范围内 | `theater` |

数据库映射：

- `story_groups.story_type = area_talk`
- `story_groups.external_type = action_set_category`
- `story_groups.external_id = 分类键`
- `stories.scenario_id = actionSets[].scenarioId`
- `stories.sort_order = actionSets[].id`

### 特殊剧情

输入：

- `specialStories.id`
- `specialStories.episodes[].assetbundleName`
- `specialStories.episodes[].scenarioId`

URL：

```text
{base}scenario/special/{assetbundleName}/{scenarioId}.json
```

数据库映射：

- `story_groups.story_type = special_story`
- `story_groups.external_type = special`
- `story_groups.external_id = specialStories.id`
- `stories.scenario_id = episodes[].scenarioId`
- `stories.sort_order = episodes[].episodeNo`

## Scenario JSON 解析

Moe Sekai 读取的 scenario JSON 与现有 `external-api.md` 中描述的 Unity scenario 结构一致，核心字段包括：

- `ScenarioId`
- `AppearCharacters`
- `Snippets`
- `TalkData`
- `SpecialEffectData`
- `SoundData`
- `FirstBackground`
- `FirstBgm`

用于检索的解析规则：

- `Snippets[].Action == 1`：台词，写入 `line_type = dialogue`。
- `Snippets[].Action == 6`：特殊效果，根据 `EffectType` 映射为 `scene`、`upper_scene`、`choice` 等。
- `TalkData[].WindowDisplayName` 或 `TalkCharacters[].Character2dId` 用于解析说话人。
- `TalkData[].Body` 和特殊效果文本写入 `story_source_lines.text`。
- voice、BGM、SE、背景、角色 ID 等保留在 `story_source_lines.metadata`。

## 后续补充数据

Moe Sekai 还有两类增强数据，但不作为原文同步的必要依赖：

| 数据 | URL 模板 | 用途 |
|---|---|---|
| 活动剧情概要 | `https://moe.exmeaning.com/story/detail/event_{eventId padded 3}.json` | 展示活动简介、章节摘要和中文标题。 |
| 活动剧情译文 | `https://translation.exmeaning.com/translation/eventStory/event_{eventId}.json` | 展示 JP 剧情的中文译文。 |

这些数据在后续作为导入或展示增强来源接入。当前对接范围保证原文剧情同步、解析和搜索。

## 同步流程

1. 读取 current version，记录本次同步的 master / asset 版本。
2. 下载同步范围内的 master JSON。
3. 根据 master JSON 生成 `story_groups` 和 `stories`。
4. 根据 `story_type` 和 metadata 生成 scenario JSON URL。
5. 下载 scenario JSON，失败时记录单条失败，不中断整个同步任务。
6. 解析 scenario JSON，重建对应 `story_source_lines`。
7. 写入 PostgreSQL 后更新 Elasticsearch 索引。
8. 在 `sync_jobs.metadata` 中记录数据源、版本、成功数量、失败数量和失败样例。

## 风险和约束

- Exmeaning / Moe Sekai 数据源是公共静态资源，不提供本平台可控 SLA。
- master JSON 字段结构以实际响应为准，同步代码容忍字段缺失和单条剧情资源缺失。
- 不同资源镜像的路径规则可能不完全一致，不能只替换域名后假设所有路径都存在。
- 译文和剧情概要属于增强数据，不能作为原文同步成功与否的判定条件。
