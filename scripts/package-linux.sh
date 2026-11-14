#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_DIR="${PROJECT_ROOT}/bin/dist/linux"
APPDIR="${OUTPUT_DIR}/AppDir"

echo "==> Publishing Mouser for Linux (linux-x64)..."
mkdir -p "${OUTPUT_DIR}"

dotnet publish "${PROJECT_ROOT}/src/Mouser.Desktop/Mouser.Desktop.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "${OUTPUT_DIR}"

echo "SUCCESS: Linux release published to: ${OUTPUT_DIR}/Mouser"

echo "==> Preparing AppDir for AppImage packaging..."
mkdir -p "${APPDIR}/usr/bin"
mkdir -p "${APPDIR}/usr/share/applications"
mkdir -p "${APPDIR}/usr/share/icons/hicolor/256x256/apps"

cp "${OUTPUT_DIR}/Mouser" "${APPDIR}/usr/bin/Mouser"
cp "${PROJECT_ROOT}/installer/linux/mouser.desktop" "${APPDIR}/usr/share/applications/mouser.desktop"
cp "${PROJECT_ROOT}/installer/linux/mouser.desktop" "${APPDIR}/mouser.desktop"

# Create AppRun symlink / wrapper
cat << 'EOF' > "${APPDIR}/AppRun"
#!/bin/sh
HERE="$(dirname "$(readlink -f "${0}")")"
exec "${HERE}/usr/bin/Mouser" "$@"
EOF
chmod +x "${APPDIR}/AppRun"

if command -v appimagetool >/dev/null 2>&1; then
    echo "==> Building Mouser-x86_64.AppImage with appimagetool..."
    appimagetool "${APPDIR}" "${OUTPUT_DIR}/Mouser-x86_64.AppImage"
    echo "SUCCESS: AppImage generated at: ${OUTPUT_DIR}/Mouser-x86_64.AppImage"
else
    echo "NOTE: 'appimagetool' not found on PATH. AppDir structure is prepared at: ${APPDIR}"
fi
