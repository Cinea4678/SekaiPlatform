export type QueryValue = string | null | (string | null)[]

export const defaultPageSize = 20

export function readQueryString(value: QueryValue) {
  const text = Array.isArray(value) ? value[0] : value
  return text?.trim() || ''
}

export function readQueryNumber(value: QueryValue, fallback: number) {
  const text = readQueryString(value)
  if (!text) {
    return fallback
  }

  const parsed = Number(text)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback
}
