#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_DIR="${PROJECT_ROOT}/bin/dist/linux"

echo "==> Publishing Mouser for Linux (linux-x64)..."
mkdir -p "${OUTPUT_DIR}"

dotnet publish "${PROJECT_ROOT}/src/Mouser.Desktop/Mouser.Desktop.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "${OUTPUT_DIR}"

echo "SUCCESS: Linux release published to: ${OUTPUT_DIR}/Mouser.Desktop"
