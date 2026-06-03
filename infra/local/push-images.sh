#!/usr/bin/env bash
set -euo pipefail

# NextStep - Build & Push Docker images (frontend, backend, agents)
# Usage:
#   ./infra/local/push-images.sh
#   ./infra/local/push-images.sh path/to/.env.prod
#   ./infra/local/push-images.sh path/to/.env.prod v1.4.0
#
# Required env vars (from .env.prod):
#   - DOCKER_HUB_NAMESPACE (or DOCKER_HUB_USER)
# Optional:
#   - IMAGE_VERSION (if not passed as arg)
#   - DOCKER_BUILD_PLATFORM (ex: linux/amd64)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ENV_FILE="${1:-$SCRIPT_DIR/.env.prod}"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: env file not found: $ENV_FILE"
  exit 1
fi

set -a
# shellcheck disable=SC1090
source "$ENV_FILE"
set +a

NAMESPACE="${DOCKER_HUB_NAMESPACE:-${DOCKER_HUB_USER:-}}"
if [[ -z "$NAMESPACE" ]]; then
  echo "ERROR: DOCKER_HUB_NAMESPACE (or DOCKER_HUB_USER) is required in $ENV_FILE"
  exit 1
fi

VERSION="${2:-${IMAGE_VERSION:-}}"
if [[ -z "$VERSION" ]]; then
  VERSION="$(date -u +%Y.%m.%d-%H%M)"
fi

if git -C "$PROJECT_ROOT" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  GIT_SHA="$(git -C "$PROJECT_ROOT" rev-parse --short HEAD)"
else
  GIT_SHA="nogit"
fi

BUILD_PLATFORM="${DOCKER_BUILD_PLATFORM:-}"
if [[ -n "$BUILD_PLATFORM" ]]; then
  PLATFORM_ARGS=(--platform "$BUILD_PLATFORM")
else
  PLATFORM_ARGS=()
fi

declare -A IMAGES=(
  ["nextstep-frontend"]="frontend"
  ["nextstep-backend"]="backend"
  ["nextstep-agents"]="agents"
)

echo "Docker namespace : $NAMESPACE"
echo "Version tag      : $VERSION"
echo "Git short SHA    : $GIT_SHA"
echo

for IMAGE in "${!IMAGES[@]}"; do
  CONTEXT_DIR="${PROJECT_ROOT}/${IMAGES[$IMAGE]}"
  FULL_NAME="${NAMESPACE}/${IMAGE}"
  TAG_LATEST="${FULL_NAME}:latest"
  TAG_VERSION="${FULL_NAME}:${VERSION}"
  TAG_SHA="${FULL_NAME}:${VERSION}-${GIT_SHA}"

  echo "=================================================="
  echo "Building ${FULL_NAME} from ${CONTEXT_DIR}"

  docker build \
    "${PLATFORM_ARGS[@]}" \
    --target production \
    -t "$TAG_LATEST" \
    -t "$TAG_VERSION" \
    -t "$TAG_SHA" \
    "$CONTEXT_DIR"

  echo "Pushing tags:"
  echo "  - $TAG_LATEST"
  echo "  - $TAG_VERSION"
  echo "  - $TAG_SHA"
  docker push "$TAG_LATEST"
  docker push "$TAG_VERSION"
  docker push "$TAG_SHA"
done

echo
echo "Done. Images pushed to Docker Hub namespace: ${NAMESPACE}"
echo "Suggested deploy tag: ${VERSION}"
