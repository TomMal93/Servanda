# Servanda — architektura i przewodnik developerski

## Cel dokumentu

Ten dokument opisuje uzgodniony stack, docelową architekturę oraz zasady pracy nad aplikacją Servanda. Jest punktem odniesienia zarówno dla programistów, jak i agentów programistycznych.

Jeżeli implementacja odbiega od tego dokumentu, należy najpierw ustalić, czy zmieniły się wymagania, a następnie zaktualizować dokument razem z kodem.

## Cel produktu

Servanda jest lokalną aplikacją webową do przechowywania i organizowania prywatnych notatek. Interfejs działa w przeglądarce, natomiast dane są zapisywane przez lokalny backend do pliku bazy SQLite na komputerze użytkownika.

Najważniejsze założenia:

- aplikacja działa lokalnie i nie wymaga zewnętrznej usługi bazodanowej;
- użytkownik otwiera interfejs w przeglądarce;
- dane są przechowywane w fizycznym pliku SQLite;
- cały tryb developerski uruchamia jedno polecenie `npm run dev`;
- zmiany frontendu są widoczne natychmiast dzięki HMR;
- zmiany backendu są automatycznie obsługiwane przez `dotnet watch`;
- architektura powinna umożliwiać późniejsze dodanie eksportu, kopii zapasowych i ewentualnej synchronizacji, ale nie należy implementować ich przed pojawieniem się wymagania.

## Uzgodniony stack

### Frontend

- React;
- TypeScript;
- Vite;
- CSS (biblioteka komponentów lub Tailwind CSS mogą zostać dodane dopiero po osobnej decyzji);
- Vitest;
- React Testing Library;
- Playwright dla najważniejszych scenariuszy przeglądarkowych.

### Backend

- .NET 10 LTS;
- ASP.NET Core Minimal API;
- Entity Framework Core;
- provider `Microsoft.EntityFrameworkCore.Sqlite`;
- SQLite;
- xUnit.

### Narzędzia developerskie

- npm jako główny interfejs poleceń dla całego repozytorium;
- `concurrently` do równoległego uruchamiania frontendu i backendu;
- `dotnet watch` do automatycznego przeładowywania backendu;
- npm lockfile musi być przechowywany w repozytorium.

Wersje zależności JavaScript należy odczytywać z `package.json` i lockfile. Nie należy wpisywać numerów wersji w wielu dokumentach.

## Model uruchomieniowy

W trybie developerskim działają dwa procesy uruchamiane wspólnie:

```text
npm run dev
├── WEB: Vite + React       http://localhost:5173
└── API: ASP.NET Core       http://localhost:5180
                             └── SQLite: data/servanda.db
```

Frontend wysyła żądania pod ścieżki `/api/*`. Dev server Vite przekazuje je do ASP.NET Core. Kod frontendu nie powinien zawierać na sztywno adresu `http://localhost:5180`.

Docelowy główny `package.json` powinien zapewniać co najmniej następujące polecenia:

```json
{
  "scripts": {
    "dev": "concurrently -k -n WEB,API -c cyan,green \"npm run dev:web\" \"npm run dev:api\"",
    "dev:web": "npm --prefix src/Servanda.Web run dev",
    "dev:api": "dotnet watch --project src/Servanda.Api run",
    "build": "npm run build:web && dotnet build",
    "build:web": "npm --prefix src/Servanda.Web run build",
    "test": "npm run test:web && dotnet test",
    "test:web": "npm --prefix src/Servanda.Web test"
  }
}
```

Szczegóły poleceń mogą zostać rozszerzone, ale `npm run dev` pozostaje podstawowym i jedynym wymaganym poleceniem do rozpoczęcia pracy nad całą aplikacją.

## Docelowa struktura repozytorium

```text
Servanda/
├── package.json
├── package-lock.json
├── Servanda.sln
├── data/
│   └── servanda.db              # dane lokalne, poza kontrolą wersji
├── docs/
│   └── development-guide.md
├── src/
│   ├── Servanda.Api/
│   ├── Servanda.Application/
│   ├── Servanda.Domain/
│   ├── Servanda.Infrastructure/
│   └── Servanda.Web/
└── tests/
    ├── Servanda.Api.Tests/
    ├── Servanda.Application.Tests/
    └── Servanda.E2E/
```

