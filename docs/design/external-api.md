# 外部数据源设计文档

## 结论

一期对接 Moe Sekai / Exmeaning 公共静态数据源。平台不把 Moe Sekai 当传统业务 REST API 使用，而是同步两类资源：

- master JSON：活动、主线、卡牌、区域对话、特殊剧情等元数据。
- scenario JSON：具体剧情内容。

同步流程固定为：

1. 读取 current version。
2. 下载 master JSON。
3. 生成 `story_groups` 和 `stories`。
4. 拼接 scenario JSON URL。
5. 下载并解析 scenario JSON。
6. 重建 `story_source_lines`。
7. 写入 PostgreSQL。
8. 刷新 Elasticsearch 索引。

## Master 数据源

| 用途 | 主地址 | 备用地址 |
|---|---|---|
| JP master | `https://sekaimaster.exmeaning.com/master/{file}` | `https://sk.exmeaning.com/master/{file}` |
| CN master | `https://sekaimaster-cn.exmeaning.com/master/{file}` | `https://sk-cn.exmeaning.com/master/{file}` |
| 当前版本 | `https://sekaimaster.exmeaning.com/versions/current_version.json` | `https://sk.exmeaning.com/versions/current_version.json` |

当前同步 JP master。CN master 作为补充来源，不阻塞原文检索链路。

同步文件：

| 文件 | 用途 |
|---|---|
| `events.json` | 活动基础信息 |
| `eventStories.json` | 活动剧情章节、`scenarioId`、活动剧情 `assetbundleName` |
| `unitStories.json` | 主线剧情章节、`scenarioId`、主线章节 `assetbundleName` |
| `unitStoryEpisodeGroups.json` | 主线章节分组 |
| `cards.json` | 卡牌基础信息 |
| `cardEpisodes.json` | 卡面剧情前后篇和 `scenarioId` |
| `actionSets.json` | 区域对话、开放条件和 `scenarioId` |
| `character2ds.json` | 角色 2D ID 映射 |
| `mobCharacters.json` | mob 角色名称映射 |
| `specialStories.json` | 特殊剧情章节和 `scenarioId` |

## 资源镜像

| 区域 | Base URL |
|---|---|
| JP | `https://storage.exmeaning.com/sekai-jp-assets/` |
| CN | `https://storage.exmeaning.com/sekai-cn-assets/` |
| JP 备用 | `https://storage2.exmeaning.com/sekai-jp-assets/` |
| CN 备用 | `https://storage2.exmeaning.com/sekai-cn-assets/` |
| 海外 JP | `https://storage.pjsk.moe/sekai-jp-assets/` |
| 海外 CN | `https://storage.pjsk.moe/sekai-cn-assets/` |

默认使用 JP 主镜像，失败时尝试备用镜像。资源镜像返回 JSON。

## Scenario URL

| 剧情类型 | URL 模板 |
|---|---|
| 活动剧情 | `{base}event_story/{assetbundleName}/scenario/{scenarioId}.json` |
| 主线剧情 | `{base}scenario/unitstory/{chapterAssetbundleName}/{scenarioId}.json` |
| 卡面剧情 | `{base}character/member/{cardAssetbundleName}/{scenarioId}.json` |
| 区域对话 | `{base}scenario/actionset/group{groupId}/{scenarioId}.json` |
| 特殊剧情 | `{base}scenario/special/{assetbundleName}/{scenarioId}.json` |

区域对话：

```text
groupId = floor(actionSetId / 100)
```

区域对话分类键：

| 条件 | 分类键 |
|---|---|
| `releaseConditionId` 为 6 位且以 `1` 开头 | `event_{eventId}` |
| `actionSetType = normal`、`isNextGrade = false`、`releaseConditionId = 1` | `grade1` |
| `actionSetType = normal`、`isNextGrade = true`、`releaseConditionId = 1` | `grade2` |
| `actionSetType = limited` | `limited_{areaId}` |
| `scenarioId` 包含 `aprilfool` | `aprilfool{year}` |
| `releaseConditionId` 在剧场版条件范围内 | `theater` |

## 数据库映射

| 剧情类型 | `story_groups` | `stories` |
|---|---|---|
| 活动剧情 | 一个活动一个剧情集，`external_type = sekai_event`，`external_id = events.id` | 一话一个 story，`scenario_id = eventStoryEpisodes[].scenarioId` |
| 主线剧情 | 一个 unit 一个剧情集，`external_type = sekai_unit` | 一话一个 story，`scenario_id = episodes[].scenarioId` |
| 卡面剧情 | 一张卡一个剧情集，`external_type = sekai_card`，`external_id = cards.id` | 前篇/后篇各一个 story，`scenario_id = cardEpisodes[].scenarioId` |
| 区域对话 | 一个分类一个剧情集，`external_type = action_set_category` | 一条 actionSet 一个 story，`scenario_id = actionSets[].scenarioId` |
| 特殊剧情 | 一个特殊剧情条目一个剧情集，`external_type = special` | 一话一个 story，`scenario_id = episodes[].scenarioId` |

具体表结构见 `data-model.md`。

## Scenario JSON 解析

核心字段：

- `ScenarioId`
- `AppearCharacters`
- `Snippets`
- `TalkData`
- `SpecialEffectData`
- `SoundData`
- `FirstBackground`
- `FirstBgm`

解析规则：

| 来源 | 规则 |
|---|---|
| `Snippets[].Action == 1` | 台词，写入 `line_type = dialogue` |
| `Snippets[].Action == 6` | 特殊效果，按 `EffectType` 映射为 `scene`、`upper_scene`、`choice` |
| `TalkData[].WindowDisplayName` | 说话人来源之一 |
| `TalkCharacters[].Character2dId` | 说话人映射来源之一 |
| `TalkData[].Body` | 原文文本 |
| `SpecialEffectData[].StringVal` | 特殊效果文本 |

voice、BGM、SE、背景、角色 ID 等保留在 `story_source_lines.metadata`。

## 增强数据

以下数据不作为原文同步成功条件：

| 数据 | URL 模板 | 用途 |
|---|---|---|
| 活动剧情概要 | `https://moe.exmeaning.com/story/detail/event_{eventId padded 3}.json` | 活动简介、章节摘要和中文标题 |
| 活动剧情译文 | `https://translation.exmeaning.com/translation/eventStory/event_{eventId}.json` | JP 剧情中文译文 |

增强数据后续通过导入或展示增强链路接入。

## 风险和约束

- Exmeaning / Moe Sekai 数据源是公共静态资源，不提供本平台可控 SLA。
- master JSON 字段结构以实际响应为准。
- 单条 scenario 缺失或解析失败不应中断整个同步任务。
- 不同资源镜像的路径规则可能不完全一致。
- 译文和剧情概要不能作为原文同步成功与否的判定条件。

## 参考源码

- `/Users/zhangyao/build6/Moesekai/web/src/lib/fetch.ts`
- `/Users/zhangyao/build6/Moesekai/web/src/lib/storyAsset.ts`
- `/Users/zhangyao/build6/Moesekai/web/src/app/story/event/`
- `/Users/zhangyao/build6/Moesekai/web/src/app/story/unit/`
- `/Users/zhangyao/build6/Moesekai/web/src/app/story/card/`
- `/Users/zhangyao/build6/Moesekai/web/src/app/story/area/`
- `/Users/zhangyao/build6/SekaiText/backend/internal/service/json_loader.go`
