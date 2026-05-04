# 外部数据源设计文档

## 结论

阶段四优先对接 Moe Sekai。Moe Sekai 对接方案使用 Exmeaning master JSON 和资源镜像，具体 URL、字段映射和同步流程见 @external-api-moe.md。

SekaiText 作为补充参考，用于确认 Unity scenario JSON 的解析方式、剧情类型映射和其他资源源站差异。本平台一期所谓“外部 API”主要不是传统业务 REST API，而是两类公共静态数据源：

1. master JSON，用于同步剧情、卡牌、活动、区域对话等元数据。
2. 游戏资源镜像，用于按元数据拼接并下载具体剧情 scenario JSON。

平台一期同步逻辑固定为：先同步 master metadata，再根据剧情类型、索引和章节生成 scenario 资源 URL，下载 Unity 剧情 JSON，解析为平台内部的原文行。

## SekaiText 参考文件

- `/Users/zhangyao/build6/SekaiText/backend/internal/service/update.go`
  - 定义 master JSON 数据源。
  - 下载并转换 `events.json`、`cards.json`、`mainStory.json`、`areatalks.json`、`specials.json` 等本地 catalog 文件。
- `/Users/zhangyao/build6/SekaiText/backend/internal/service/list_manager.go`
  - 定义 CDN base URL。
  - 根据剧情类型、索引、章节和 source 生成 scenario JSON URL。
- `/Users/zhangyao/build6/SekaiText/backend/internal/service/downloader.go`
  - 下载 scenario JSON，并支持本地缓存。
- `/Users/zhangyao/build6/SekaiText/backend/internal/service/json_loader.go`
  - 解析 Unity scenario JSON，转换为 `SourceTalk[]`。
- `/Users/zhangyao/build6/SekaiText/backend/internal/api/handlers.go`
  - 暴露更新、下载、加载和语音 URL 相关接口。

## Master JSON 数据源

`SekaiText` 使用以下 URL 模板下载 master 数据：

```text
https://sekai-master-direct.haruki.seiunx.com/haruki-sekai-master/master/{table}.json
```

其中 `{table}` 由同步逻辑传入。当前确认使用的表包括：

| table | 用途 | 生成的平台 catalog |
|---|---|---|
| `events` | 活动基础信息 | `events.json` |
| `eventStories` | 活动剧情章节和 scenarioId | `events.json` |
| `eventCards` | 活动关联卡牌 | `events.json` |
| `cards` | 卡牌基础信息 | `cards.json` |
| `unitStories` | 主线剧情章节和 scenarioId | `mainStory.json` |
| `actionSets` | 区域对话 scenarioId 和开放条件 | `areatalks.json` |
| `character2ds` | 区域对话角色映射 | `areatalks.json` |
| `specialStories` | 特殊剧情 | `specials.json` |
| `systemLive2ds` | 主界面语音 | `greets.json`，当前 SekaiText 未完整实现 |

响应格式兼容两种：

- 直接返回 JSON 数组。
- 返回 `{ "data": [...] }` 包裹结构。

同步结果在 `SekaiText` 中会落到 `resources/catalog/` 下，作为后续剧情导航和 URL 拼接的本地索引。

## CDN 数据源

`SekaiText` 中确认的 CDN base URL：

| source | base URL | 说明 |
|---|---|---|
| `sekai.best` | `https://storage.sekai.best/sekai-jp-assets/` | 默认推荐来源之一，资源后缀通常为 `.asset`。 |
| `unipjsk` | `https://assets.unipjsk.com/` | 资源路径通常带 `startapp/` 前缀，后缀为 `.json`。 |
| `haruki` | `https://bot-assets.haruki.seiunx.com/assets/` | 资源路径通常带 `startapp/` 前缀，后缀为 `.json`。 |
| `harukiJP` | `https://sekai-assets-bdf29c81.seiunx.net/jp-assets/` | 在 `SekaiText` 的 base URL 表中存在，但当前 `GetJsonPath` 未实际使用。 |

`SekaiText` 默认设置中的下载源为 `best`，实际请求时前端使用的 source 值可能是 `sekai.best`、`unipjsk` 或 `haruki`。

## Scenario URL 生成规则

Scenario URL 由剧情类型、catalog 元数据、章节编号和 source 共同决定。

### 主线剧情

`sekai.best`：

```text
{base}scenario/unitstory/{unitAssetName}/{scenarioId}.asset
```

其他 source：

```text
{base}startapp/scenario/unitstory/{unitAssetName}/{scenarioId}.json
```

### 活动剧情

`sekai.best`：

```text
{base}event_story/{eventAssetName}/scenario/{scenarioId}.asset
```

其他 source：

```text
{base}ondemand/event_story/{eventAssetName}/scenario/{scenarioId}.json
```

### 卡面剧情

`sekai.best`：

```text
{base}character/member/res{characterId3}_no{cardNo}/{characterId3}{cardNo}_{characterName}{chapter}.asset
```

其他 source：

```text
{base}startapp/character/member/res{characterId3}_no{cardNo}/{characterId3}{cardNo}_{characterName}{chapter}.json
```

### 区域对话

`sekai.best`：