Podział odpowiedzialności:

- `Servanda.Domain` — encje i reguły domenowe niezależne od UI, HTTP i bazy danych;
- `Servanda.Application` — przypadki użycia i kontrakty wymaganych usług;
- `Servanda.Infrastructure` — EF Core, SQLite, repozytoria i operacje na plikach;
- `Servanda.Api` — konfiguracja hosta oraz endpointy HTTP;
- `Servanda.Web` — interfejs React, routing i komunikacja z API;
- `tests` — testy odpowiadające warstwom oraz scenariusze E2E.

Zależności powinny być skierowane do środka:

```text
Servanda.Api ───────────────> Servanda.Application ──> Servanda.Domain
       └──> Servanda.Infrastructure ────────────────────────┘
                         └──> Servanda.Application

Servanda.Web ──HTTP──> Servanda.Api
```

Warstwa domenowa nie może zależeć od EF Core, ASP.NET Core ani Reacta.

## Początkowy model danych

Minimalny model domenowy:

```text
Category
- Id
- Name
- Color
- SortOrder

Note
- Id
- CategoryId
- Title
- Content
- CreatedAt
- UpdatedAt
- IsPinned
- IsArchived

Tag
- Id
- Name

NoteTag
- NoteId
- TagId
```

Treść `Note.Content` powinna być przechowywana jako Markdown lub zwykły tekst zgodny z Markdown. Format zapisu nie może zależeć od konkretnego edytora UI.

Identyfikatory i dokładne typy pól należy ustalić przy implementacji pierwszego przypadku użycia. Daty powinny być zapisywane w UTC, a formatowane w lokalnej strefie czasowej dopiero w UI.

## Baza danych i migracje

- Domyślna baza developerska znajduje się w `data/servanda.db`.
- Plik bazy, pliki WAL/SHM oraz lokalne kopie zapasowe muszą być ignorowane przez Git.
- Schemat jest zarządzany wyłącznie przez migracje EF Core.
- Nie należy używać `EnsureCreated()` jako docelowego mechanizmu tworzenia schematu.
- Przy starcie developerskim aplikacja może automatycznie zastosować oczekujące migracje, o ile błędy są wyraźnie raportowane i nie powoduje to utraty danych.
- Testy integracyjne muszą korzystać z odizolowanej, tymczasowej bazy; nie wolno im modyfikować `data/servanda.db`.
- Każda destrukcyjna migracja wymaga świadomej decyzji i opisu sposobu ochrony istniejących danych.

## API

API jest dostępne pod prefiksem `/api`. Początkowy, orientacyjny zestaw zasobów:

```text
GET    /api/notes
GET    /api/notes/{id}
POST   /api/notes
PUT    /api/notes/{id}
DELETE /api/notes/{id}

GET    /api/categories
POST   /api/categories
PUT    /api/categories/{id}
DELETE /api/categories/{id}

GET    /api/tags
POST   /api/tags
DELETE /api/tags/{id}
```

Nie jest to zamrożony kontrakt. Endpointy należy projektować pod rzeczywiste przypadki użycia. API powinno:

- używać DTO zamiast bezpośrednio serializować encje EF Core;
- walidować dane wejściowe;
- zwracać spójne kody HTTP i odpowiedzi błędów w formacie Problem Details;
- przyjmować `CancellationToken` w operacjach asynchronicznych;
- nie ujawniać ścieżek plików, connection stringów ani szczegółów wyjątków.

## Frontend

- Komponenty powinny być małe i skupione na jednej odpowiedzialności.
- Logika komunikacji z API nie powinna być duplikowana w komponentach.
- Stan zdalny i lokalny stan UI należy traktować oddzielnie.
- Formularze muszą obsługiwać stany ładowania, błędu i braku danych.
- Autosave powinien mieć debounce i czytelny status: zapisywanie, zapisano, błąd.
- Podstawowa obsługa aplikacji musi być dostępna z klawiatury.
- UI nie powinno zakładać, że backend odpowiada natychmiast lub zawsze poprawnie.
- Adres API w developmentcie pozostaje względny (`/api`), obsługiwany przez proxy Vite.

