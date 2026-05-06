#!/usr/bin/env bash
set -euo pipefail

tmpdir="$(mktemp -d)"

cleanup() {
  rm -rf "$tmpdir"
}
trap cleanup EXIT

generate_actor() {
  local actor="$1"
  local prefix="$2"
  local key="$tmpdir/$actor.key"

  openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$key" >/dev/null 2>&1

  local private_key
  private_key="$(openssl pkcs8 -topk8 -nocrypt -in "$key" -outform DER | base64 | tr -d '\n')"

  local public_key
  public_key="$(openssl rsa -in "$key" -pubout -outform DER 2>/dev/null | base64 | tr -d '\n')"

  printf '%s_INTERNAL_PRIVATE_KEY=%s\n' "$prefix" "$private_key"
  printf '%s_INTERNAL_PUBLIC_KEY=%s\n' "$prefix" "$public_key"
}

generate_actor "api-service" "API_SERVICE"
generate_actor "asset-service" "ASSET_SERVICE"
generate_actor "openapi-service" "OPENAPI_SERVICE"
generate_actor "sync-worker" "SYNC_WORKER"
