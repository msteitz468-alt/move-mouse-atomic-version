#!/usr/bin/env bash
# ============================================================
# build-deb.sh — Build a self-contained Debian .deb package
#                for Move Mouse Linux
#
# Prerequisites (install with apt):
#   dotnet-sdk-8.0   debhelper   dpkg-dev
#
# Usage:
#   chmod +x build-deb.sh
#   ./build-deb.sh
#
# Output:
#   ../move-mouse_4.0.0-1_amd64.deb
# ============================================================

set -euo pipefail

PACKAGE="move-mouse"
VERSION="4.0.0"
REVISION="1"
ARCH="amd64"
RUNTIME="linux-x64"
PKGDIR="$(pwd)/pkg/${PACKAGE}_${VERSION}-${REVISION}_${ARCH}"

echo "==> Cleaning previous build..."
rm -rf pkg/
mkdir -p "${PKGDIR}/usr/lib/${PACKAGE}"
mkdir -p "${PKGDIR}/usr/bin"
mkdir -p "${PKGDIR}/usr/share/applications"
mkdir -p "${PKGDIR}/DEBIAN"

echo "==> Publishing .NET 8 self-contained single-file binary..."
dotnet publish MoveMouseLinux.csproj \
    --configuration Release \
    --runtime "${RUNTIME}" \
    --self-contained true \
    --output "${PKGDIR}/usr/lib/${PACKAGE}" \
    -p:PublishSingleFile=true \
    -p:DebugType=none \
    -p:EnableCompressionInSingleFile=true

echo "==> Creating /usr/bin symlink..."
ln -sf "/usr/lib/${PACKAGE}/${PACKAGE}" \
    "${PKGDIR}/usr/bin/${PACKAGE}"

echo "==> Installing .desktop entry..."
cp debian/move-mouse.desktop \
    "${PKGDIR}/usr/share/applications/${PACKAGE}.desktop"

echo "==> Writing DEBIAN/control..."
cat > "${PKGDIR}/DEBIAN/control" <<EOF
Package: ${PACKAGE}
Version: ${VERSION}-${REVISION}
Architecture: ${ARCH}
Maintainer: Move Mouse <contact@movemouse.co.uk>
Depends: libx11-6, xdotool, xprintidle, wmctrl
Recommends: libayatana-appindicator3-1
Section: utils
Priority: optional
Homepage: http://www.movemouse.co.uk
Description: Automate mouse movement and keyboard actions on Linux
 Move Mouse periodically moves the cursor and simulates keyboard input
 to prevent screensavers and idle timeouts. Supports scheduled start/stop,
 custom actions, and blackout periods.
 .
 Built with .NET 8 and Avalonia UI. Uses xdotool for X11 input simulation.
EOF

echo "==> Writing DEBIAN/postinst..."
cat > "${PKGDIR}/DEBIAN/postinst" <<'EOF'
#!/bin/sh
set -e
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database -q /usr/share/applications || true
fi
EOF
chmod 0755 "${PKGDIR}/DEBIAN/postinst"

echo "==> Setting permissions..."
find "${PKGDIR}" -type f -exec chmod 644 {} \;
find "${PKGDIR}" -type d -exec chmod 755 {} \;
chmod 755 "${PKGDIR}/usr/lib/${PACKAGE}/${PACKAGE}"
chmod 755 "${PKGDIR}/DEBIAN/postinst"

echo "==> Building .deb package..."
dpkg-deb --build --root-owner-group \
    "${PKGDIR}" \
    "${PACKAGE}_${VERSION}-${REVISION}_${ARCH}.deb"

echo ""
echo "Done! Package created: ${PACKAGE}_${VERSION}-${REVISION}_${ARCH}.deb"
echo ""
echo "Install with:"
echo "  sudo apt install ./${PACKAGE}_${VERSION}-${REVISION}_${ARCH}.deb"
echo ""
echo "Runtime dependencies installed automatically:"
echo "  sudo apt install xdotool xprintidle wmctrl"
