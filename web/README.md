# Sekai Platform Web

Vue 3 + Vite + TypeScript 前端应用，用于承载 Sekai Platform 的语言资产工作台。

## 环境

前端请求始终使用同源 `/api` 路径。开发环境通过 Vite dev proxy 转发 API：

- `.env.development`：`API_PROXY_TARGET=https://platform.pjs.accr.cc`
- `.env.production`：`API_PROXY_TARGET=https://platform.pjs.accr.cc`

如需连接本地后端，在 `web/.env.development.local` 写入：

```text
API_PROXY_TARGET=http://localhost:8080
```

## 命令

```bash
pnpm install
pnpm dev
pnpm dev --mode production
pnpm lint
pnpm build
```

`pnpm dev` 默认通过代理连接生产 API；`pnpm dev --mode production` 使用 production mode 下的同一代理目标。

## 当前能力

当前已接入应用骨架、登录、会话恢复、租户选择、租户切换、登出和角色导航。搜索、资产、导入和管理入口会按当前身份展示，并逐步开放完整业务操作。
