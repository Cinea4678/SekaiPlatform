# Docker Compose + GitHub Actions 部署操作手册

## 目标

使用 GitHub Actions 构建并推送镜像到 GHCR。部署时，GitHub-hosted runner 通过 SSH 登录服务器，只执行服务器上的部署脚本：

```bash
/usr/local/bin/deploy-from-github --image-prefix <image-prefix> --image-tag <commit-sha>
```

服务器脚本负责登录 GHCR、拉取镜像、生成 Compose 运行文件并重启服务。

## 1. 服务器准备 Docker

在服务器执行：

```bash
docker version
docker compose version
```

部署用户必须能直接运行 Docker：

```bash
docker ps
```

如果当前用户没有 Docker 权限，将用户加入 `docker` 组后重新登录：

```bash
sudo usermod -aG docker <deploy-user>
```

## 2. 安装部署脚本

在本地仓库执行，把脚本复制到服务器：

```bash
scp deploy/deploy-from-github <deploy-user>@<server-host>:/tmp/deploy-from-github
```

在服务器执行：

```bash
sudo install -m 0755 /tmp/deploy-from-github /usr/local/bin/deploy-from-github
sudo mkdir -p /etc/sekai-platform
sudo mkdir -p /opt/sekai-platform
sudo chown <deploy-user>:<deploy-user> /opt/sekai-platform
```

验证脚本可执行：

```bash
/usr/local/bin/deploy-from-github --help
```

## 3. 配置服务器运行环境

在服务器创建 `/etc/sekai-platform/server.env`：

```bash
sudo install -o <deploy-user> -g <deploy-user> -m 0600 /dev/null /etc/sekai-platform/server.env
sudo nano /etc/sekai-platform/server.env
```

按 `deploy/server.env.example` 填写以下内容：

```bash
POSTGRES_DB=sekai_platform
POSTGRES_USER=sekai_platform
POSTGRES_PASSWORD=<server-postgres-password>

JWT_ISSUER=sekai-platform
JWT_AUDIENCE=sekai-platform
JWT_SIGNING_KEY=<server-jwt-signing-key>

INTERNAL_AUTH_ISSUER=sekai-platform-internal
API_SERVICE_INTERNAL_PRIVATE_KEY=<api-service-private-key>
API_SERVICE_INTERNAL_PUBLIC_KEY=<api-service-public-key>
ASSET_SERVICE_INTERNAL_PRIVATE_KEY=<asset-service-private-key>
ASSET_SERVICE_INTERNAL_PUBLIC_KEY=<asset-service-public-key>
SYNC_WORKER_INTERNAL_PRIVATE_KEY=<sync-worker-private-key>
SYNC_WORKER_INTERNAL_PUBLIC_KEY=<sync-worker-public-key>

ASPNETCORE_ENVIRONMENT=Production
DATABASE_AUTO_MIGRATE=false
DATABASE_SEED=false

API_SERVICE_BIND_HOST=127.0.0.1
API_SERVICE_PORT=8080
ELASTICSEARCH_INDEX_NAME=sekai-language-assets-v1
ELASTICSEARCH_JAVA_OPTS=-Xms512m -Xmx512m
ELASTICSEARCH_CLI_JAVA_OPTS=
```

生成内部 token 密钥时，在本地或服务器执行：

```bash
scripts/generate-internal-auth-keys.sh
```

不要把生成结果提交到 Git。

## 4. 配置服务器 GHCR 读取凭据

准备一个可读取 GHCR package 的 GitHub token。私有 package 使用 classic PAT 时需要 `read:packages` 权限。

在服务器创建 `/etc/sekai-platform/deploy-from-github.env`：

```bash
sudo install -o <deploy-user> -g <deploy-user> -m 0600 /dev/null /etc/sekai-platform/deploy-from-github.env
sudo nano /etc/sekai-platform/deploy-from-github.env
```

写入：

```bash
GHCR_USERNAME=<github-user-or-machine-account>
GHCR_TOKEN=<github-token-with-read-packages>
```

验证服务器能登录 GHCR：

```bash
source /etc/sekai-platform/deploy-from-github.env
printf '%s' "$GHCR_TOKEN" | docker login ghcr.io -u "$GHCR_USERNAME" --password-stdin
```

