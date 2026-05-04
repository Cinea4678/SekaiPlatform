# Phase 4 完成记录

## 状态

Phase 4：外部数据源同步已完成。

## 已完成内容

- 新增 `SekaiPlatform.SourceSync` 包，封装 Moe Sekai / Exmeaning 数据源同步能力：
  - 下载 current version 和阶段四范围内的 master JSON。
  - 生成 scenario 资源下载路径。
  - 下载 Unity scenario JSON。
  - 解析 scenario JSON 为平台原文行。
  - 将 master metadata 和解析结果写入 `story_groups`、`stories`、`story_source_lines`。
- 同步范围覆盖阶段四要求的五类剧情：
  - 活动剧情。
  - 主线剧情。
  - 卡面剧情。
  - 区域对话。
  - 特殊剧情。
- catalog builder 已按职责拆分：
  - `EventStoryCatalogBuilder`
  - `UnitStoryCatalogBuilder`
  - `CardStoryCatalogBuilder`
  - `AreaTalkCatalogBuilder`
  - `SpecialStoryCatalogBuilder`
- Asset Service 实现内部同步接口：
  - `POST /internal/sync/jobs`
  - `GET /internal/sync/jobs`
  - `GET /internal/sync/jobs/{syncJobId}`
- API Service 实现外部 Phase 4 同步代理接口：
  - `POST /api/sync/jobs`
  - `GET /api/sync/jobs`
  - `GET /api/sync/jobs/{syncJobId}`
- Sync Worker 实现每天一次自动同步，默认本地时间 `04:00`。
- 同步任务写入 `sync_jobs`：
  - 记录任务类型、触发类型、状态、开始时间、结束时间和错误信息。
  - 成功任务在 metadata 中记录同步统计、版本信息和部分失败样本。
  - 失败任务记录错误信息。
- 同步写入策略：
  - 先 upsert `story_groups`。
  - 再按 `story_type + scenario_id` upsert `stories`。
  - 每次同步重建对应剧情的 `story_source_lines`。
- 并发控制使用 PostgreSQL advisory lock，同一时间只允许一个原文同步任务运行。
- 部分 scenario 下载或解析失败时，记录失败样本并继续处理其他剧情；如果没有任何 scenario 成功同步，则任务标记为失败。
- 同步失败不自动重试，后续由管理员手动触发或等待下一次定时任务。

## 安全说明

- 手动同步要求用户已登录并已选择当前租户，API 使用 `sekai.tenant_selected` 策略。
- Asset Service 在业务逻辑中二次校验当前用户必须是当前租户的 `admin` 或 `super_admin`。
- API Service 只转发登录态中的 bearer token 或认证 Cookie，不信任客户端直接传入的租户上下文 Header。
- Moe Sekai URL 配置默认只允许 HTTPS 和固定 host allowlist；`AllowInsecureHttp` 仅用于本地或明确可信环境。
- scenario 路径片段会经过校验和转义，避免 master 数据中的异常字段拼出任意 URL 路径。

## 配置说明

- 配置节为 `MoeSekai`。
- 主要配置项：
  - `MasterBaseUrls`：master JSON 基础地址，按顺序尝试。
  - `VersionUrls`：current version 文档地址，按顺序尝试。
  - `AssetBaseUrls`：scenario 资源基础地址，按顺序尝试。
  - `AllowedHosts`：允许访问的上游 host。
  - `AllowInsecureHttp`：是否允许 HTTP 上游地址。
  - `RequestTimeout`：单次上游请求超时。
  - `FailureSampleLimit`：写入 metadata 的失败样本上限。
  - `ScheduledLocalTime`：Sync Worker 每日自动同步时间，默认 `04:00`。

## 验证结果

已验证通过：

- `dotnet build SekaiPlatform.sln --no-restore -v minimal -nr:false /p:UseSharedCompilation=false`
- `dotnet test tests/integration-tests/SekaiPlatform.IntegrationTests.csproj --no-build -v minimal -nr:false`，19 个测试通过

集成测试覆盖：

- 管理员可以通过 API Service 触发一次实际同步流程。
- 同步后数据库存在剧情集、剧情、原文行和成功任务日志。
- 普通用户触发手动同步会被拒绝。
- 单条 scenario 失败时任务保持成功并记录 metadata。
- 全部 scenario 失败时任务标记失败。
- 手动同步请求 JSON 格式错误时返回统一错误响应。
- scenario URL 生成、区域对话分类和 Unity scenario 解析规则。

## 后续衔接

Phase 5 搜索索引可在当前落库结果上继续：

- 监听或主动扫描 `story_source_lines`。
- 将原文行、剧情、剧情集和同步 metadata 中的必要字段写入 Elasticsearch。
- 继续沿用 `story_type + scenario_id + line_no` 作为原文行的稳定定位信息。
