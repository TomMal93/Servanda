# Servanda

Servanda to lokalna aplikacja webowa do tworzenia i organizowania prywatnych notatek. Interfejs działa w przeglądarce, a dane są zapisywane przez lokalny backend do pliku SQLite na komputerze użytkownika.

## Status

Projekt jest na etapie przygotowania architektury. Szkielet aplikacji i polecenia opisane poniżej zostaną dodane w kolejnym kroku implementacji.

## Stack

- React, TypeScript i Vite;
- .NET 10 oraz ASP.NET Core Minimal API;
- Entity Framework Core;
- SQLite;
- Vitest, xUnit i Playwright.

Pełny opis architektury, zasad pracy i modelu danych znajduje się w [przewodniku developerskim](docs/development-guide.md).

## Wymagania

- .NET 10 SDK;
- Node.js w wersji wskazanej przez przyszły plik `.nvmrc` lub pole `engines` w `package.json`;
- npm.

## Uruchomienie

Po utworzeniu szkieletu projektu pierwsze uruchomienie będzie wyglądać następująco:

```bash
npm install
npm run dev
```

`npm run dev` uruchomi jednocześnie frontend Vite oraz backend ASP.NET Core. Planowane adresy developerskie:

- frontend: `http://localhost:5173`;
- API: `http://localhost:5180`;
- baza: `data/servanda.db`.

Frontend korzysta ze względnych ścieżek `/api`, przekazywanych do backendu przez proxy Vite.

## Planowane polecenia

```bash
npm run dev       # frontend i backend w trybie obserwowania zmian
npm test          # testy frontendu i backendu
npm run build     # produkcyjny build całego rozwiązania
```

Do czasu utworzenia `package.json` polecenia te stanowią kontrakt docelowy i nie są jeszcze dostępne.

## Dane lokalne

Plik bazy danych oraz jego pliki pomocnicze nie są wersjonowane. Testy automatyczne nie mogą korzystać z bazy użytkownika. Zmiany schematu będą zarządzane przez migracje EF Core.

## Współpraca z agentem

Instrukcje operacyjne dla agentów znajdują się w [`AGENTS.md`](AGENTS.md). Decyzje architektoniczne i kryteria ukończenia zmian opisuje [przewodnik developerski](docs/development-guide.md).

