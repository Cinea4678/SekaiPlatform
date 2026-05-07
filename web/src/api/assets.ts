import type { PagedResponseDto } from './types'
import { apiRequest } from './client'
import { mapPagedResponse } from './types'

export interface StoryTypeInfo {
  value: string
  label: string
}

export interface StoryGroup {
  id: number
  storyType: string
  externalType: string | null
  externalId: string | null
  displayNo: number | null
  title: string
  subtitle: string | null
  metadata: Record<string, unknown> | null
  createdAt: string
  updatedAt: string
}

export interface Story {
  id: number
  group: StoryGroup | null
  storyType: string
  scenarioId: string
  title: string
  sortOrder: number
  metadata: Record<string, unknown> | null
  createdAt: string
  updatedAt: string
}

export interface AssetListParams {
  storyType?: string
  storyGroupId?: number
  keyword?: string
  page?: number
  pageSize?: number
}

interface StoryGroupDto {
  id: number
  story_type: string
  external_type: string | null
  external_id: string | null
  display_no: number | null
  title: string
  subtitle: string | null
  metadata: Record<string, unknown> | null
  created_at: string
  updated_at: string
}

interface StoryDto {
  id: number
  group: StoryGroupDto | null
  story_type: string
  scenario_id: string
  title: string
  sort_order: number
  metadata: Record<string, unknown> | null
  created_at: string
  updated_at: string
}

export function getStoryTypes() {
  return apiRequest<StoryTypeInfo[]>('/api/story-types')
}

export function getStoryGroups(params: AssetListParams = {}) {
  return apiRequest<PagedResponseDto<StoryGroupDto>>(`/api/story-groups${toQueryString(params)}`)
    .then(response => mapPagedResponse(response, mapStoryGroup))
}

export function getStoryGroup(storyGroupId: number) {
  return apiRequest<StoryGroupDto>(`/api/story-groups/${storyGroupId}`).then(mapStoryGroup)
}

export function getStories(params: AssetListParams = {}) {
  return apiRequest<PagedResponseDto<StoryDto>>(`/api/stories${toQueryString(params)}`)
    .then(response => mapPagedResponse(response, mapStory))
}

export function getStory(storyId: number) {
  return apiRequest<StoryDto>(`/api/stories/${storyId}`).then(mapStory)
}

function mapStoryGroup(group: StoryGroupDto): StoryGroup {
  return {
    id: group.id,
    storyType: group.story_type,
    externalType: group.external_type,
    externalId: group.external_id,
    displayNo: group.display_no,
    title: group.title,
    subtitle: group.subtitle,
    metadata: group.metadata,
    createdAt: group.created_at,
    updatedAt: group.updated_at,
  }
}

function mapStory(story: StoryDto): Story {
  return {
    id: story.id,
    group: story.group ? mapStoryGroup(story.group) : null,
    storyType: story.story_type,
    scenarioId: story.scenario_id,
    title: story.title,
    sortOrder: story.sort_order,
    metadata: story.metadata,
    createdAt: story.created_at,
    updatedAt: story.updated_at,
  }
}

function toQueryString(params: AssetListParams) {
  const query = new URLSearchParams()
  if (params.storyType) {
    query.set('story_type', params.storyType)
  }

  if (params.storyGroupId) {
    query.set('story_group_id', String(params.storyGroupId))
  }

  if (params.keyword) {
    query.set('keyword', params.keyword)
  }

  if (params.page) {
    query.set('page', String(params.page))
  }

  if (params.pageSize) {
    query.set('page_size', String(params.pageSize))
  }

  const text = query.toString()
  return text ? `?${text}` : ''
}
