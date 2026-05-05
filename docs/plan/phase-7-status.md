# Phase 7 完成记录

## 状态

Phase 7：历史译文批量导入已完成。

## 已完成

- API Service 暴露 `POST /api/import/translation-versions`。
- Asset Service 实现内部导入接口 `POST /internal/import/translation-versions`。
- 导入请求只接受 JSON。
- 一次请求可导入多个剧情的译文版本。
- 剧情通过 `story_type + scenario_id` 匹配。
- 翻译行通过 `line_no` 匹配原文行。
- 同一租户同一剧情每次导入创建新的 `translation_versions` 版本号。
- 整批导入使用事务；任意项校验失败时整批不写入。
- 导入成功后刷新对应翻译版本的 translation 搜索索引。

## 权限

- 公开导入接口要求用户已登录并已选择当前租户。
- Asset Service 继续校验当前用户是当前租户 active `admin` 或 `super_admin`。
- API Service 调用 Asset Service 时使用 internal token，scope 为 `translations.import.write`。
- Asset Service 调用 Search Service 刷新索引时使用 `search.index.rebuild`。

## 验证

- `dotnet build SekaiPlatform.sln`
- `dotnet test tests/integration-tests/SekaiPlatform.IntegrationTests.csproj`

## 后续

Phase 8 剧情详情可在当前数据基础上继续：

- 查询剧情详情。
- 查询原文行。
- 查询当前租户翻译版本列表。
- 查询当前租户翻译行。
