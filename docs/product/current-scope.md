# Bieżący zakres Servandy

> Status: obowiązujący  
> Etap: ukończone wydanie v1; aktywny fundament danych i odzyskiwania v2 (P3)

## Cel etapu

Przygotować jednoznaczną dokumentację nowej aplikacji budowanej od zera, a następnie dostarczyć pierwsze lokalne wydanie dla Linuksa na komputery osobiste i laptopy. Nowa implementacja wynika z dokumentacji znajdującej się w `docs/`.

## Zakres v1

V1 jest fundamentowym wydaniem powłoki Servandy. Potwierdza bezpieczny sposób uruchomienia lokalnej aplikacji oraz kierunek interfejsu, ale nie przechowuje jeszcze kolekcji użytkownika i nie udostępnia działających modułów dziedzinowych. Granicę trwałości utrwala [ADR 0006](../engineering/adr/0006-bezstanowy-zakres-v1.md).

### Platforma i uruchomienie

- Linux jako podstawowy system użytkownika,
- komputery osobiste i laptopy jako jedyna wspierana klasa urządzeń,
- ASP.NET Core 10 i Blazor Web App na .NET 10 LTS w trybie Interactive Server,
- lokalny proces nasłuchujący wyłącznie na loopbacku,
- publikacja `self-contained` dla wybranej architektury Linuksa,
- pojedyncza instancja procesu oraz launcher odnajdujący potwierdzony stan istniejącej instancji i otwierający ją w domyślnej przeglądarce,
- sesja procesu inicjowana jednorazowym biletem launchera oraz ochrona `Host`, `Origin`, antiforgery i CSP zgodna z `security-model.md`,
- skrót `.desktop` jako standardowa droga uruchomienia wydania użytkowego,
- uruchomienie deweloperskie przez przypięte SDK .NET 10.

### Dane i bezpieczeństwo

- brak kanonicznej bazy i trwałych danych domenowych w v1,
- prywatne katalogi XDG wyłącznie dla plików runtime i logów pozbawionych treści użytkownika; v1 nie tworzy magazynu ustawień,
- brak utrwalania sekretów procesu w Web Storage,
- brak wysyłania danych do usług zewnętrznych,
- czytelny błąd launchera, jeżeli host nie może się uruchomić; v1 nie udostępnia przeglądarkowego trybu `recovery`.

### Interfejs i obszary

- ekran główny z centralną, adaptacyjną siatką kafli obszarów,
- panel boczny z powrotem do ekranu głównego i statyczną listą obszarów,
- początkowy zestaw obszarów wskazany w `features/areas-dashboard.md`,
- wszystkie obszary mają status „Planowane”; kafel nie prowadzi do pustego widoku udającego działającą funkcję,
- system UI, klawiatura, reflow i stany procesu zgodne z dokumentacją.

### Świadomie nieobecne w v1

- działające moduły, w tym katalog narzędzi, biblioteka promptów, Prompt Studio i pozostałe obszary,
- dodawanie, edycja, kolejność, ukrywanie i archiwizacja obszarów,
- SQLite, migracje schematu, kopie ochronne i tryb `recovery`,
- edytory, współbieżność danych, wyszukiwanie, kategorie i tagi,
- import i eksport kolekcji.

## Praca dokumentacyjna przed implementacją

Przed utworzeniem rozwiązania .NET dokumentacja MUSI:

1. rozdzielać wymagania pierwszego wydania od późniejszych modułów,
2. rozdzielać bezstanowy fundament v1 od modelu danych, kopii, eksportu i migracji planowanych dla v2,
3. opisywać uruchomienie użytkowe i deweloperskie,
4. zawierać kryteria akceptacji funkcji oraz wymagania jakościowe,
5. jednoznacznie rozdzielać wspierane widoki komputerowe od niewspieranych telefonów i tabletów.

## Zakres rozpoczynający się w v2

- jedna lokalna baza SQLite oraz wersjonowane migracje,
- kopie ochronne, tryb `recovery`, import i eksport,
- trwałe zarządzanie obszarami,
- katalog narzędzi, biblioteka promptów i Prompt Studio,
- aktywowanie pozostałych obszarów dopiero po dodaniu ich własnych kontraktów zachowania, modelu danych i kryteriów akceptacji,
- edytory, kategorie, tagi, wyszukiwanie oraz ochrona współbieżności danych.

## Poza zatwierdzonym zakresem v1 i v2

- zunifikowane centrum ustawień i operacji systemowych obejmujące rozbudowaną diagnostykę, zarządzanie retencją oraz wszystkie operacje danych w jednym miejscu,
- trwałe wersje robocze w profilu przeglądarki; wymagają osobnego kontraktu prywatności, retencji, czyszczenia i zachowania między profilami,
- przypomnienia systemowe, integracja kalendarza i automatyczne działania,
- porady medyczne, diagnozowanie lub automatyczna interpretacja danych zdrowotnych,
- konta, role i wielu użytkowników,
- synchronizacja między urządzeniami,
- dostęp z LAN lub Internetu,
- aplikacja natywna oraz wersje na telefony i tablety,
- automatyczne wykonywanie promptów przez zewnętrzne API,
- automatyczne tagowanie i wyszukiwanie semantyczne.

## Kryteria ukończenia v1

- użytkownik uruchamia Servandę z menu aplikacji Linuksa bez instalowania Node, npm ani SDK .NET,
- aplikacja otwiera ekran główny i nie wystawia portu poza loopback,
- wejście na odgadnięty port bez sesji launchera nie udostępnia powłoki, a obce pochodzenie nie może zamknąć procesu,
- pulpit pokazuje pełny początkowy zestaw obszarów jako jednoznacznie planowane i nie oferuje pozornego zapisu danych,
- aplikacja nie tworzy kanonicznej bazy, kopii ani eksportu i nie pokazuje operacji zarezerwowanych dla v2,
- ponowne uruchomienie launchera otwiera istniejącą, zweryfikowaną instancję albo bezpiecznie uruchamia nową,
- krytyczne przepływy mają testy automatyczne,
- `dotnet format --verify-no-changes`, `dotnet build` i `dotnet test` przechodzą,
- klawiatura, dostępność oraz szerokości 1024, 1280, 1440 i 1920 px zostały zweryfikowane na Linuksie.