Nie należy dodawać rozbudowanej biblioteki zarządzania stanem, dopóki lokalny stan Reacta i niewielka warstwa zapytań są wystarczające.

## Bezpieczeństwo i prywatność

- Backend developerski powinien domyślnie nasłuchiwać tylko na `localhost`.
- Aplikacja nie może wysyłać treści notatek do zewnętrznych usług bez jednoznacznej funkcji i zgody użytkownika.
- Sekrety i dane lokalne nie mogą trafić do repozytorium.
- Logi nie powinny zawierać pełnej treści notatek.
- Jeżeli aplikacja zostanie udostępniona w sieci, przed takim wdrożeniem trzeba dodać uwierzytelnianie, autoryzację, ochronę żądań oraz osobną analizę zagrożeń.

## Wymagania developerskie

Docelowo do pracy będą potrzebne:

- .NET 10 SDK;
- wspierana wersja Node.js zgodna z `package.json`/`.nvmrc`;
- npm.

Po utworzeniu projektu standardowy start powinien wyglądać następująco:

```bash
npm install
npm run dev
```

`npm install` jest potrzebne po pierwszym sklonowaniu lub zmianie zależności. Codzienna praca powinna wymagać tylko `npm run dev`.

## Kryteria ukończenia zmian

Zmiana jest gotowa, gdy w zakresie adekwatnym do jej ryzyka:

- spełnia opisany przypadek użycia;
- nie narusza granic warstw;
- ma testy dla istotnej logiki i regresji;
- przechodzi testy frontendu i backendu;
- przechodzi produkcyjny build;
- nie wprowadza błędów formatowania ani ostrzeżeń kompilatora;
- zawiera migrację, jeżeli zmienia schemat danych;
- aktualizuje dokumentację, jeżeli zmienia komendy, architekturę lub kontrakt API;
- została sprawdzona w przeglądarce, jeżeli wpływa na zachowanie albo wygląd UI.

## Zasady pracy dla agentów programistycznych

Agent pracujący nad repozytorium powinien:

1. Przed zmianą odczytać ten dokument oraz lokalne `AGENTS.md`, jeżeli taki plik powstanie.
2. Sprawdzić bieżący stan repozytorium i zachować wszystkie niezwiązane zmiany użytkownika.
3. Nie zmieniać uzgodnionego stacku ani nie dodawać frameworków bez wyraźnej potrzeby.
4. Preferować najmniejszą zmianę, która kompletnie realizuje wymaganie.
5. Nie tworzyć abstrakcji, repozytoriów ani warstw „na przyszłość”, jeżeli nie obsługują rzeczywistego przypadku użycia.
6. Utrzymywać możliwość uruchomienia całej aplikacji przez `npm run dev`.
7. Po zmianie uruchomić adekwatne testy i build; zgłosić dokładnie, czego nie udało się zweryfikować.
8. Nie modyfikować ani nie usuwać lokalnej bazy użytkownika podczas testów.
9. Nie wykonywać destrukcyjnych migracji lub operacji Git bez jednoznacznej zgody.
10. W podsumowaniu wskazać zmienione pliki, rezultat weryfikacji i ewentualne dalsze kroki.

## Decyzje odłożone

Następujące kwestie nie są jeszcze ustalone i nie powinny być rozstrzygane przypadkowo podczas innych prac:

- biblioteka komponentów i finalny system stylowania;
- konkretny edytor Markdown;
- biblioteka obsługi zapytań po stronie Reacta;
- mechanizm kopii zapasowych i eksportu;
- szyfrowanie lokalnej bazy;
- instalator i sposób uruchamiania wersji produkcyjnej;
- synchronizacja między urządzeniami;
- uwierzytelnianie i dostęp sieciowy.

Decyzje te należy podjąć wtedy, gdy pojawią się odpowiadające im wymagania.
