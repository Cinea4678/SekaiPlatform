# 前端开发说明

## 本地 API

```bash
pnpm dev
```

默认读取 `.env.development`，通过 Vite dev proxy 将 `/api` 转发到 `https://platform.pjs.accr.cc`。

## 连接生产 API

```bash
pnpm dev --mode production
```

该命令读取 `.env.production`，通过 Vite dev proxy 将 `/api` 转发到 `https://platform.pjs.accr.cc`。

如需连接本地 API，在 `web/.env.development.local` 写入：

```text
API_PROXY_TARGET=http://localhost:8080
```

## 构建

```bash
pnpm lint
pnpm build
```

构建产物输出到 `dist/`，可由 `pnpm serve` 进行本地静态服务验证。
