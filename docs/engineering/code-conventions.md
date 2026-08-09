# Konwencje kodu Servandy

> Status: obowiązujący dla nowego projektu

## Struktura rozwiązania

Docelowy punkt wyjścia:

```text
Servanda.sln
global.json
Directory.Build.props
src/
├── Servanda.App/             # host ASP.NET Core, Razor, konfiguracja i zasoby
├── Servanda.Application/     # przypadki użycia v2, komendy, zapytania i rezultaty
├── Servanda.Domain/          # model domenowy v2 i reguły niezależne od infrastruktury
└── Servanda.Infrastructure/  # XDG/runtime w v1; EF Core, SQLite, kopie i eksport od v2
tests/
├── Servanda.Domain.Tests/
├── Servanda.Application.Tests/
├── Servanda.Infrastructure.Tests/
└── Servanda.E2E/
```

Liczba projektów może zostać zmniejszona wyłącznie wtedy, gdy zachowany jest kierunek zależności i możliwość testowania domeny bez hosta oraz SQLite. Zmiana nazw ścieżek wymaga aktualizacji README, AGENTS.md i poleceń dokumentacji.

Odpowiedzialność launchera należy do wykonywalnego bootstrapu w `Servanda.App` albo do osobnego projektu wykonywalnego dodanego podczas P1. `Servanda.Infrastructure` dostarcza implementacje blokady instancji, XDG i protokołu deskryptora, ale nie jest samo w sobie launcherem. Od v2 blokadę bazy utrzymuje długowieczny host, nie proces kończący się po otwarciu przeglądarki.

Kierunek zależności:

```text
App -> Application -> Domain
App -> Infrastructure -> Application/Domain
```

`Domain` nie odwołuje się do Razor, EF Core, `HttpContext`, `IJSRuntime`, systemu plików ani konfiguracji hosta.

## Konfiguracja .NET

- Przypnij wspierane SDK LTS w `global.json`.
- Włącz nullable reference types, implicit usings i analizatory .NET.
- Traktuj ostrzeżenia projektu jako błędy; wyjątek wymaga lokalnego komentarza lub udokumentowanej decyzji.
- Wspólne właściwości projektów należą do `Directory.Build.props`.
- Wersje pakietów NuGet są zarządzane centralnie.
- Nowy pakiet wymaga uzasadnienia funkcją, bezpieczeństwem albo testowalnością.

## Domena i przypadki użycia od v2

- Reguły walidacji, archiwizacji, retencji, hierarchii i kontroli wersji należą do domeny lub warstwy aplikacyjnej, nie do komponentu UI.
- Komendy modyfikują jeden jawny agregat i zwracają typowany rezultat: sukces, walidacja, konflikt, brak albo błąd infrastruktury.
- Zapytania zwracają modele tylko z danymi potrzebnymi widokowi; komponent nie otrzymuje śledzonych encji EF Core.
- Identyfikatory są typami wartości albo jawnie walidowanymi ULID, nie dowolnymi stringami przepuszczanymi między warstwami.
- Daty trwałe zapisuj w UTC. Bieżący czas dostarcza `TimeProvider`.
- Teksty domenowe mają limity zgodne z `data-model.md`; UI może walidować wcześniej, ale nie definiuje innych reguł.

## C# i asynchroniczność

- Używaj `async`/`await` od komponentu do I/O. Nie używaj `.Result`, `.Wait()` ani synchronicznego blokowania operacji asynchronicznych.
- Operacje I/O przyjmują i przekazują `CancellationToken`.
- Nie używaj `async void` poza handlerem wymaganym przez platformę.
- Preferuj rekordy i niemutowalne modele do komend, rezultatów oraz migawek.
- Oczekiwany błąd domenowy nie jest sterowany tekstem wyjątku systemowego.
- Nie przechwytuj ogólnego `Exception` bez dodania kontekstu, bezpiecznego logowania i zachowania przyczyny.

## EF Core i SQLite od v2

