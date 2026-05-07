import { apiRequest } from './client'

export type TenantRole = 'normal' | 'admin' | 'super_admin'

export interface AuthResponseDto {
  access_token: string
  expires_at: string
  user: UserProfileDto
  current_tenant: TenantMembershipDto | null
  tenants: TenantMembershipDto[]
}

export interface SessionResponseDto {
  user: UserProfileDto
  current_tenant: TenantMembershipDto | null
  tenants: TenantMembershipDto[]
}

export interface TenantMembershipDto {
  id: number
  name: string
  avatar_url: string | null
  role: string
}

export interface UserProfileDto {
  id: number
  qq_id: string | null
  display_name: string | null
  avatar_url: string | null
}

export interface AuthSession {
  accessToken: string | null
  expiresAt: string | null
  user: UserProfile
  currentTenant: TenantMembership | null
  tenants: TenantMembership[]
}

export interface SessionState {
  user: UserProfile
  currentTenant: TenantMembership | null
  tenants: TenantMembership[]
}

export interface TenantMembership {
  id: number
  name: string
  avatarUrl: string | null
  role: TenantRole
}

export interface UserProfile {
  id: number
  qqId: string | null
  displayName: string | null
  avatarUrl: string | null
}

export function login(username: string, password: string) {
  return apiRequest<AuthResponseDto>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ username, password }),
  }).then(mapAuthResponse)
}

export function logout() {
  return apiRequest<{ ok: boolean }>('/api/auth/logout', {
    method: 'POST',
  })
}

export function getSession() {
  return apiRequest<SessionResponseDto>('/api/auth/session').then(mapSessionResponse)
}

export function switchTenant(tenantId: number) {
  return apiRequest<AuthResponseDto>('/api/auth/current-tenant', {
    method: 'PUT',
    body: JSON.stringify({ tenant_id: tenantId }),
  }).then(mapAuthResponse)
}

function mapAuthResponse(response: AuthResponseDto): AuthSession {
  return {
    accessToken: response.access_token,
    expiresAt: response.expires_at,
    user: mapUser(response.user),
    currentTenant: response.current_tenant ? mapTenant(response.current_tenant) : null,
    tenants: response.tenants.map(mapTenant),
  }
}

function mapSessionResponse(response: SessionResponseDto): SessionState {
  return {
    user: mapUser(response.user),
    currentTenant: response.current_tenant ? mapTenant(response.current_tenant) : null,
    tenants: response.tenants.map(mapTenant),
  }
}

function mapUser(user: UserProfileDto): UserProfile {
  return {
    id: user.id,
    qqId: user.qq_id,
    displayName: user.display_name,
    avatarUrl: user.avatar_url,
  }
}

function mapTenant(tenant: TenantMembershipDto): TenantMembership {
  return {
    id: tenant.id,
    name: tenant.name,
    avatarUrl: tenant.avatar_url,
    role: isTenantRole(tenant.role) ? tenant.role : 'normal',
  }
}

function isTenantRole(role: string): role is TenantRole {
  return role === 'normal' || role === 'admin' || role === 'super_admin'
}
