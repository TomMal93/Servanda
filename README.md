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

Host wybiera dynamiczny port IPv4 wyłącznie na loopbacku. Launcher potwierdza prywatny deskryptor istniejącej instancji albo uruchamia host, czeka na stan `ready`, pobiera jednorazowy bilet prywatnym sekretem i otwiera procesową sesję przeglądarki. Chroniona akcja „Zamknij Servandę” wymaga potwierdzenia w interfejsie, sesji procesu, dokładnego originu i tokenu antiforgery.

### Artefakt użytkowy Linux x64

Profil wydaniowy tworzy przenośny, samowystarczalny katalog bez zależności od SDK lub systemowego runtime'u .NET:

```bash
dotnet restore src/Servanda.App/Servanda.App.csproj -r linux-x64
dotnet publish src/Servanda.App/Servanda.App.csproj -p:PublishProfile=linux-x64 --no-restore
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

## Ochrona danych

Przyszłe bazy, kopie i eksporty są danymi użytkownika. Ich usunięcie wymaga osobnego, jawnego zadania i mechanizmu ochronnego zgodnego z dokumentacją.

Instrukcje dla agentów: [AGENTS.md](./AGENTS.md).
