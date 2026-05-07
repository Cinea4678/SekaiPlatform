import type { StoryTypeInfo, TranslationStaff, TranslationVersion } from '@/api/assets'
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

export function formatTranslationVersion(version: TranslationVersion) {
  return `第 ${version.versionNo} 版 · ${version.title || '未命名译文'}`
}

export function formatLineType(lineType: string) {
  const labels: Record<string, string> = {
    dialogue: '对白',
    scene: '场景',
    upper_scene: '上层场景',
    choice: '选项',
    separator: '分隔',
  }
  return labels[lineType] || lineType
}

export function formatStaff(staff: TranslationStaff | null | undefined) {
  if (!staff) {
    return '暂无署名'
  }

  const parts = [
    staff.translator ? `翻译：${staff.translator}` : '',
    staff.proofreader ? `校对：${staff.proofreader}` : '',
    staff.approver ? `合意：${staff.approver}` : '',
  ].filter(Boolean)

  return parts.length ? parts.join(' · ') : '暂无署名'
}
