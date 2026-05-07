import type { TranslationStaff } from './assets'
import type { PageMeta } from './types'
import { apiRequest } from './client'

export type SearchAssetType = 'source' | 'translation'

export interface SearchSourceLineReference {
  sourceLineId: number
  text: string
  speaker: string | null
}

export interface SearchTranslationLineReference {
  translationLineId: number
  translationVersionId: number
  versionNo: number
  translationVersionTitle: string | null
  staff: TranslationStaff | null
  text: string
  speaker: string | null
}

export interface SearchHit {
  assetType: SearchAssetType
  text: string
  highlightText: string
  speaker: string | null
  lineNo: number
  storyId: number
  storyTitle: string
  storyType: string
  storyGroupId: number | null
  storyGroupTitle: string | null
  sourceLineId: number
  translationLineId: number | null
  translationVersionId: number | null
  staff: TranslationStaff | null
  source: SearchSourceLineReference | null
  translations: SearchTranslationLineReference[]
}

export interface SearchResponse {
  items: SearchHit[]
  page: PageMeta
}

export interface SearchParams {
  keyword: string
  page?: number
  pageSize?: number
}

interface SearchResponseDto {
  items: SearchHitDto[]
  total: number
  page: number
  page_size: number
}

interface SearchSourceLineReferenceDto {
  source_line_id: number
  text: string
  speaker: string | null
}

interface SearchTranslationLineReferenceDto {
  translation_line_id: number
  translation_version_id: number
  version_no: number
  translation_version_title: string | null
  staff: TranslationStaff | null
  text: string
  speaker: string | null
}

interface SearchHitDto {
  asset_type: SearchAssetType
  text: string
  highlight_text: string
  speaker: string | null
  line_no: number
  story_id: number
  story_title: string
  story_type: string
  story_group_id: number | null
  story_group_title: string | null
  source_line_id: number
  translation_line_id: number | null
  translation_version_id: number | null
  staff: TranslationStaff | null
  source: SearchSourceLineReferenceDto | null
  translations: SearchTranslationLineReferenceDto[]
}

export function searchAssets(params: SearchParams) {
  const query = new URLSearchParams()
  query.set('keyword', params.keyword)
  if (params.page) {
    query.set('page', String(params.page))
  }

  if (params.pageSize) {
    query.set('page_size', String(params.pageSize))
  }

  return apiRequest<SearchResponseDto>(`/api/search?${query.toString()}`).then(mapSearchResponse)
}

function mapSearchResponse(response: SearchResponseDto): SearchResponse {
  return {
    items: response.items.map(mapSearchHit),
    page: {
      page: response.page,
      pageSize: response.page_size,
      total: response.total,
    },
  }
}

function mapSearchHit(hit: SearchHitDto): SearchHit {
  return {
    assetType: hit.asset_type,
    text: hit.text,
    highlightText: hit.highlight_text,
    speaker: hit.speaker,
    lineNo: hit.line_no,
    storyId: hit.story_id,
    storyTitle: hit.story_title,
    storyType: hit.story_type,
    storyGroupId: hit.story_group_id,
    storyGroupTitle: hit.story_group_title,
    sourceLineId: hit.source_line_id,
    translationLineId: hit.translation_line_id,
    translationVersionId: hit.translation_version_id,
    staff: hit.staff,
    source: hit.source
      ? {
          sourceLineId: hit.source.source_line_id,
          text: hit.source.text,
          speaker: hit.source.speaker,
        }
      : null,
    translations: hit.translations.map(translation => ({
      translationLineId: translation.translation_line_id,
      translationVersionId: translation.translation_version_id,
      versionNo: translation.version_no,
      translationVersionTitle: translation.translation_version_title,
      staff: translation.staff,
      text: translation.text,
      speaker: translation.speaker,
    })),
  }
}
