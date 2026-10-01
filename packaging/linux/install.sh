#!/usr/bin/env bash
# Installs Lithophane Maker for the current user (no root needed):
#   app files  -> ~/.local/share/lithophane-maker
#   launcher   -> ~/.local/bin/lithophane-maker
#   menu entry, icons and the .lipo file type -> ~/.local/share/{applications,icons,mime}
#
# Usage:
#   dotnet publish -c Release -r linux-x64 --self-contained -o publish/linux
#   packaging/linux/install.sh publish/linux
#   packaging/linux/install.sh --uninstall
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
data="${XDG_DATA_HOME:-$HOME/.local/share}"
app_dir="$data/lithophane-maker"
bin_link="$HOME/.local/bin/lithophane-maker"
sizes=(16 24 32 48 64 128 256)

refresh() {
    update-desktop-database "$data/applications" 2>/dev/null || true
    update-mime-database "$data/mime" 2>/dev/null || true
    gtk-update-icon-cache -q -t "$data/icons/hicolor" 2>/dev/null || true
}

if [[ "${1:-}" == "--uninstall" ]]; then
    rm -rf "$app_dir"
    rm -f "$bin_link" "$data/applications/lithophane-maker.desktop" "$data/mime/packages/lithophane-maker.xml"
    rm -f "$data/icons/hicolor/scalable/apps/lithophane-maker.svg"
    for s in "${sizes[@]}"; do rm -f "$data/icons/hicolor/${s}x${s}/apps/lithophane-maker.png"; done
    refresh
    echo "Lithophane Maker uninstalled."
    exit 0
fi

src="${1:-}"
if [[ -z "$src" || ! -x "$src/LithophaneGUI" ]]; then
    echo "usage: $0 <publish-folder>   (the folder containing the LithophaneGUI executable)" >&2
    echo "       $0 --uninstall" >&2
    exit 1
fi

rm -rf "$app_dir"
mkdir -p "$app_dir" "$(dirname "$bin_link")"
cp -r "$src"/. "$app_dir"/
ln -sf "$app_dir/LithophaneGUI" "$bin_link"

install -Dm644 "$here/lithophane-maker.desktop" "$data/applications/lithophane-maker.desktop"
# Point the menu entry at the installed binary so it works even if ~/.local/bin is not on PATH.
sed -i "s|^Exec=.*|Exec=\"$app_dir/LithophaneGUI\" %F|" "$data/applications/lithophane-maker.desktop"
install -Dm644 "$here/lithophane-maker.xml" "$data/mime/packages/lithophane-maker.xml"
install -Dm644 "$here/icons/lithophane-maker.svg" "$data/icons/hicolor/scalable/apps/lithophane-maker.svg"
for s in "${sizes[@]}"; do
    install -Dm644 "$here/icons/lithophane-maker-$s.png" "$data/icons/hicolor/${s}x${s}/apps/lithophane-maker.png"
done
refresh

echo "Lithophane Maker installed. It should now appear in your application menu."
