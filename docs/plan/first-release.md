# 一期后端交付状态

## 定位

本文记录一期后端完成后的维护口径。历史逐阶段执行记录已清理，当前以后端能力、工程约束和剩余交付事项为准。

## 已完成范围

一期后端已经具备完整语言资产检索链路：

- Docker Compose 本地开发环境。
- API Service、Auth Service、Asset Service、Search Service、Sync Worker。
- PostgreSQL 主存储与 EF Core migration。
- Elasticsearch 统一索引与中文、日文、Unicode 规范化分析插件。
- 用户名密码登录、登出、会话读取、租户选择、用户邀请。
- Moe Sekai / Exmeaning master JSON 与 scenario JSON 同步。
- 活动剧情、主线剧情、卡面剧情、区域对话、特殊剧情原文入库。
- 原文和当前租户译文的行级搜索。
- 历史译文 JSON 批量导入。
- 剧情类型、剧情集、剧情、原文行、翻译版本、翻译行读取接口。
- 内部 token 服务间鉴权模型。
- 集成测试基线。
- GitHub Actions 编译、集成测试、镜像构建和部署入口。

## 后端能力边界

- 原文剧情是全平台共享资产。
- 译文、翻译版本和翻译行按租户隔离。
- 业务 API 使用登录态中的当前租户，不接受客户端传入租户 ID 作为权限依据。
- 内部服务调用使用 `docs/design/security-model.md` 定义的内部 token。
- 平台业务 API 文档在 Apifox 项目 `8210187` 维护，文档站为 <https://sekai-platform.apifox.cn/>。
- 开放 API 文档在 Apifox 项目 `8216122` 维护，草案见 `docs/design/open-api.md`。
- 当前仓库不维护本地 OpenAPI 源文件。

## 工程约束

- 新增或调整业务接口时同步编写集成测试。
- 涉及鉴权、租户隔离、权限边界、数据写入或跨服务调用的接口，完成标准包含对应集成测试通过。
- 涉及架构、数据模型、接口或关键业务流程的改动，先查看 `docs/design/`。
- 涉及内部服务调用、内部 endpoint、维护接口或跨服务用户上下文传递的改动，必须遵循 `docs/design/security-model.md`。
- .NET 项目初始化、solution 管理、项目引用、包引用优先使用 `dotnet` CLI。
- 不手写可由 `dotnet new`、`dotnet sln`、`dotnet add` 生成或维护的项目骨架文件。

## 验证入口

常用验证命令：

```bash
dotnet build SekaiPlatform.sln
dotnet test tests/integration-tests/SekaiPlatform.IntegrationTests.csproj
docker compose config
```

本地服务验证优先使用 IPv6 loopback：

```bash
curl 'http://[::1]:8080/health'
```

冒烟脚本：

```bash
API_BASE_URL='http://[::1]:8080' bash scripts/deployment-smoke.sh
```

## 部署交付状态

已准备：

- `.github/workflows/dotnet.yml`：编译、集成测试、镜像构建、部署入口。
- `deploy/compose.server.yml`：服务器 Docker Compose 基线。
- `deploy/deploy-from-github`：服务器部署脚本源码。
- `deploy/server.env.example`：服务器环境变量样例。
- `docs/deploy/docker-compose-github-actions.md`：Compose 与 GitHub Actions 部署约定。
- `scripts/generate-internal-auth-keys.sh`：内部 token RSA 密钥变量生成。
- `scripts/deployment-smoke.sh`：登录、租户、同步列表、搜索、剧情详情、译文导入冒烟路径。

剩余事项：

- 生产数据库迁移入口。
- 服务器反向代理配置。
- 服务器备份和恢复脚本。
- 真实服务器 Compose 环境冒烟记录。

## 暂不处理

- TXT 导入。
- 实时协同。
- 翻译、校对、合意协作工作流。
- AI 翻译。
- 自动发布。
- 移动端适配。
- 消息队列。
- gRPC。
- Kubernetes。
- 前端页面。
- TXT 导出。
