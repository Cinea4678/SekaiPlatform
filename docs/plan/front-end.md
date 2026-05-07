# 前端实现计划

## 定位

前端用于把一期后端能力组织成可日常使用的语言资产工作台。首屏以搜索和资产导航为核心，服务字幕组成员查找原文、查看历史译文、导入译文版本，以及让管理员完成用户邀请、原文同步和搜索索引维护。

本计划聚焦功能模块和交付顺序。正式 API 合同以 Apifox 项目 `8210187` 为准，仓内 `docs/design/interface.md` 只作为语义概览。

## 范围

一期前端实现：

- 登录、会话恢复、租户选择、租户切换和登出。
- 当前用户与当前租户信息展示。
- 剧情类型、剧情集、剧情列表和剧情详情浏览。
- 原文行、翻译版本和翻译行对照查看。
- 原文和当前租户译文的统一搜索。
- 历史译文 JSON 导入。
- 租户用户邀请。
- 外部原文同步任务触发和同步任务查看。
- 搜索索引重建触发。

暂不实现：

- TXT 导入和导出。
- 实时协同编辑。
- 翻译、校对、合意工作流。
- AI 翻译。
- 自动发布。
- 面向开放 API 的合作伙伴控制台。
- 独立移动端适配。

## 功能模块

### 1. 前端基础设施

职责：

- 基于仓库现有 `web/` 前端模板改造业务应用，不另起 `apps/web-frontend/`。
- 沿用模板的 Vue 3、Vite、TypeScript、Vue Router、Tailwind CSS、shadcn-vue 风格组件和 `lucide-vue-next` 图标体系。
- 保留模板中已可用的应用入口、路由、主布局、侧边栏、顶部栏和基础 UI 组件，替换 CRM 示例页面和示例导航。
- 统一 API Base URL 配置，开发环境默认指向 `http://localhost:8080`。
- 统一处理 `{ "msg": "...", "trace_id": "..." }` 错误响应，页面错误态必须展示 `msg`，调试区域或错误详情可展示 `trace_id`。
- 统一分页、空状态、加载态、失败重试、表单校验和权限隐藏策略。
- 统一 snake_case API 字段到前端类型的映射，不在业务组件里散落字段转换。

关键约束：

- 业务请求使用后端登录态中的当前租户，不在前端请求中传入 `tenant_id` 作为权限依据。
- 前端只调用 `/api/...` 外部接口，不调用 `/internal/...`。
- 登录后端会写入 `SEKAI_PLATFORM_AUTH` HttpOnly Cookie；前端可以在运行时内存中使用 `access_token` 发送 `Authorization: Bearer`，但不能依赖本地持久化 token 作为唯一登录状态来源。
- 搜索高亮来自后端 `highlight_text`，只允许渲染后端约定的 `<mark>` 标签，其余内容按文本处理，避免把搜索结果当任意 HTML 注入。

模板改造目录：

```text
web/
  src/api/                 # API client、请求/响应类型、错误解析
  src/components/ui/       # 沿用并扩展 shadcn-vue 风格基础组件
  src/components/          # 业务无关复用组件
  src/layouts/             # 改造 MainLayout、导航、用户菜单、权限入口
  src/router/              # 替换模板示例路由，增加登录守卫
  src/views/               # 页面级组件
  src/views/auth/
  src/views/assets/
  src/views/search/
  src/views/import/
  src/views/admin/
  src/lib/                 # 工具函数、格式化、权限判断、类型转换
```

模板文件处理：

- `web/src/views/Dashboard.vue` 等 CRM 示例页按业务模块逐步替换，不保留无关示例功能。
- `web/src/layouts/MainLayout.vue` 的导航项替换为搜索、资产、导入和管理入口。
- `web/src/router/index.ts` 的 base path 改为平台部署路径，不继续使用模板默认 `/material-dashboard-shadcn-vue/`。
- `web/README.md`、`web/INSTALLATION.md` 中的模板说明在前端实现完成后改为平台前端说明。
- `web/dist/` 作为构建产物处理，不作为业务源码维护入口。

工程命令：

- 开发：在 `web/` 下执行 `pnpm dev`，模板默认监听 `5000` 端口。
- 构建：在 `web/` 下执行 `pnpm build`。
- 静态检查：在 `web/` 下执行 `pnpm lint`。

### 2. 登录和租户模块

页面与入口：

- `/login`：用户名密码登录。
- `/tenant/select`：多租户用户选择当前租户。
- 顶部用户菜单：展示用户、当前租户、角色、租户切换和登出。

接口：

- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/auth/session`
- `GET /api/auth/tenants`
- `PUT /api/auth/current-tenant`

实现要点：

- 应用启动先请求 `GET /api/auth/session`，成功后进入业务页面，失败后进入登录页。
- 登录响应没有 `current_tenant` 时进入租户选择页。
- 切换租户成功后刷新当前会话、清理租户相关查询缓存，并回到搜索或资产首页。
- 角色值按 `normal`、`admin`、`super_admin` 处理，权限显示由前端根据当前租户成员角色控制，后端仍是最终权限边界。

### 3. 全局导航和工作台首页

页面与入口：

- `/`：工作台首页。
- 顶部或侧边导航：搜索、资产、导入、管理。

实现要点：

- 首页放置搜索输入、常用入口和最近使用的资产导航，不做营销式落地页。
- 导航项根据角色显示：普通成员可见搜索、资产、导入；管理员可见用户邀请和同步任务；超级管理员可见搜索索引重建。
- 页面 URL 应保留核心筛选条件，刷新后保持当前搜索词、剧情类型、分页和所选剧情集。

### 4. 资产目录模块

页面与入口：

- `/assets`：剧情类型和资产入口。
- `/assets/groups`：剧情集列表。
- `/assets/groups/:storyGroupId`：剧情集详情和该剧情集下的剧情列表。
- `/stories`：全局剧情列表。

接口：

- `GET /api/story-types`
- `GET /api/story-groups`
- `GET /api/story-groups/{storyGroupId}`
- `GET /api/stories`
- `GET /api/stories/{storyId}`

实现要点：

- 剧情类型使用后端返回的 `value` 和 `label`，不要在前端硬编码全部展示文案。
- 剧情集列表支持 `story_type`、`keyword`、分页。
- 剧情列表支持 `story_group_id`、`story_type`、`keyword`、分页。
- 列表项显示后端已返回的剧情类型、标题、副标题、编号、scenario_id 和更新时间；剧情集下的剧情通过 `story_group_id` 过滤后的剧情列表读取。
- 面包屑按剧情类型、剧情集、剧情详情组织，保证从搜索结果跳入详情后仍能回到上下文。

### 5. 剧情详情和译文阅读模块

页面与入口：

- `/stories/:storyId`：剧情详情。
- `/stories/:storyId?line=:lineNo`：从搜索结果定位到指定行。
- `/translations/:translationVersionId`：翻译版本详情。

接口：

- `GET /api/stories/{storyId}`
- `GET /api/stories/{storyId}/source-lines`
- `GET /api/stories/{storyId}/translation-versions`
- `GET /api/translation-versions/{translationVersionId}`
- `GET /api/translation-versions/{translationVersionId}/lines`

实现要点：

- 剧情详情默认展示原文行，并在右侧或同行展示当前选中翻译版本的译文。
- 翻译版本列表展示 `version_no`、标题、创建时间、创建人 ID，以及 `metadata.staff.translator`、`proofreader`、`approver`。
- 支持选择不同翻译版本后重新加载译文行。
- 原文行和译文行以 `line_no` 对齐；缺失译文行时保留原文行位置并显示空译文状态。
- 从搜索结果进入详情时定位到 `line_no`，并短暂高亮目标行。
- 行类型 `dialogue`、`scene`、`upper_scene`、`choice`、`separator` 需要有稳定的视觉区分，但不引入编辑能力。

### 6. 搜索模块

页面与入口：

- `/search`：统一搜索。

接口：

- `GET /api/search?keyword=...&page=1&page_size=20`

实现要点：

- 搜索词去除首尾空白后不能为空。
- 结果按行展示，明确区分命中来源：共享原文或当前租户译文。
- 每条结果展示剧情类型、剧情集标题、剧情标题、行号、说话人、高亮文本和关联上下文。
- 命中原文时展示同一原文行下当前租户译文摘要。
- 命中译文时展示对应原文和译文版本信息。
- 支持点击结果进入 `/stories/:storyId?line=:lineNo`，如果命中译文则同时带上 `translation_version_id` 作为详情页初始版本。
- 搜索结果分页与关键词同步到 URL，刷新后可恢复。

### 7. 历史译文导入模块

页面与入口：

- `/import/translations`：历史译文 JSON 导入。

接口：

- `POST /api/import/translation-versions`

实现要点：

- 支持上传 JSON 文件和粘贴 JSON 文本。
- 提交前做前端结构校验：`items[]`、`story_type`、`scenario_id`、`lines[]`、`line_no`、`text`。
- 展示导入预览：剧情数量、总行数、story_type、scenario_id、标题和 staff 信息。
- 明确导入语义：每次导入都会为同一租户同一剧情创建新的翻译版本，不做自动去重或更新。
- 导入成功后展示 `translation_version_id`、`version_no`、`line_count`，并提供跳转到对应剧情或翻译版本的入口。
- 导入失败展示后端 `msg` 和 `trace_id`，保留用户输入内容便于修正后重试。
- 注意 `import-write` 限流，页面避免自动重复提交。

### 8. 租户管理模块

页面与入口：

- `/admin/users`：邀请用户加入当前租户。

接口：

- `POST /api/users/invitations`

实现要点：

- `admin` 和 `super_admin` 可进入该页面。
- 表单字段包括 QQ 号和角色。
- `admin` 只能邀请普通用户；`super_admin` 可授予 `normal`、`admin`、`super_admin`。
- 成功后展示用户、成员关系、是否新建用户、是否新建成员关系和 `default_password`。
- `default_password` 只在本次响应中展示，页面不做持久化。

### 9. 同步和运维模块

页面与入口：

- `/admin/sync`：同步任务。
- `/admin/search-index`：搜索索引维护。

接口：

- `POST /api/sync/jobs`
- `GET /api/sync/jobs`
- `GET /api/sync/jobs/{syncJobId}`
- `POST /api/search/index/rebuild`

实现要点：

- 原文同步当前只提交 `{ "source": "moesekai" }`。
- 同步任务列表展示状态、触发类型、开始时间、结束时间、错误信息和 metadata 摘要。
- 同步任务详情用于查看失败样本和执行 metadata。
- 同一时间只允许一个原文同步任务运行；前端在发现运行中任务时禁用重复触发按钮。
- 搜索索引重建只对 `super_admin` 展示入口；接口返回 `202 Accepted` 后展示“已排队”状态，不假设立即完成。
- 搜索索引重建执行结果目前写入服务日志，前端只负责触发和解释当前可见状态。

## 交付顺序

### Milestone 1：应用骨架和登录闭环

交付内容：

- 基于 `web/` 模板清理 CRM 示例页面、示例导航和模板默认 base path。
- 补齐平台路由、布局导航、API Client、错误处理和必要基础组件扩展。
- 登录、会话恢复、租户选择、租户切换、登出。
- 基于角色的导航显示。

验收标准：

- 未登录访问业务页会进入登录页。
- 登录后能进入当前租户工作台。
- 多租户用户可以切换租户，切换后业务数据重新加载。
- `pnpm build` 和 `pnpm lint` 在 `web/` 下通过。

### Milestone 2：搜索和资产只读主路径

交付内容：

- 工作台首页、统一搜索、剧情类型、剧情集列表、剧情列表。
- 剧情详情、原文行、翻译版本和译文行对照阅读。
- 搜索结果跳转到剧情指定行。

验收标准：

- 用户可以从关键词定位到具体剧情行。
- 用户可以从资产目录打开剧情并查看原文和历史译文。
- 分页、筛选、搜索词和目标行可以通过 URL 恢复。

### Milestone 3：导入和租户管理

交付内容：

- 历史译文 JSON 导入、预览、提交结果和错误处理。
- 用户邀请页面和角色约束。

验收标准：

- 合法 JSON 可成功导入并跳转到新翻译版本。
- 非法 JSON 或后端校验失败时保留输入并展示明确错误。
- 管理员权限入口和角色选项符合后端权限规则。

### Milestone 4：同步和运维能力

交付内容：

- 同步任务触发、列表和详情。
- 搜索索引重建触发。
- 运维页面的运行中、已排队、失败和空状态。

验收标准：

- 管理员可以触发 Moe Sekai 同步并查看任务状态。
- 超级管理员可以触发搜索索引重建并看到 `202 Accepted` 后的排队反馈。
- 普通用户看不到管理运维入口。

### Milestone 5：联调、验收和部署接入

交付内容：

- 对接 Docker Compose 本地 API。
- 补齐关键页面的端到端冒烟脚本。
- 接入生产构建产物和服务器反向代理路径。

验收标准：

- 本地 `docker compose up --build` 后可访问前端并完成登录、搜索、剧情查看、导入和管理主路径。
- 关键端到端冒烟覆盖登录、租户选择、搜索、剧情详情、导入和同步任务。
- 构建产物可部署到服务器反向代理后访问 API Service。

## 验证计划

前端单元和组件测试覆盖：

- API Client 错误解析和字段映射。
- 登录状态机和租户切换。
- 搜索结果高亮渲染。
- 导入 JSON 校验。
- 角色到导航入口和表单权限的映射。

端到端冒烟覆盖：

- 登录成功和登出。
- 租户选择和切换。
- 搜索关键词并跳转剧情详情。
- 打开剧情并切换翻译版本。
- 导入一个小型翻译版本。
- 管理员触发同步任务。
- 超级管理员触发搜索索引重建。

## 风险和依赖

- 前端类型来源依赖 Apifox；当前仓库不维护本地 OpenAPI 源文件，类型生成或手写类型都需要定期和 Apifox 对账。
- 搜索索引重建没有前端可查询的任务状态接口，只能展示已排队反馈和说明执行结果在服务日志中查看。
- 导入接口是整批事务，任意项失败整批不写入；前端预校验只能减少低级错误，不能替代后端校验。
- 翻译版本目前只有导入和读取能力，没有编辑、删除、发布或协作流程，前端不得设计对应入口。
- 后端当前不处理移动端适配，前端一期只保证桌面和常规平板宽度下可用。
