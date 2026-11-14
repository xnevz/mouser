#!/usr/bin/env bash
# Build Mouser locally and publish the artifacts as a GitHub Release.
# Usage: bash scripts/release.sh v0.1.0 [--draft]
# Needs: dotnet, gh (logged in). Android needs the SDK + JDK 17; macOS builds only on a Mac.
set -euo pipefail

TAG="${1:?usage: release.sh vX.Y.Z [--draft]}"
DRAFT="${2:-}"
[[ "$TAG" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "Tag must look like v1.2.3"; exit 1; }

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/bin/release/$TAG"
cd "$ROOT"

[ -z "$(git status --porcelain)" ] || { echo "Commit or stash your changes first."; exit 1; }
git fetch -q origin
[ -n "$(git branch -r --contains HEAD)" ] || { echo "Push HEAD to GitHub first (the tag points at it)."; exit 1; }

rm -rf "$OUT" && mkdir -p "$OUT"
FILES=()

echo "==> Windows"
bash scripts/package-windows.sh
cp bin/dist/windows/Mouser.exe "$OUT/Mouser-windows-x64.exe"
FILES+=("$OUT/Mouser-windows-x64.exe")

echo "==> Linux"
bash scripts/package-linux.sh
tar -czf "$OUT/Mouser-linux-x64.tar.gz" -C bin/dist/linux Mouser # tar keeps the executable bit
FILES+=("$OUT/Mouser-linux-x64.tar.gz")

if [ "$(uname)" = "Darwin" ]; then
    echo "==> macOS"
    ARCH="osx-$([ "$(uname -m)" = "arm64" ] && echo arm64 || echo x64)"
    bash scripts/package-macos-app.sh "$ARCH"
    tar -czf "$OUT/Mouser-macos-$ARCH.tar.gz" -C bin/dist/macos Mouser.app
    FILES+=("$OUT/Mouser-macos-$ARCH.tar.gz")
else
    echo "==> macOS skipped (needs a Mac)"
fi

echo "==> Android"
# Debug build on purpose: the release build is unsigned, and Android refuses to install unsigned APKs.
(cd android && ./gradlew assembleDebug --console=plain)
cp android/app/build/outputs/apk/debug/app-debug.apk "$OUT/Mouser-android.apk"
FILES+=("$OUT/Mouser-android.apk")

(cd "$OUT" && sha256sum -- * > SHA256SUMS.txt)
FILES+=("$OUT/SHA256SUMS.txt")

echo "==> Publishing $TAG"
gh release create "$TAG" "${FILES[@]}" --target "$(git rev-parse HEAD)" --title "Mouser $TAG" --generate-notes $DRAFT
echo "Done: $(gh release view "$TAG" --json url -q .url)"
