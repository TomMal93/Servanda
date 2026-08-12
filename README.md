# Servanda

Prywatna, lokalna pamięć zewnętrzna dla ważnych spraw codzienności. Projekt ukończył fundament hosta P1 i rozpoczął etap P2, obejmujący budowę statycznego pulpitu v1.

## Aktualny stan

- Dokumentacja w `docs/` jest źródłem wymagań nowego projektu, a etapy D0 i P1 są ukończone.
- Aktywnym etapem jest P2: system interfejsu, adaptacyjna powłoka i statyczny pulpit planowanych obszarów.
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
DOTNET_ENVIRONMENT=Development \
dotnet run --project src/Servanda.App
```

`DOTNET_ENVIRONMENT=Development` jest wymagane przy uruchamianiu z wyniku kompilacji. W przeciwnym razie ASP.NET Core nie włącza deweloperskich Static Web Assets i przeglądarka może otrzymać pusty wariant skompresowanego CSS lub JavaScript. Po zmianie środowiska wcześniej uruchomiony host trzeba zatrzymać i uruchomić ponownie.

Host wybiera dynamiczny port IPv4 wyłącznie na loopbacku. Launcher potwierdza prywatny deskryptor istniejącej instancji albo uruchamia host, czeka na stan `ready`, pobiera jednorazowy bilet prywatnym sekretem i otwiera procesową sesję przeglądarki. Chroniona akcja „Zamknij Servandę” wymaga potwierdzenia w interfejsie, sesji procesu, dokładnego originu i tokenu antiforgery.

Host zapisuje w prywatnym katalogu `XDG_STATE_HOME` ograniczony log cyklu życia bez adresów, nagłówków, sekretów i danych żądań. Log jest rotowany przy 256 KiB i zachowuje najwyżej trzy poprzednie pliki.

### Artefakt użytkowy Linux x64

Profil wydaniowy tworzy przenośny, samowystarczalny katalog bez zależności od SDK lub systemowego runtime'u .NET:

Artefakt wymaga systemu Linux x86-64 z glibc 2.27 lub nowszą. Nie jest przeznaczony dla dystrybucji używających musl ani dla architektury Arm64.

```bash
dotnet restore src/Servanda.App/Servanda.App.csproj -r linux-x64
dotnet publish src/Servanda.App/Servanda.App.csproj -p:PublishProfile=linux-x64 --no-restore
./packaging/linux/verify-artifact.sh artifacts/publish/Servanda-linux-x64
```

Artefakt powstaje w `artifacts/publish/Servanda-linux-x64/`. Można uruchomić go bezpośrednio albo zainstalować skrót w menu bieżącego użytkownika:

```bash
./artifacts/publish/Servanda-linux-x64/Servanda
./artifacts/publish/Servanda-linux-x64/install-desktop.sh
```

`uninstall-desktop.sh` usuwa wyłącznie wpis menu. Nie usuwa katalogu programu ani katalogów XDG Servandy. Po instalacji skrótu katalog artefaktu musi pozostać w tej samej lokalizacji.

Minimalna weryfikacja:

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build --no-restore
dotnet test --no-build
```

Testy przepływu opublikowanego artefaktu w przypiętych Chromium i Firefox uruchamia osobne polecenie. Przeglądarki i Playwright są zależnościami wyłącznie testowymi i nie trafiają do pakietu użytkowego:

```bash
./tests/Servanda.E2E/run-browser-tests.sh
```

## Ochrona danych

Przyszłe bazy, kopie i eksporty są danymi użytkownika. Ich usunięcie wymaga osobnego, jawnego zadania i mechanizmu ochronnego zgodnego z dokumentacją.

Instrukcje dla agentów: [AGENTS.md](./AGENTS.md).
