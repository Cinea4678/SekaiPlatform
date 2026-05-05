# Phase 8 完成记录

## 状态

Phase 8：剧情详情已完成。

## 已完成

- API Service 暴露 Assets 读接口：
  - `GET /api/story-types`
  - `GET /api/story-groups`
  - `GET /api/story-groups/{storyGroupId}`
  - `GET /api/stories`
  - `GET /api/stories/{storyId}`
  - `GET /api/stories/{storyId}/source-lines`
  - `GET /api/stories/{storyId}/translation-versions`
  - `GET /api/translation-versions/{translationVersionId}`
  - `GET /api/translation-versions/{translationVersionId}/lines`
- Asset Service 实现对应内部读接口。
- 剧情类型、剧情集、剧情、原文行按共享资产读取。
- 翻译版本和翻译行按当前租户读取。
- 列表接口支持分页，`page_size` 范围为 1 到 100，结果窗口不超过 10000。
- 剧情集列表支持 `story_type`、`keyword` 过滤。
- 剧情列表支持 `story_group_id`、`story_type`、`keyword` 过滤。
- `metadata` 按 JSON 对象返回。
- Apifox 已同步 Assets 分页接口的 400 响应、分页约束和 `ExternalType` 枚举。

## 权限

- 公开 Assets 读接口要求用户已登录并已选择当前租户。
- API Service 调用 Asset Service 时使用 internal token，scope 为 `assets.read`。
- Asset Service 只接受 `api-service` 携带 `assets.read` scope 调用。
- 内部读接口要求 internal token 携带 `subject_user_id` 和 `tenant_id`。
- Asset Service 会校验当前用户仍是当前租户 active 成员。
- 跨租户翻译版本和翻译行返回 404。
- API Service 不信任客户端传入的上下文 Header。

## 验证

- `dotnet build SekaiPlatform.sln`
- `dotnet test tests/integration-tests/SekaiPlatform.IntegrationTests.csproj --filter FullyQualifiedName~AssetsApiTests`
- `dotnet test tests/integration-tests/SekaiPlatform.IntegrationTests.csproj`

## 后续

部署交付可在当前完整后端链路基础上继续：

- 完善 Docker Compose 和本地启动说明。
- 添加接口冒烟测试。
- 覆盖登录、租户选择、同步、搜索、导入和剧情详情。
