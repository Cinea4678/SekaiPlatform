#!/usr/bin/env bash
set -euo pipefail

API_BASE_URL="${API_BASE_URL:-http://localhost:8080}"
SMOKE_USERNAME="${SMOKE_USERNAME:-1650121748}"
SMOKE_PASSWORD="${SMOKE_PASSWORD:-${SEED_ADMIN_PASSWORD:-}}"
SMOKE_SEARCH_KEYWORD="${SMOKE_SEARCH_KEYWORD:-test}"
SMOKE_START_SYNC="${SMOKE_START_SYNC:-0}"

if [[ -z "$SMOKE_PASSWORD" ]]; then
  printf 'SMOKE_PASSWORD or SEED_ADMIN_PASSWORD is required.\n' >&2
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  printf 'jq is required for deployment smoke checks.\n' >&2
  exit 1
fi

curl_opts=(--noproxy '*' -g -fsS)
tmpdir="$(mktemp -d)"

cleanup() {
  rm -rf "$tmpdir"
}
trap cleanup EXIT

request_json() {
  local method="$1"
  local path="$2"
  local output="$3"
  local token="${4:-}"
  local body="${5:-}"

  local args=("${curl_opts[@]}" -X "$method" "$API_BASE_URL$path" -H 'Content-Type: application/json' -o "$output")
  if [[ -n "$token" ]]; then
    args+=(-H "Authorization: Bearer $token")
  fi
  if [[ -n "$body" ]]; then
    args+=(-d "$body")
  fi

  curl "${args[@]}"
}

health_headers="$tmpdir/health.headers"
health_body="$tmpdir/health.json"
curl "${curl_opts[@]}" -D "$health_headers" "$API_BASE_URL/health" -o "$health_body"
grep -qi '^X-Sekai-Trace-Id:' "$health_headers"
grep -qi 'Healthy' "$health_body"

login_body="$tmpdir/login.json"
request_json POST /api/auth/login "$login_body" "" "$(jq -nc --arg username "$SMOKE_USERNAME" --arg password "$SMOKE_PASSWORD" '{username:$username,password:$password}')"
token="$(jq -r '.access_token' "$login_body")"
tenant_id="$(jq -r '.current_tenant.id // empty' "$login_body")"
test -n "$token"
test -n "$tenant_id"

request_json GET /api/auth/session "$tmpdir/session.json" "$token"
jq -e '.current_tenant.id != null' "$tmpdir/session.json" >/dev/null

request_json GET /api/auth/tenants "$tmpdir/tenants.json" "$token"
jq -e '.tenants | length >= 1' "$tmpdir/tenants.json" >/dev/null

request_json GET /api/story-types "$tmpdir/story-types.json" "$token"
jq -e 'length >= 1' "$tmpdir/story-types.json" >/dev/null

if [[ "$SMOKE_START_SYNC" == "1" ]]; then
  request_json POST /api/sync/jobs "$tmpdir/sync-start.json" "$token" '{"source":"moesekai"}'
  jq -e '.id != null and .status != null' "$tmpdir/sync-start.json" >/dev/null
fi

request_json GET '/api/sync/jobs?limit=1' "$tmpdir/sync-list.json" "$token"
jq -e 'type == "array" or has("items")' "$tmpdir/sync-list.json" >/dev/null

request_json GET "/api/search?keyword=$SMOKE_SEARCH_KEYWORD&page=1&page_size=1" "$tmpdir/search.json" "$token"
jq -e '.items != null and .page_size == 1' "$tmpdir/search.json" >/dev/null

request_json GET '/api/stories?page=1&page_size=1' "$tmpdir/stories.json" "$token"
story_id="$(jq -r '.items[0].id // empty' "$tmpdir/stories.json")"
scenario_id="$(jq -r '.items[0].scenario_id // empty' "$tmpdir/stories.json")"
story_type="$(jq -r '.items[0].story_type // empty' "$tmpdir/stories.json")"

if [[ -n "$story_id" ]]; then
  request_json GET "/api/stories/$story_id" "$tmpdir/story.json" "$token"
  jq -e --argjson story_id "$story_id" '.id == $story_id' "$tmpdir/story.json" >/dev/null

  request_json GET "/api/stories/$story_id/source-lines" "$tmpdir/source-lines.json" "$token"
  jq -e 'type == "array"' "$tmpdir/source-lines.json" >/dev/null

  first_line_no="$(jq -r '.[0].line_no // empty' "$tmpdir/source-lines.json")"
  if [[ -n "$scenario_id" && -n "$story_type" && -n "$first_line_no" ]]; then
    import_body="$(jq -nc \
      --arg story_type "$story_type" \
      --arg scenario_id "$scenario_id" \
      --argjson line_no "$first_line_no" \
      '{items:[{story_type:$story_type,scenario_id:$scenario_id,title:"Deployment smoke import",lines:[{line_no:$line_no,text:"Deployment smoke translation"}]}]}')"
    request_json POST /api/import/translation-versions "$tmpdir/import.json" "$token" "$import_body"
    version_id="$(jq -r '.items[0].translation_version_id // empty' "$tmpdir/import.json")"
    test -n "$version_id"
    request_json GET "/api/translation-versions/$version_id" "$tmpdir/version.json" "$token"
    jq -e --argjson version_id "$version_id" '.id == $version_id' "$tmpdir/version.json" >/dev/null
    request_json GET "/api/translation-versions/$version_id/lines" "$tmpdir/translation-lines.json" "$token"
    jq -e 'length >= 1' "$tmpdir/translation-lines.json" >/dev/null
  fi
fi

printf 'Deployment smoke checks passed for %s\n' "$API_BASE_URL"