- `DbContext` ma krótki zakres jednej operacji. Nie przechowuj go w komponencie Blazor ani przez cały circuit.
- Każda komenda zapisu rozpoczyna transakcję, jeżeli zmienia więcej niż jeden wiersz lub wykonuje retencję.
- Ograniczenia bazy uzupełniają walidację domenową: klucze obce, indeksy unikalne, wymagane pola i sensowne `CHECK`.
- `revision` jest tokenem optymistycznej współbieżności. Konflikt EF jest mapowany na stabilny rezultat aplikacyjny.
- Przy zapisie odłączonego modelu oczekiwana rewizja z komendy jest ustawiana jako `OriginalValue` tokenu EF Core albo jawnie występuje w warunku `UPDATE`. Ustawienie jej wyłącznie jako wartości bieżącej jest błędem.
- Zero wierszy zmienionych przez warunkowe `UPDATE` oznacza konflikt albo brak rekordu rozróżniony bezpiecznym odczytem; nie jest sukcesem i nie uruchamia automatycznego „client wins”.
- Reorder korzysta z `ordering_scopes` i protokołu ADR 0004. Komponent nie zapisuje sekwencji pojedynczych zmian `sort_order`, a implementacja utrzymuje unikalność również podczas przejściowego przenumerowania.
- Modele zwykłej edycji nie pozwalają ustawić `sort_order` ani pól członkostwa listy. Tworzenie, przenoszenie i usuwanie mają osobne komendy z oczekiwanymi rewizjami zakresów.
- Zapytania tylko do odczytu używają `AsNoTracking`.
- Nie ukrywaj nieograniczonego zapytania za metodą `GetAll`, jeżeli widok potrzebuje stronicowania lub projekcji.
- Migracja ma opisową nazwę, test od poprzedniej wersji i nie usuwa danych bez jawnego planu odzyskania.
- Seed początkowych obszarów jest idempotentny i nie nadpisuje edycji użytkownika.
- Dokument FTS jest pochodną projekcją agregatu. Zapis danych domenowych i aktualizacja indeksu należą do tej samej transakcji, a kod infrastruktury udostępnia jawny przypadek pełnej odbudowy indeksu.
- Projekcja FTS pobiera nazwy współdzielonych kategorii i tagów z bieżącej bazy w transakcji, nie ufa ich kopii przesłanej przez odłączony model edytora.

## Blazor Interactive Server

- Komponent odpowiada za prezentację, lokalny stan widoku i delegowanie przypadku użycia.
- Stan krytyczny nie może istnieć wyłącznie w pamięci komponentu lub circuitu.
- Serwis `Scoped` żyje w zakresie circuitu; nie traktuj go jak współdzielonej sesji użytkownika.
- Serwis `Singleton` jest bezstanowy albo jawnie bezpieczny wątkowo.
- Parametr komponentu jest wejściem właściciela. Potomek zgłasza zmianę przez `EventCallback` albo przypadek użycia.
- Listy trwałych elementów używają `@key`.
- Subskrypcje i timery są zwalniane przez `IDisposable` lub `IAsyncDisposable`.
- Długa operacja pokazuje zajętość i blokuje wyłącznie akcje tworzące konflikt.
- Szybka akcja zapisu, w tym przełączenie ulubionego, używa oczekiwanej rewizji korzenia i aktualizuje model karty rewizją zwróconą po sukcesie.
- Prerenderowany komponent nie zakłada dostępu do JavaScriptu przed interaktywnym renderem.

## Formularze danych od v2 i dostępność od v1

- Formularze korzystają z `EditForm` i jawnego modelu wejściowego.
- Każda kontrolka ma etykietę; placeholder nie jest etykietą.
- Błąd wskazuje pole albo operację i nie ujawnia ścieżki, SQL, treści pliku ani stosu wywołań.
- Dialog ma nazwę, kontrolkę zamknięcia, poprawny fokus początkowy i przewidywalny powrót fokusu.
- Status zapisu, kopiowania, konfliktu i połączenia jest widoczny oraz ogłaszany przez `aria-live`.
- Stan nie zależy wyłącznie od koloru, położenia lub animacji.

## JavaScript interop

- Interop służy tylko do granic przeglądarki: bootstrapu biletu z fragmentu URL, schowka, fokusu, `beforeunload` i otwarcia przeglądarki, gdy nie realizuje tego launcher.
- Każda odpowiedzialność ma mały moduł JavaScript i typowany serwis C#.
- Logika domenowa, normalizacja wyszukiwania, zapytania FTS i podstawowy stan aplikacji nie przechodzą do JavaScriptu.
- Referencje modułów są zwalniane po zakończeniu cyklu życia.
- Tekst renderuje domyślne kodowanie Razor. `MarkupString` wymaga jawnie zaufanej lub oczyszczonej treści.
- Do interopu przekazuj minimalną wartość, nie całą kolekcję.
- Sekret launchera, bilet i sesja nie trafiają do `localStorage`, `sessionStorage` ani IndexedDB; od v2 zakaz obejmuje także treść formularzy.

## Bezpieczeństwo procesu i plików

