export interface PageMeta {
  page: number
  pageSize: number
  total: number
}

export interface PagedResponse<TItem> {
  items: TItem[]
  page: PageMeta
}

export interface PageMetaDto {
  page: number
  page_size: number
  total: number
}

export interface PagedResponseDto<TItem> {
  items: TItem[]
  page: PageMetaDto
}

export function mapPagedResponse<TDto, TItem>(
  response: PagedResponseDto<TDto>,
  mapItem: (item: TDto) => TItem,
): PagedResponse<TItem> {
  return {
    items: response.items.map(mapItem),
    page: {
      page: response.page.page,
      pageSize: response.page.page_size,
      total: response.page.total,
    },
  }
}
