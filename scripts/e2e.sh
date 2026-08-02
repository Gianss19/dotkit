#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SAMPLE_SRC="$REPO_DIR/tests/e2e/MyApi"
ARTIFACTS="$REPO_DIR/artifacts"

NUPKG="$(find "$ARTIFACTS" -maxdepth 1 -name 'dotkit.*.nupkg' | sort | head -n 1)"
if [[ -z "$NUPKG" ]]; then
  echo "ERROR: no dotkit.*.nupkg found in $ARTIFACTS" >&2
  exit 1
fi
VERSION="$(basename "$NUPKG" | sed -E 's/^dotkit\.(.*)\.nupkg$/\1/')"

echo "==> dotkit $VERSION ($NUPKG)"

# The runner may not have a native .NET 6 runtime; roll forward to a newer one.
export DOTNET_ROLL_FORWARD=Major

WORK="$(mktemp -d)"
trap 'pkill -f MyApi 2>/dev/null || true; rm -rf "$WORK"' EXIT

echo "==> Installing dotkit tool (local tool-path)"
dotnet tool install dotkit --tool-path "$WORK/tool" --add-source "$ARTIFACTS" --version "$VERSION"

declare -A MAJOR_OF=( [net6.0]=6 [net8.0]=8 [net10.0]=10 )
PORT_BASE=5000

for TFM in net6.0 net8.0 net10.0; do
  MAJOR="${MAJOR_OF[$TFM]}"
  PORT=$((PORT_BASE + MAJOR * 10))
  API_DIR="$WORK/api-$TFM"

  cp -r "$SAMPLE_SRC" "$API_DIR"
  sed -i "s#<TargetFramework>net8.0</TargetFramework>#<TargetFramework>$TFM</TargetFramework>#" "$API_DIR/MyApi.csproj"

  echo "==> [$TFM] Running dotkit install --no-user-secrets"
  "$WORK/tool/dotkit" install --project "$API_DIR" --no-user-secrets

  echo "==> [$TFM] Verifying JwtBearer major $MAJOR"
  VER="$(grep -oP 'Microsoft.AspNetCore.Authentication.JwtBearer" Version="\K[^"]+' "$API_DIR/MyApi.csproj")"
  case "$VER" in
    "$MAJOR".*) echo "    OK: JwtBearer $VER" ;;
    *) echo "ERROR: expected JwtBearer $MAJOR.* but found '$VER'" >&2; exit 1 ;;
  esac

  echo "==> [$TFM] Building"
  dotnet build "$API_DIR" -c Release --nologo -v q

  echo "==> [$TFM] Running app on port $PORT"
  ASPNETCORE_URLS="http://127.0.0.1:$PORT" dotnet run --project "$API_DIR" -c Release --no-build --nologo &
  APP_PID=$!

  ready=0
  for _ in $(seq 1 90); do
    if curl -sf -o /dev/null "http://127.0.0.1:$PORT/token"; then
      ready=1
      break
    fi
    sleep 1
  done
  if [[ $ready -ne 1 ]]; then
    echo "ERROR: [$TFM] app did not become ready on port $PORT" >&2
    kill "$APP_PID" 2>/dev/null || true
    exit 1
  fi

  code_token="$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:$PORT/token")"
  [[ "$code_token" == "200" ]] || { echo "ERROR: [$TFM] /token returned $code_token (expected 200)" >&2; exit 1; }
  echo "    OK: /token -> $code_token"

  code_no_token="$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:$PORT/protected")"
  [[ "$code_no_token" == "401" ]] || { echo "ERROR: [$TFM] /protected without token returned $code_no_token (expected 401)" >&2; exit 1; }
  echo "    OK: /protected (no token) -> $code_no_token"

  TOKEN="$(curl -s "http://127.0.0.1:$PORT/token" | sed -E 's/.*"token":"([^"]+)".*/\1/')"
  [[ -n "$TOKEN" ]] || { echo "ERROR: [$TFM] /token did not return a JWT" >&2; exit 1; }
  code_with_token="$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $TOKEN" "http://127.0.0.1:$PORT/protected")"
  [[ "$code_with_token" == "200" ]] || { echo "ERROR: [$TFM] /protected with token returned $code_with_token (expected 200)" >&2; exit 1; }
  echo "    OK: /protected (with token) -> $code_with_token"

  kill "$APP_PID" 2>/dev/null || true
  pkill -f MyApi 2>/dev/null || true
  echo "==> [$TFM] PASSED"
done

echo "ALL E2E PASSED"
