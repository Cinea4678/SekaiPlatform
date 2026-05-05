# Phase 9 准备记录

## 状态

Phase 9：本地交付与部署基线准备中。

## 范围

Phase 9 在 Phase 8 完整后端链路基础上收口交付入口：

- 本地 Docker Compose 启动。
- 本地内部 token 密钥生成。
- 本地接口冒烟测试。
- 服务器 Docker Compose 部署基线。
- GitHub Actions 编译、测试、镜像发布和部署入口。
- GitHub-hosted runner 通过 SSH 触发服务器部署脚本。

## 已准备

- 更新 `.github/workflows/dotnet.yml`：
  - `build-test` 执行 .NET 编译和集成测试。
  - `build-images` 构建并推送服务镜像到 GHCR。
  - `deploy` 使用 production environment SSH 私钥触发服务器部署脚本。
- 新增 `deploy/compose.server.yml`，服务器使用 GHCR 镜像运行服务。
- 新增 `deploy/deploy-from-github`，作为 `/usr/local/bin/deploy-from-github` 的服务器脚本源码。
- 新增 `deploy/server.env.example`，记录服务器 `.env` 所需变量。
- 新增 `docs/deploy/docker-compose-github-actions.md`，记录 Compose 与 GitHub Actions 部署约定。
- 新增 `scripts/generate-internal-auth-keys.sh`，生成本地和服务器可用的内部 token RSA 密钥变量。
- 新增 `scripts/phase9-smoke.sh`，覆盖健康检查、登录、租户、同步列表、搜索、剧情详情和译文导入的冒烟路径。

## 部署约定

- 本地开发继续使用根目录 `docker-compose.yml`。
- 服务器部署使用 `deploy/compose.server.yml`。
- 服务器 API Service 默认只绑定 `127.0.0.1:8080`。
- 服务器公网入口由反向代理负责。
- GitHub deploy job 使用 GitHub-hosted runner。
- GitHub deploy job 通过 SSH 调用服务器上的 `/usr/local/bin/deploy-from-github`。
- GitHub deploy job 通过 `workflow_dispatch` 手动触发。

## 待完成

- 生产数据库迁移入口。
- 服务器反向代理配置。
- 服务器备份和恢复脚本。
- Phase 9 冒烟测试在真实 Compose 环境跑通记录。

## 验证

待补充。
