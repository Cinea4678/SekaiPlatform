# Phase 5 完成记录

## 状态

Phase 5：搜索索引已完成。

## 已完成内容

- 新增 Elasticsearch 自定义镜像，安装 `analysis-smartcn`、`analysis-kuromoji` 和 `analysis-icu`。
- Search Service 实现统一索引 `sekai-language-assets-v1` 的创建和 mapping 初始化。
- 统一索引覆盖原文和译文两类文档：
  - 原文文档 `asset_type = source`，不绑定 `tenant_id`。
  - 译文文档 `asset_type = translation`，必须绑定 `tenant_id`。
- 索引字段包含资产类型、租户、剧情、剧情集、翻译版本、说话人、行号和文本。
- 文本字段提供日文、中文和 folded 多字段分析，用于后续关键词搜索。
- Search Service 实现内部索引重建接口：
  - `POST /internal/search/index/rebuild`
  - 支持 `scope: all | source | translation`
  - 支持按 `story_ids`、`tenant_id`、`translation_version_id` 局部重建
- 原文同步成功后，Asset Service 手动同步和 Sync Worker 定时同步都会请求 Search Service 对成功同步的 story 做 all-scope 局部重建，以同步刷新原文和已有译文文档中的剧情元信息。

## 安全说明

- 索引重建接口仅作为内部服务间维护接口使用，不在 API Service 暴露给前端；调用方必须携带 `X-Sekai-Maintenance-Token`。
- Docker Compose 中 Elasticsearch 默认只绑定宿主机 `127.0.0.1:9200`，避免本地开发环境把无认证 ES 直接暴露到局域网。
- 原文索引刷新失败不会回滚 PostgreSQL 同步结果，服务会记录错误日志，后续可通过重建接口修复。
- 译文索引能力已可从数据库重建；Phase 7 导入接口完成后复用 translation 局部重建能力。

## 后续衔接

Phase 6 搜索 API 可在当前索引基础上继续：

- API Service 新增 `/api/search` 代理入口。
- Search Service 实现关键词查询、分页和租户过滤。
- 查询条件同时覆盖共享原文和当前租户译文。
- 搜索结果按行返回剧情、剧情集、剧情类型、翻译版本、说话人、行号和命中文本。
