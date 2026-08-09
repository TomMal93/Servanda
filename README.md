# Servanda

Prywatna, lokalna pamięć zewnętrzna dla ważnych spraw codzienności. Projekt zakończył etap kompletowania specyfikacji D0 i rozpoczął etap P1, obejmujący budowę fundamentu hosta v1.

## Aktualny stan

- Dokumentacja w `docs/` jest źródłem wymagań nowego projektu, a etap D0 specyfikacji jest ukończony.
- Aktywnym etapem jest P1: fundament bezpiecznego, bezstanowego hosta v1 i uruchomienia na Linuksie.
- Docelowy stos to .NET 10 LTS (`net10.0`), ASP.NET Core 10, Blazor Web App Interactive Server, EF Core i SQLite.
- Linux jest podstawową platformą.
- Pierwsze wydanie jest przeznaczone wyłącznie na komputery osobiste i laptopy; telefony i tablety są poza zakresem.
- Wydanie użytkowe będzie uruchamiane z menu aplikacji przez launcher i skrót `.desktop`.
- V1 jest bezstanową powłoką ze statycznym pulpitem planowanych obszarów. Moduły, SQLite, migracje, kopie, recovery, import i eksport rozpoczynają się w v2.

Kolejność prac definiuje [roadmapa](./docs/product/roadmap.md), a granice pierwszego wydania — [bieżący zakres](./docs/product/current-scope.md).

## Dokumentacja

Punkt wejścia, routing i źródła prawdy: [docs/README.md](./docs/README.md).

Najważniejsze dokumenty:

- [wizja produktu](./docs/product/product-vision.md),
- [obszary i ekran główny](./docs/product/features/areas-dashboard.md),
- [model danych](./docs/engineering/data-model.md),
- [architektura](./docs/engineering/technical-decisions.md),
- [uruchomienie na Linuksie](./docs/engineering/linux-deployment.md),
- [wymagania jakościowe](./docs/engineering/quality-requirements.md).

## Uruchomienie deweloperskie

Rozwiązanie wymaga przypiętego w `global.json` SDK .NET 10. Tryb deweloperski używa jawnie odizolowanych katalogów XDG w repozytorium, aby nigdy nie dotknąć produkcyjnego runtime'u użytkownika:

```bash
dotnet restore
mkdir -p .servanda-dev/runtime .servanda-dev/state
chmod 700 .servanda-dev/runtime .servanda-dev/state
XDG_RUNTIME_DIR="$PWD/.servanda-dev/runtime" \
XDG_STATE_HOME="$PWD/.servanda-dev/state" \
dotnet run --project src/Servanda.App
```

Host wybiera dynamiczny port IPv4 wyłącznie na loopbacku. Launcher potwierdza prywatny deskryptor istniejącej instancji albo uruchamia host, czeka na stan `ready`, pobiera jednorazowy bilet prywatnym sekretem i otwiera procesową sesję przeglądarki. Chroniona akcja „Zamknij Servandę” wymaga potwierdzenia w interfejsie, sesji procesu, dokładnego originu i tokenu antiforgery. Publikacja `self-contained` oraz wpis `.desktop` pozostają w trakcie implementacji P1.

### Otwieranie przeglądarki przy izolowanym runtime deweloperskim

Aktualny launcher przekazuje przeglądarce zmienną `XDG_RUNTIME_DIR` ustawioną dla Servandy. Gdy wskazuje ona `.servanda-dev/runtime`, Firefox lub systemowy mechanizm otwierania adresów może utracić dostęp do sesyjnego D-Bus w `/run/user/<UID>` i zakończyć się komunikatem podobnym do `Failed to synchronize with dbus proxy`. Host może mimo tego pozostać gotowy; potwierdza to prywatny deskryptor `.servanda-dev/runtime/servanda/instance.json`.

Do czasu poprawienia separacji środowiska launchera działającą instancję można bezpiecznie otworzyć w Firefoksie następująco:

```bash
runtime="$PWD/.servanda-dev/runtime/servanda"
origin=$(jq -r .origin "$runtime/instance.json")
control=$(base64 -w0 "$runtime/control.secret")
ticket=$(curl -fsS -X POST \
  -H "X-Servanda-Control: $control" \
  "$origin/launcher/ticket" | jq -r .ticket)

XDG_RUNTIME_DIR="/run/user/$(id -u)" \
firefox --new-tab "$origin/bootstrap#ticket=$ticket"
```

Nie należy otwierać samego originu z deskryptora. Bez jednorazowego biletu przeglądarka nie otrzyma wymaganej sesji procesu. Polecenie zachowuje izolowany runtime hosta, a systemowy `XDG_RUNTIME_DIR` przywraca wyłącznie procesowi przeglądarki.

Minimalna weryfikacja:

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build --no-restore
dotnet test --no-build
```

## Ochrona danych

Przyszłe bazy, kopie i eksporty są danymi użytkownika. Ich usunięcie wymaga osobnego, jawnego zadania i mechanizmu ochronnego zgodnego z dokumentacją.

Instrukcje dla agentów: [AGENTS.md](./AGENTS.md).
