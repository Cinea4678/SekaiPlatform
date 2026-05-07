import type { StoryTypeInfo } from '@/api/assets'
import type { TenantRole } from '@/api/auth'

const roleLabels: Record<TenantRole, string> = {
  normal: '成员',
  admin: '管理员',
  super_admin: '超级管理员',
}

export function formatTenantRole(role: TenantRole | undefined | null) {
  return role ? roleLabels[role] : '未选择角色'
}

export function formatDateTime(value: string | undefined | null) {
  if (!value) {
    return '-'
  }

  return new Intl.DateTimeFormat('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

export function createStoryTypeLabeler(storyTypes: StoryTypeInfo[]) {
  const labels = new Map(storyTypes.map(item => [item.value, item.label]))
  return (storyType: string) => labels.get(storyType) || storyType
}