## 5. 配置部署 SSH key

在本地生成部署专用 key：

```bash
ssh-keygen -t ed25519 -C sekai-platform-deploy -f ./sekai-platform-deploy
```

把公钥加入服务器部署用户：

```bash
ssh-copy-id -i ./sekai-platform-deploy.pub <deploy-user>@<server-host>
```

生成 known_hosts 内容：

```bash
ssh-keyscan -p <ssh-port> <server-host> > ./sekai-platform-known-hosts
```

验证 SSH 只能执行部署脚本所需命令前，先确认基础连通：

```bash
ssh -i ./sekai-platform-deploy -p <ssh-port> <deploy-user>@<server-host> \
  '/usr/local/bin/deploy-from-github --help'
```

## 6. 配置 GitHub production environment

在 GitHub 仓库进入：

```text
Settings -> Environments -> New environment -> production
```

添加 Environment variables：

| Name | Value |
|---|---|
| `SEKAI_DEPLOY_HOST` | `<server-host>` |
| `SEKAI_DEPLOY_PORT` | `<ssh-port>` |
| `SEKAI_DEPLOY_USER` | `<deploy-user>` |
| `SEKAI_DEPLOY_SCRIPT` | `/usr/local/bin/deploy-from-github` |

添加 Environment secrets：

| Name | Value |
|---|---|
| `SEKAI_DEPLOY_SSH_KEY` | `./sekai-platform-deploy` 私钥全文 |
| `SEKAI_DEPLOY_KNOWN_HOSTS` | `./sekai-platform-known-hosts` 全文 |

配置完成后删除本地临时私钥文件：

```bash
rm -f ./sekai-platform-deploy ./sekai-platform-deploy.pub ./sekai-platform-known-hosts
```

## 7. 构建镜像

推送到 `main` 后，GitHub Actions 自动执行：

```text
build-test -> build-images
```

确认 `build-images` 成功，并且 GHCR 中存在以下镜像：

```text
ghcr.io/<owner>/<repo>/api-service:<commit-sha>
ghcr.io/<owner>/<repo>/auth-service:<commit-sha>
ghcr.io/<owner>/<repo>/asset-service:<commit-sha>
ghcr.io/<owner>/<repo>/search-service:<commit-sha>
ghcr.io/<owner>/<repo>/sync-worker:<commit-sha>
ghcr.io/<owner>/<repo>/elasticsearch:<commit-sha>
```

## 8. 首次部署

在 GitHub Actions 页面手动触发 `Phase 9` workflow：

```text
Actions -> Phase 9 -> Run workflow -> main
```

该 workflow 会重新执行构建和镜像发布，然后执行 deploy job。deploy job 只会通过 SSH 执行服务器脚本。

## 9. 服务器验证

在服务器执行：

```bash
cd /opt/sekai-platform
docker compose ps
docker compose logs --tail=100 api-service
```

在服务器或反向代理所在机器执行：

```bash
curl -fsS http://127.0.0.1:8080/health
```

如需在本机连服务器验证，通过反向代理域名访问公开入口。

## 10. 手动部署指定镜像

在服务器执行：

```bash
/usr/local/bin/deploy-from-github \
  --image-prefix ghcr.io/<owner>/<repo> \
  --image-tag <commit-sha>
```

先做 dry-run：

```bash
/usr/local/bin/deploy-from-github \
  --image-prefix ghcr.io/<owner>/<repo> \
  --image-tag <commit-sha> \
  --dry-run
```

## 11. 回滚

找到上一个可用 commit SHA 后，在服务器执行：

```bash
/usr/local/bin/deploy-from-github \
  --image-prefix ghcr.io/<owner>/<repo> \
  --image-tag <previous-good-sha>
```

确认：

```bash
cd /opt/sekai-platform
docker compose ps
curl -fsS http://127.0.0.1:8080/health
```

## 12. 数据库迁移

当前 Production 环境保持：

```bash
DATABASE_AUTO_MIGRATE=false
DATABASE_SEED=false
```

生产数据库迁移在部署前单独执行。Phase 9 不在部署脚本内自动执行 migration。
