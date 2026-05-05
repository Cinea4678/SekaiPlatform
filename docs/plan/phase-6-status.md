# Phase 6 完成记录

## 状态

Phase 6：搜索 API 已完成。

## 已完成内容

- API Service 实现公开搜索入口：
  - `GET /api/search`
  - 支持 `keyword`、`page`、`page_size` 查询参数。
  - 要求用户已登录并已选择当前租户。
- API Service 代理 Search Service 时签发内部 token：
  - audience 为 `search-service`。
  - scope 为 `search.query`。
  - 携带当前 `subject_user_id` 和 `tenant_id`。
- Search Service 实现内部查询接口：
  - `GET /internal/search`
  - 仅允许 `api-service` 使用 `search.query` scope 调用。
  - 必须携带用户和租户上下文。
- Search Service 基于 Phase 5 统一索引查询：
  - 同时搜索共享原文和当前租户译文。
  - 原文通过 `asset_type = source` 查询，不绑定租户。
  - 译文通过 `asset_type = translation` 和当前 `tenant_id` 过滤。
  - 一期查询字段覆盖正文 `text`、`text.zh` 和 `text.folded`；说话人、剧情标题和剧情集标题作为结果上下文返回。
- 搜索响应使用分页对象：
  - `items`
  - `total`
  - `page`
  - `page_size`
- 分页约束：
  - `page` 从 1 开始。
  - `page_size` 范围为 1 到 100。
  - `from + page_size` 不得超过 10000，超出时返回参数错误。
- 搜索结果按行返回剧情、剧情集、剧情类型、翻译版本、说话人、行号、完整文本和高亮文本。

## 安全说明

- `/api/search` 不接受客户端传入的租户 ID；当前租户只来自登录态。
- `/internal/search` 不接受外部用户 token、上下文 Header 或维护 token 作为授权依据。
- Search Service 会在本服务内校验 `subject_user_id` 仍是当前 `tenant_id` 的 active 成员，避免旧 token 在成员关系变更后继续访问租户译文。
- Search Service 查询译文时必须带当前 `tenant_id` 过滤，避免跨租户返回译文。

## 后续衔接

Phase 7 历史译文批量导入完成后，导入接口写入译文并刷新 translation 索引，导入结果即可通过当前 `/api/search` 在租户内检索。
