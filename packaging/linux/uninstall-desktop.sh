#!/bin/sh
set -eu

data_home=${XDG_DATA_HOME:-"$HOME/.local/share"}
applications_dir="$data_home/applications"
desktop_file="$applications_dir/servanda.desktop"

if [ -f "$desktop_file" ]; then
    rm -f -- "$desktop_file"
fi

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$applications_dir" >/dev/null 2>&1 || true
fi

printf '%s\n' "Usunięto skrót Servandy. Katalog programu pozostał bez zmian."
