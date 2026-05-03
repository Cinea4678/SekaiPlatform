#!/usr/bin/env bash
set -euo pipefail

API_BASE_URL="${API_BASE_URL:-http://localhost:8080}"
curl_opts=(--noproxy '*' -g -fsS)

health_headers="$(mktemp)"
health_body="$(mktemp)"
internal_body="$(mktemp)"

cleanup() {
  rm -f "$health_headers" "$health_body" "$internal_body"
}
trap cleanup EXIT

curl "${curl_opts[@]}" -D "$health_headers" "$API_BASE_URL/health" -o "$health_body"
grep -qi '^X-Sekai-Trace-Id:' "$health_headers"
grep -qi 'Healthy' "$health_body"

curl "${curl_opts[@]}" "$API_BASE_URL/api/internal-services/health" -o "$internal_body"
grep -q '"status":"healthy"' "$internal_body"
grep -q '"service":"auth-service"' "$internal_body"
grep -q '"service":"asset-service"' "$internal_body"
grep -q '"service":"search-service"' "$internal_body"

printf 'Phase 1 smoke checks passed for %s\n' "$API_BASE_URL"
