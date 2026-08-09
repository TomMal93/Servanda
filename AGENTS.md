# Servanda — instrukcja dla agentów

## Stan projektu

Repozytorium jest w fazie dokumentacyjnej przed budową aplikacji od zera. Dokumentacja w `docs/` definiuje nowy produkt. W repozytorium nie ma wcześniejszej implementacji ani danych do migracji.

Nowe rozwiązanie .NET powstaje dopiero zgodnie z etapami `docs/product/roadmap.md`.

## Czytaj tylko potrzebny kontekst

- routing dokumentacji: `docs/README.md`
- wizja i zakres pierwszego wydania: `docs/product/product-vision.md`, `docs/product/current-scope.md`
- kolejność budowy: `docs/product/roadmap.md`
- ekran główny i obszary: `docs/product/features/areas-dashboard.md`
- katalog narzędzi v2: `docs/product/features/tool-catalog.md`
- biblioteka promptów i Prompt Studio v2: `docs/product/features/prompt-library.md`
- edytory i zapis: `docs/product/features/content-editors.md`
- system interfejsu: `docs/design/ui-system.md`
- SQLite, encje, eksport i kopie od v2: `docs/engineering/data-model.md`
- architektura i decyzje: `docs/engineering/technical-decisions.md`, `docs/engineering/adr/`
- uruchomienie na Linuksie: `docs/engineering/linux-deployment.md`
- bezpieczeństwo lokalnego hosta i przeglądarki: `docs/engineering/security-model.md`
- testy, dostępność i wydajność: `docs/engineering/quality-requirements.md`
- struktura rozwiązania i konwencje: `docs/engineering/code-conventions.md`

Nie wczytuj całego `docs/`, gdy zadanie dotyczy jednego obszaru.

## Docelowa mapa rozwiązania

Do utworzenia podczas P1; odpowiedzialności danych pozostają puste do etapów v2:

| Odpowiedzialność | Docelowa ścieżka |
|---|---|
| host, konfiguracja i komponenty Razor | `src/Servanda.App/` |
| przypadki użycia v2 | `src/Servanda.Application/` |
| model i reguły domenowe v2 | `src/Servanda.Domain/` |
| XDG/runtime w v1; EF Core, SQLite, kopie i eksport od v2 | `src/Servanda.Infrastructure/` |
| testy domeny | `tests/Servanda.Domain.Tests/` |
| testy aplikacyjne | `tests/Servanda.Application.Tests/` |
| testy infrastruktury | `tests/Servanda.Infrastructure.Tests/` |
| testy przepływów użytkownika | `tests/Servanda.E2E/` |

Po bootstrapie zaktualizuj tabelę, jeżeli rzeczywiste ścieżki są inne. Kod, migracje i testy staną się źródłem szczegółów wdrożenia, ale nie mogą po cichu zmieniać kontraktów dokumentacji.

## Reguły pracy

1. Zachowaj jedno normatywne źródło każdego kontraktu zgodnie z `docs/README.md`.
2. Produkt pozostaje prywatnym narzędziem `local-first` dla jednego użytkownika na Linuksie.
3. Nie dodawaj kont, synchronizacji, LAN ani publicznego hostingu bez zmiany zakresu i ADR.
4. Nie dodawaj funkcji, których dokumentacja nowego projektu nie wymaga.
5. Zmiana modelu wymaga aktualizacji domeny, konfiguracji EF Core, migracji, testów migracji, eksportu/importu i `data-model.md`.
6. Migracja, import i operacja masowo destrukcyjna wymagają odzyskiwalnej, zweryfikowanej kopii.
7. Nie usuwaj bazy, kopii ani eksportów bez jawnego zakresu i ochrony danych.
8. Istotną zmianę zachowania dopisz do specyfikacji funkcji i jej kryteriów akceptacji.
9. Decyzję zmieniającą magazyn, bezpieczeństwo, dystrybucję lub wymagającą kosztownej migracji zapisz w ADR.
10. Po utworzeniu kodu uruchamiaj co najmniej `dotnet format --verify-no-changes`, `dotnet build` i `dotnet test`.
11. Zmianę UI sprawdzaj klawiaturą oraz w szerokościach 1024, 1280, 1440 i 1920 px.
12. Przejście buildu i testów jednostkowych nie dowodzi bezpieczeństwa migracji, odzyskania ani poprawności przepływu E2E.
13. Nie używaj produkcyjnego katalogu XDG użytkownika w testach lub trybie deweloperskim.
14. W pierwszym wydaniu host MUSI pozostać na loopbacku, a pakiet użytkowy MUSI działać bez Node, npm i SDK .NET.
15. Pierwsze wydanie jest przeznaczone wyłącznie na komputery osobiste i laptopy; nie projektuj ani nie testuj wersji na telefony i tablety bez zmiany zakresu.