```text
{base}scenario/actionset/group{groupId}/{scenarioId}.asset
```

其他 source：

```text
{base}startapp/scenario/actionset/group{groupId}/{scenarioId}.json
```

其中 `groupId = actionSetId / 100`。

### 特殊剧情

`sekai.best`：

```text
{base}scenario/special/{dirName}/{fileName}.asset
```

其他 source：

```text
{base}startapp/scenario/special/{dirName}/{fileName}.json
```

## Scenario JSON 结构

`SekaiText` 将下载到的 Unity scenario JSON 解析为以下结构：

```json
{
  "ScenarioId": "string",
  "Snippets": [
    {
      "Action": 1,
      "ReferenceIndex": 0
    }
  ],
  "TalkData": [
    {
      "WindowDisplayName": "string",
      "Body": "string",
      "Voices": [
        {
          "VoiceId": "string",
          "Volume": 100
        }
      ],
      "WhenFinishCloseWindow": 0
    }
  ],
  "SpecialEffectData": [
    {
      "EffectType": 8,
      "StringVal": "string"
    }
  ]
}
```

解析规则：

- `Snippets[].Action == 1` 表示台词，使用 `TalkData[ReferenceIndex]`。
- `Snippets[].Action == 6` 表示特殊效果，使用 `SpecialEffectData[ReferenceIndex]`。
- `SpecialEffectData.EffectType == 8` 映射为 `场景`。
- `SpecialEffectData.EffectType == 18` 映射为 `左上场景`。
- `SpecialEffectData.EffectType == 23` 映射为 `选项`。
- `WindowDisplayName` 会按 `_` 截断，只保留前半部分作为说话人。
- `WhenFinishCloseWindow != 0` 时追加空行分隔。
- 解析完成后移除尾部空行。

平台将解析结果映射为 `story_source_lines`：

| 字段 | 来源 |
|---|---|
| `scenario_id` | `ScenarioId` |
| `line_no` | 解析后的行序号 |
| `line_type` | 根据台词或特殊效果推导 |
| `speaker` | `WindowDisplayName` 或特殊效果类型 |
| `text` | `Body` 或 `StringVal` |
| `metadata` | voices、volume、charIndex、flashback clues 等扩展信息 |

## 语音资源

语音 URL 由 `scenarioId` 和 `voiceId` 拼接：

`sekai.best`：

```text
https://storage.sekai.best/sekai-jp-assets/voice/{scenarioId}/{voiceId}.mp3
```

`unipjsk`：

```text
https://assets.unipjsk.com/voice/{scenarioId}/{voiceId}.mp3
```

一期平台以语言资产检索为目标，语音资源作为 metadata 保留，不作为必须下载的资产。

## 阶段四实现指令

1. Sync Worker 同步 master JSON，保存原始响应和转换后的导航数据。
2. Asset Service 或 Sync Worker 根据导航数据生成 scenario 下载任务。
3. 下载 scenario JSON 后解析为 `story_groups`、`stories`、`story_source_lines`。
4. 原始 scenario JSON 作为调试和重建索引的缓存保存。
5. 写入 PostgreSQL 后，同步更新 Elasticsearch 统一索引。

### 剧情层级映射

本平台统一使用 `story_groups -> stories -> story_source_lines` 保存游戏剧情：

| 层级 | 含义 | 示例 |
|---|---|---|
| `story_groups` | 可导航的剧情集合 | 一个活动、一个 unit、一张卡、一个区域对话分类、一个特殊剧情条目。 |
| `stories` | 具体可打开的一话或一条对话 | 活动第 1 话、主线某一话、卡面前篇、某条区域对话。 |
| `story_source_lines` | 解析后的原文行 | 台词、场景提示、选项、分隔行。 |

具体映射规则见 @data-model.md。

### 阶段四同步范围

阶段四同步以下 master JSON：

| 文件 | 用途 |
|---|---|
| `events.json` | 活动基础信息。 |
| `eventStories.json` | 活动剧情章节和 `scenarioId`。 |
| `unitStories.json` | 主线剧情章节和 `scenarioId`。 |
| `unitStoryEpisodeGroups.json` | 主线剧情章节分组。 |
| `cards.json` | 卡牌基础信息。 |
| `cardEpisodes.json` | 卡面剧情前后篇和 `scenarioId`。 |
| `actionSets.json` | 区域对话、开放条件和 `scenarioId`。 |
| `character2ds.json` | 角色 2D ID 映射。 |
| `mobCharacters.json` | mob 角色名称映射。 |
| `specialStories.json` | 特殊剧情章节和 `scenarioId`。 |

阶段四下载并解析 event / unit / card / area / special 对应的 scenario JSON。语音、BGM、背景、活动概要和社区译文先作为 metadata 或后续增强能力处理。

需要注意：

- 外部数据源是公共静态资源，不保证稳定 SLA。
- 同步逻辑需要支持超时、失败重试和部分失败记录。
- master JSON 可能是数组，也可能是 `{ data: ... }` 包裹结构。
- 不同 source 的路径前缀和文件后缀不同，不能只替换 base URL。
- 当前 `SekaiText` 中 `greets` 的同步逻辑未完整实现，本平台一期不将主界面语音作为核心同步范围。
