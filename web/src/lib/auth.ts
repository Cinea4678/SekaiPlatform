import type { AuthSession, SessionState, TenantMembership, TenantRole, UserProfile } from '@/api/auth'
import { reactive, readonly } from 'vue'
import { getSession, login, logout, switchTenant } from '@/api/auth'
import { ApiError, setAccessToken } from '@/api/client'

interface AuthState {
  bootstrapped: boolean
  loading: boolean
  error: ApiError | null
  accessToken: string | null
  expiresAt: string | null
  user: UserProfile | null
  currentTenant: TenantMembership | null
  tenants: TenantMembership[]
}

const state = reactive<AuthState>({
  bootstrapped: false,
  loading: false,
  error: null,
  accessToken: null,
  expiresAt: null,
  user: null,
  currentTenant: null,
  tenants: [],
})

let bootstrapPromise: Promise<void> | null = null

export function useAuth() {
  return {
    state: readonly(state),
    bootstrapSession,
    login: loginWithPassword,
    logout: logoutSession,
    switchTenant: switchCurrentTenant,
    canAccessRole,
  }
}

export async function bootstrapSession() {
  if (state.bootstrapped) {
    return
  }

  bootstrapPromise ??= loadSession()
  await bootstrapPromise
}

async function loadSession() {
  state.loading = true
  state.error = null

  try {
    applySession(await getSession())
  }
  catch (error) {
    clearSession()
    if (error instanceof ApiError && error.status !== 401) {
      state.error = error
    }
  }
  finally {
    state.bootstrapped = true
    state.loading = false
    bootstrapPromise = null
  }
}

async function loginWithPassword(username: string, password: string) {
  state.loading = true
  state.error = null

  try {
    applyAuthSession(await login(username, password))
  }
  catch (error) {
    state.error = normalizeError(error)
    throw error
  }
  finally {
    state.bootstrapped = true
    state.loading = false
  }
}

async function switchCurrentTenant(tenantId: number) {
  state.loading = true
  state.error = null

  try {
    applyAuthSession(await switchTenant(tenantId))
  }
  catch (error) {
    state.error = normalizeError(error)
    throw error
  }
  finally {
    state.loading = false
  }
}

async function logoutSession() {
  state.loading = true
  state.error = null

  try {
    await logout()
  }
  finally {
    clearSession()
    state.bootstrapped = true
    state.loading = false
  }
}

function applyAuthSession(session: AuthSession) {
  setAccessToken(session.accessToken)
  state.accessToken = session.accessToken
  state.expiresAt = session.expiresAt
  state.user = session.user
  state.currentTenant = session.currentTenant
  state.tenants = session.tenants
}

function applySession(session: SessionState) {
  state.user = session.user
  state.currentTenant = session.currentTenant
  state.tenants = session.tenants
}

function clearSession() {
  setAccessToken(null)
  state.accessToken = null
  state.expiresAt = null
  state.user = null
  state.currentTenant = null
  state.tenants = []
}

function canAccessRole(role: TenantRole | undefined, allowedRoles?: TenantRole[]) {
  return !allowedRoles || (!!role && allowedRoles.includes(role))
}

function normalizeError(error: unknown) {
  return error instanceof ApiError
    ? error
    : new ApiError(0, '请求失败，请稍后重试。')
}
