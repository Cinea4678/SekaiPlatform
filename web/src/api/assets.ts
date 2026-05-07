import type { PagedResponseDto } from './types'
import { apiRequest } from './client'
import { mapPagedResponse } from './types'

export interface TranslationStaff {
  translator: string | null
  proofreader: string | null
  approver: string | null
}

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

export interface StorySourceLine {
  id: number
  storyId: number
  lineNo: number
  lineType: string
  speaker: string | null
  text: string
  metadata: Record<string, unknown> | null
  createdAt: string
  updatedAt: string
}

export interface TranslationVersion {
  id: number
  storyId: number
  versionNo: number
  title: string | null
  metadata: Record<string, unknown> | null
  staff: TranslationStaff | null
  createdBy: number
  createdAt: string
  updatedAt: string
}

export interface TranslationLine {
  id: number
  versionId: number
  sourceLineId: number
  lineNo: number
  speaker: string | null
  text: string
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

export interface TranslationVersionListParams {
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

interface StorySourceLineDto {
  id: number
  story_id: number
  line_no: number
  line_type: string
  speaker: string | null
  text: string
  metadata: Record<string, unknown> | null
  created_at: string
  updated_at: string
}

interface TranslationVersionDto {
  id: number
  story_id: number
  version_no: number
  title: string | null
  metadata: Record<string, unknown> | null
  created_by: number
  created_at: string
  updated_at: string
}

interface TranslationLineDto {
  id: number
  version_id: number
  source_line_id: number
  line_no: number
  speaker: string | null
  text: string
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

export function getStorySourceLines(storyId: number) {
  return apiRequest<StorySourceLineDto[]>(`/api/stories/${storyId}/source-lines`)
    .then(response => response.map(mapStorySourceLine))
}

export function getTranslationVersions(storyId: number, params: TranslationVersionListParams = {}) {
  return apiRequest<PagedResponseDto<TranslationVersionDto>>(
    `/api/stories/${storyId}/translation-versions${toQueryString(params)}`,
  ).then(response => mapPagedResponse(response, mapTranslationVersion))
}

export function getTranslationVersion(translationVersionId: number) {
  return apiRequest<TranslationVersionDto>(`/api/translation-versions/${translationVersionId}`)
    .then(mapTranslationVersion)
}

export function getTranslationLines(translationVersionId: number) {
  return apiRequest<TranslationLineDto[]>(`/api/translation-versions/${translationVersionId}/lines`)
    .then(response => response.map(mapTranslationLine))
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

function mapStorySourceLine(line: StorySourceLineDto): StorySourceLine {
  return {
    id: line.id,
    storyId: line.story_id,
    lineNo: line.line_no,
    lineType: line.line_type,
    speaker: line.speaker,
    text: line.text,
    metadata: line.metadata,
    createdAt: line.created_at,
    updatedAt: line.updated_at,
  }
}

function mapTranslationVersion(version: TranslationVersionDto): TranslationVersion {
  return {
    id: version.id,
    storyId: version.story_id,
    versionNo: version.version_no,
    title: version.title,
    metadata: version.metadata,
    staff: readTranslationStaff(version.metadata),
    createdBy: version.created_by,
    createdAt: version.created_at,
    updatedAt: version.updated_at,
  }
}

function mapTranslationLine(line: TranslationLineDto): TranslationLine {
  return {
    id: line.id,
    versionId: line.version_id,
    sourceLineId: line.source_line_id,
    lineNo: line.line_no,
    speaker: line.speaker,
    text: line.text,
    metadata: line.metadata,
    createdAt: line.created_at,
    updatedAt: line.updated_at,
  }
}

export function readTranslationStaff(metadata: Record<string, unknown> | null): TranslationStaff | null {
  const staff = metadata?.staff
  if (!staff || typeof staff !== 'object') {
    return null
  }

  const source = staff as Record<string, unknown>
  return {
    translator: readOptionalText(source.translator),
    proofreader: readOptionalText(source.proofreader),
    approver: readOptionalText(source.approver),
  }
}

function readOptionalText(value: unknown) {
  return typeof value === 'string' && value.trim() ? value : null
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
