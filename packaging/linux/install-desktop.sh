#!/bin/sh
set -eu

package_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
executable="$package_dir/Servanda"
template="$package_dir/servanda.desktop.in"

if [ ! -x "$executable" ]; then
    printf '%s\n' "Brak wykonywalnego pliku Servanda w katalogu pakietu." >&2
    exit 1
fi

data_home=${XDG_DATA_HOME:-"$HOME/.local/share"}
applications_dir="$data_home/applications"
desktop_file="$applications_dir/servanda.desktop"

mkdir -p -- "$applications_dir"

escaped_executable=$(printf '%s' "$executable" | sed 's/[\\"`$]/\\&/g')
sed "s|@EXECUTABLE@|\"$escaped_executable\"|" "$template" > "$desktop_file.tmp"
chmod 644 "$desktop_file.tmp"
mv -f -- "$desktop_file.tmp" "$desktop_file"

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$applications_dir" >/dev/null 2>&1 || true
fi

printf '%s\n' "Zainstalowano skrót Servandy: $desktop_file"
