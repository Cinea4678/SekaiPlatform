export interface ApiErrorPayload {
  msg: string
  trace_id: string
}

export class ApiError extends Error {
  readonly status: number
  readonly traceId?: string

  constructor(status: number, message: string, traceId?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.traceId = traceId
  }
}

let accessToken: string | null = null

export function setAccessToken(token: string | null) {
  accessToken = token
}

export async function apiRequest<TResponse>(
  path: string,
  options: RequestInit = {},
): Promise<TResponse> {
  const response = await fetch(path, {
    ...options,
    credentials: 'include',
    headers: createHeaders(options),
  })

  if (!response.ok) {
    throw await createApiError(response)
  }

  if (response.status === 204) {
    return undefined as TResponse
  }

  return await response.json() as TResponse
}

function createHeaders(options: RequestInit) {
  const result = new Headers(options.headers)
  if (!result.has('Accept')) {
    result.set('Accept', 'application/json')
  }

  if (options.body && !result.has('Content-Type')) {
    result.set('Content-Type', 'application/json')
  }

  if (accessToken) {
    result.set('Authorization', `Bearer ${accessToken}`)
  }

  return result
}

async function createApiError(response: Response) {
  const fallbackMessage = response.status === 401
    ? '未登录或登录已失效。'
    : '请求失败，请稍后重试。'

  try {
    const payload = await response.json() as Partial<ApiErrorPayload>
    return new ApiError(
      response.status,
      payload.msg || fallbackMessage,
      payload.trace_id,
    )
  }
  catch {
    return new ApiError(response.status, fallbackMessage)
  }
}