- Ścieżki XDG wyznacza jeden typowany serwis infrastruktury i nie mogą pochodzić z parametrów żądania.
- Uprawnienia katalogów i plików są ustawiane oraz weryfikowane przy tworzeniu.
- Host odrzuca konfigurację wiążącą go do adresu innego niż loopback w pierwszym wydaniu.
- Host buduje kanoniczny origin z rzeczywiście związanego adresu, nie z nagłówka żądania. Host Filtering, kontrola `Origin`, WebSocket `AllowedOrigins`, jawne `UseAntiforgery` i sesja launchera są konfigurowane oraz testowane jako osobne warstwy.
- Sekret sterujący, bilety i identyfikatory sesji generuje kryptograficzny generator liczb losowych. Porównanie sekretów jest odporne na analizę czasu, a wartości nie trafiają do argumentów procesu ani logów.
- Log nie zawiera treści użytkownika ani sekretów.
- Eksport używa bezpiecznej nazwy generowanej przez aplikację; nazwa podana przez użytkownika nie może pozwolić na traversal.
- Akcja zamknięcia procesu i operacje zapisu wymagają sesji oraz zabezpieczeń określonych w `security-model.md`; kontroler lub komponent nie tworzy słabszej alternatywnej ścieżki.

## CSS i system wizualny

- Globalne style obejmują tokeny, reset, typografię i rzeczywiście współdzielone wzorce.
- Style komponentu trafiają domyślnie do odpowiadającego pliku `.razor.css`.
- Pierwsze wydanie używa zwykłego CSS bez SASS/SCSS i bez frontendowego kroku npm. Zmiana łańcucha budowania wymaga aktualizacji decyzji technicznych i zachowania publikacji bez Node/npm dla użytkownika.
- Wartość odstępu, rozmiaru tekstu, promienia, wysokości kontrolki, warstwy, koloru albo czasu ruchu używa tokenu z `ui-system.md`; nowa wartość współdzielona najpierw trafia do kontraktu tokenów.
- Klasy opisują rolę, na przykład `area-tile` i `prompt-card`, a nie chwilowy kolor lub pozycję.
- Nie używaj selektorów zależnych od wygenerowanych atrybutów izolacji CSS.
- `::deep` jest dozwolone wyłącznie z lokalnym elementem opakowującym, gdy właściciel komponentu świadomie styluje renderowane dziecko. Nie służy do globalnego przebijania izolacji ani zależności od wewnętrznego DOM obcego komponentu.
- Nie ustawiaj stałej wysokości kontenera tekstu ani `overflow: hidden`, jeżeli może to uciąć dane użytkownika, fokus lub komunikat. Każde skrócenie treści ma dostępną akcję pokazania pełnej wartości.
- Breakpoint wynika z treści, pozostaje w zakresie widoków komputerowych od `1024px` i jest sprawdzany przy reprezentatywnych szerokościach 1024, 1280, 1440 i 1920 px.
- Reguły reflow używane przy zoomie na komputerze nie oznaczają wsparcia urządzeń mobilnych. Krytyczne widoki są dodatkowo sprawdzane przy zoomie wymaganym przez `quality-requirements.md`.
- Pełna biblioteka komponentów UI wymaga osobnej oceny dostępności, rozmiaru i zgodności wizualnej.

## Nazewnictwo

- Nazwy domenowe w kodzie są angielskie; tekst interfejsu jest polski.
- Publiczne typy i komponenty używają PascalCase, prywatne pola `_camelCase`, a metody asynchroniczne sufiksu `Async`.
- Przestrzenie nazw odzwierciedlają warstwę i funkcję, nie historię katalogów.
- Identyfikator nie zależy od edytowalnej nazwy.
- Nie twórz warstwy i18n bez zatwierdzonego zakresu wielu języków.

## Testy i Definition of Done kodu

- Domena ma szybkie testy jednostkowe bez hosta i bazy.
- Przypadki użycia testują sukces, walidację, konflikt i anulowanie.
- Infrastruktura używa odizolowanych katalogów tymczasowych oraz prawdziwego SQLite.
- Migracje są testowane od pustej i poprzedniej wersji bazy.
- Komponenty są testowane przez widoczny wynik, role, dostępne nazwy i zdarzenia użytkownika.
- Krytyczne przepływy sprawdza Playwright na uruchomionym wydaniu linuksowym.
- Minimalna weryfikacja po zmianie kodu obejmuje `dotnet format --verify-no-changes`, `dotnet build` i `dotnet test`.
- Zmiana kontraktu aktualizuje właściwą specyfikację, model danych albo ADR w tym samym zestawie zmian.
