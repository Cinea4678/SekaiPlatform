# 开放 API 草案

## 定位

开放 API 面向外部合作伙伴。当前阶段只建立独立的 OpenApiService 监听端口，暂不开放任何业务 API。

OpenApiService 不复用前端登录态，不接受用户 Cookie，不暴露内部 `/internal/...` endpoint。后续开放业务接口时，路径继续使用 `/api` 风格，因为 OpenApiService 与 API Service 不共用监听端口。

正式开放 API 文档维护在独立 Apifox 项目：

- Apifox 项目编号：`8216122`
- 当前仓库保留本文件作为接口草案和安全边界说明。

## 服务边界

```text
External Partner
    |
OpenApiService
```

服务职责：

| 服务 | 职责 |
|---|---|
| OpenApiService | 开放 API 独立监听入口，处理匿名访问、IP 限流、错误格式和后续开放接口承载。 |

当前阶段 OpenApiService 不编排 Auth Service、Asset Service 或 Search Service。后续新增开放业务接口时，再为具体接口定义内部服务调用链、内部 token scope 和返回对象。

## 匿名访问

开放 API 当前允许匿名访问。调用方不需要登录态、Cookie、Bearer Token 或 API Key。

处理规则：

1. OpenApiService 不读取外部用户 Cookie。
2. OpenApiService 不接受 API Key。
3. OpenApiService 对所有进入开放 API 端口的请求执行 IP 限流。
4. 未开放的业务路径返回 `404 Not Found`。

## 限流

开放 API 必须限流。

限流规则：

| 维度 | 默认值 |
|---|---:|
| 单 IP | 10 req/min |

IP 来源：

- 优先使用 `X-Forwarded-For` 中的客户端 IP。
- `X-Forwarded-For` 只信任受控反向代理写入的值。
- 多级代理场景取第一个有效 IP。
- 没有可信 `X-Forwarded-For` 时使用连接远端 IP。

超过限制时返回 `429 Too Many Requests`，并尽量返回 `Retry-After`。

## 当前接口范围

当前阶段不开放任何业务 API。

允许存在：

- OpenApiService 监听端口。
- 健康检查。
- 限流中间件。
- 统一错误格式。

暂不开放：

- 剧情类型读取。
- 剧情集读取。
- 剧情读取。
- 原文行读取。
- 搜索。
- 租户译文、翻译版本和翻译行。
- 用户、租户、登录、邀请、租户切换。
- 导入、同步、索引重建等写操作和维护操作。
- 内部服务路径、内部 token、内部 trace 细节。

## 错误格式

开放 API 使用稳定错误码，避免把内部错误直接透传给合作伙伴。

```json
{
  "code": "not_found",
  "message": "Not found",
  "trace_id": "..."
}
```

常用错误码：

| HTTP | code | 说明 |
|---:|---|---|
| 404 | `not_found` | 当前路径未开放。 |
| 429 | `rate_limited` | 超出限流。 |
| 500 | `internal_error` | 服务内部错误。 |

## 后续接口约定

后续开放业务接口时，OpenApiService 端口下继续使用 `/api` 风格路径。

示例：

```text
/api/...
```

新增接口前必须先补充：

- 公开数据边界。
- 请求和响应对象。
- 是否需要内部服务调用。
- 内部 token scope。
- Apifox 开放 API 项目文档。

## 暂不设计的能力

当前开放 API 草案不包含：

- API Key。
- 合作伙伴账号。
- 合作伙伴级 scope。
- 合作伙伴级配额。
- API Key 签发、吊销和管理接口。

后续如果开放 API 需要分合作伙伴授权，再新增对应鉴权、数据模型和管理接口。
