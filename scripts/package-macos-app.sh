#!/usr/bin/env bash
set -euo pipefail

# Mouser macOS Application Bundling, Signing & Notarization Script
echo "=========================================================="
echo " Packaging Mouser for macOS (Hardened Runtime & DMG)       "
echo "=========================================================="

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_DIR="${PROJECT_ROOT}/bin/dist/macos"
APP_NAME="Mouser"
APP_BUNDLE="${OUTPUT_DIR}/${APP_NAME}.app"
CONTENTS="${APP_BUNDLE}/Contents"

rm -rf "${OUTPUT_DIR}"
mkdir -p "${CONTENTS}/MacOS"
mkdir -p "${CONTENTS}/Frameworks"
mkdir -p "${CONTENTS}/Resources"

# 1. Build low-level native dynamic library
echo "==> 1. Compiling libmouser_native.dylib with CMake..."
mkdir -p "${PROJECT_ROOT}/src/Mouser.Native/build"
pushd "${PROJECT_ROOT}/src/Mouser.Native/build" > /dev/null
cmake .. -DCMAKE_BUILD_TYPE=Release
cmake --build . --config Release
popd > /dev/null

cp "${PROJECT_ROOT}/bin/native/libmouser_native.dylib" "${CONTENTS}/Frameworks/"

# 2. Publish .NET 8 desktop binary (Self-contained, osx-x64/osx-arm64)
ARCH="${1:-osx-x64}"
echo "==> 2. Publishing .NET Desktop application for target: ${ARCH}..."
dotnet publish "${PROJECT_ROOT}/src/Mouser.Desktop/Mouser.Desktop.csproj" \
    -c Release \
    -r "${ARCH}" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "${OUTPUT_DIR}/publish"

cp "${OUTPUT_DIR}/publish/Mouser" "${CONTENTS}/MacOS/${APP_NAME}"
chmod +x "${CONTENTS}/MacOS/${APP_NAME}"

# 3. Create Info.plist with TCC permissions (Accessibility & Input Monitoring)
echo "==> 3. Generating Info.plist with security descriptors..."
cat << 'EOF' > "${CONTENTS}/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>Mouser</string>
    <key>CFBundleDisplayName</key>
    <string>Mouser</string>
    <key>CFBundleIdentifier</key>
    <string>com.mouser.kvm</string>
    <key>CFBundleVersion</key>
    <string>1.0.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleExecutable</key>
    <string>Mouser</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSAccessibilityUsageDescription</key>
    <string>Mouser requires Accessibility access to capture mouse movements across screen boundaries and simulate inputs seamlessly across physical computers.</string>
    <key>NSInputMonitoringUsageDescription</key>
    <string>Mouser requires Input Monitoring access to capture keyboard shortcuts and modifiers for KVM sharing.</string>
</dict>
</plist>
EOF

# 4. Generate Hardened Runtime Entitlements
echo "==> 4. Generating Mouser.entitlements..."
cat << 'EOF' > "${OUTPUT_DIR}/Mouser.entitlements"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.security.cs.allow-jit</key>
    <true/>
    <key>com.apple.security.cs.allow-unsigned-executable-memory</key>
    <true/>
    <key>com.apple.security.cs.disable-library-validation</key>
    <true/>
</dict>
</plist>
EOF

# 5. Ad-hoc or Developer ID Signing
SIGNING_IDENTITY="${APPLE_SIGNING_IDENTITY:--}"
echo "==> 5. Codesigning bundle (Identity: ${SIGNING_IDENTITY})..."
codesign --force --timestamp --options runtime \
    --sign "${SIGNING_IDENTITY}" \
    "${CONTENTS}/Frameworks/libmouser_native.dylib"

codesign --force --timestamp --options runtime \
    --entitlements "${OUTPUT_DIR}/Mouser.entitlements" \
    --sign "${SIGNING_IDENTITY}" \
    "${APP_BUNDLE}"

# 6. Create DMG container
echo "==> 6. Creating DMG installer disk image..."
DMG_PATH="${OUTPUT_DIR}/${APP_NAME}-${ARCH}.dmg"
rm -f "${DMG_PATH}"
hdiutil create -volname "${APP_NAME}" -srcfolder "${APP_BUNDLE}" -ov -format UDZO "${DMG_PATH}"

echo "=========================================================="
echo " SUCCESS: Mouser macOS Bundle & DMG Created!              "
echo " Bundle: ${APP_BUNDLE}                                    "
echo " DMG:    ${DMG_PATH}                                      "
echo "=========================================================="
