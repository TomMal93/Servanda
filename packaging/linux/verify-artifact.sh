#!/bin/sh
set -eu

if [ "$#" -ne 1 ]; then
    printf '%s\n' "Użycie: verify-artifact.sh <katalog-Servanda-linux-x64>" >&2
    exit 64
fi

artifact_dir=$1
if [ ! -d "$artifact_dir" ]; then
    printf '%s\n' "Nie znaleziono katalogu artefaktu: $artifact_dir" >&2
    exit 1
fi

artifact_dir=$(CDPATH= cd -- "$artifact_dir" && pwd)
if [ "$(basename -- "$artifact_dir")" != "Servanda-linux-x64" ]; then
    printf '%s\n' "Katalog artefaktu musi nazywać się Servanda-linux-x64." >&2
    exit 1
fi

for required_file in \
    Servanda \
    libcoreclr.so \
    libhostfxr.so \
    launcher-error.html \
    install-desktop.sh \
    uninstall-desktop.sh \
    servanda.desktop.in
do
    if [ ! -f "$artifact_dir/$required_file" ]; then
        printf '%s\n' "Brak wymaganego pliku artefaktu: $required_file" >&2
        exit 1
    fi
done

if [ ! -x "$artifact_dir/Servanda" ]; then
    printf '%s\n' "Plik Servanda nie jest wykonywalny." >&2
    exit 1
fi

temporary_dir=$(mktemp -d "${TMPDIR:-/tmp}/servanda-artifact-test.XXXXXX")
host_pid=
cleanup()
{
    if [ -n "$host_pid" ] && kill -0 "$host_pid" 2>/dev/null; then
        kill "$host_pid" 2>/dev/null || true
        wait "$host_pid" 2>/dev/null || true
    fi

    rm -rf -- "$temporary_dir"
}
trap cleanup EXIT HUP INT TERM

mkdir -p -- "$temporary_dir/home" "$temporary_dir/runtime" "$temporary_dir/state"
chmod 700 "$temporary_dir/home" "$temporary_dir/runtime" "$temporary_dir/state"

env -i \
    HOME="$temporary_dir/home" \
    PATH=/usr/bin:/bin \
    DOTNET_MULTILEVEL_LOOKUP=0 \
    DOTNET_ROOT="$temporary_dir/no-system-runtime" \
    XDG_RUNTIME_DIR="$temporary_dir/runtime" \
    XDG_STATE_HOME="$temporary_dir/state" \
    "$artifact_dir/Servanda" --host >"$temporary_dir/host.stdout" 2>"$temporary_dir/host.stderr" &
host_pid=$!

descriptor_path="$temporary_dir/runtime/servanda/instance.json"
attempt=0
while [ "$attempt" -lt 200 ]; do
    if [ -f "$descriptor_path" ] && grep -q '"state": "ready"' "$descriptor_path"; then
        break
    fi

    if ! kill -0 "$host_pid" 2>/dev/null; then
        printf '%s\n' "Host z artefaktu zakończył się przed publikacją ready." >&2
        cat "$temporary_dir/host.stderr" >&2
        exit 1
    fi

    attempt=$((attempt + 1))
    sleep 0.05
done

if [ ! -f "$descriptor_path" ] || ! grep -q '"state": "ready"' "$descriptor_path"; then
    printf '%s\n' "Host z artefaktu nie opublikował stanu ready w limicie czasu." >&2
    exit 1
fi

if ! grep -q '"origin": "http://127\.0\.0\.1:' "$descriptor_path"; then
    printf '%s\n' "Artefakt nie opublikował originu IPv4 loopback." >&2
    exit 1
fi

kill "$host_pid"
wait "$host_pid"
host_pid=

if [ -e "$descriptor_path" ]; then
    printf '%s\n' "Host nie usunął deskryptora po łagodnym zatrzymaniu." >&2
    exit 1
fi

printf '%s\n' "Artefakt Servanda-linux-x64 przeszedł test uruchomieniowy."
