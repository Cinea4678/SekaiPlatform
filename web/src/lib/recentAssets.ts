import type { Story, StoryGroup } from '@/api/assets'

export type RecentAssetKind = 'group' | 'story'

export interface RecentAsset {
  kind: RecentAssetKind
  id: number
  title: string
  subtitle: string | null
  storyType: string
  href: string
  viewedAt: string
}

const maxRecentAssets = 6

export function getRecentAssets(tenantId: number | undefined | null) {
  if (!tenantId || typeof window === 'undefined') {
    return []
  }

  const stored = window.localStorage.getItem(getStorageKey(tenantId))
  if (!stored) {
    return []
  }

  try {
    const assets = JSON.parse(stored) as unknown
    return Array.isArray(assets)
      ? assets.filter(isRecentAsset).slice(0, maxRecentAssets)
      : []
  }
  catch {
    return []
  }
}

export function recordRecentStoryGroup(tenantId: number | undefined | null, group: StoryGroup) {
  recordRecentAsset(tenantId, {
    kind: 'group',
    id: group.id,
    title: group.title,
    subtitle: group.subtitle,
    storyType: group.storyType,
    href: `/assets/groups/${group.id}`,
    viewedAt: new Date().toISOString(),
  })
}

export function recordRecentStory(tenantId: number | undefined | null, story: Story) {
  recordRecentAsset(tenantId, {
    kind: 'story',
    id: story.id,
    title: story.title,
    subtitle: story.group?.title || story.scenarioId,
    storyType: story.storyType,
    href: `/stories/${story.id}`,
    viewedAt: new Date().toISOString(),
  })
}

function recordRecentAsset(tenantId: number | undefined | null, asset: RecentAsset) {
  if (!tenantId || typeof window === 'undefined') {
    return
  }

  const next = [
    asset,
    ...getRecentAssets(tenantId).filter(item => item.kind !== asset.kind || item.id !== asset.id),
  ].slice(0, maxRecentAssets)
  window.localStorage.setItem(getStorageKey(tenantId), JSON.stringify(next))
}

function getStorageKey(tenantId: number) {
  return `sekai-platform:recent-assets:${tenantId}`
}

function isRecentAsset(value: unknown): value is RecentAsset {
  if (!value || typeof value !== 'object') {
    return false
  }

  const asset = value as Partial<RecentAsset>
  return (
    (asset.kind === 'group' || asset.kind === 'story')
    && typeof asset.id === 'number'
    && typeof asset.title === 'string'
    && typeof asset.storyType === 'string'
    && typeof asset.href === 'string'
  )
}
