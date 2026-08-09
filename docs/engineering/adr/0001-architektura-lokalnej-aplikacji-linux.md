# ADR 0001: lokalna aplikacja Blazor i SQLite dla Linuksa

Status: accepted

Data: 2026-08-08

## Kontekst

Servanda ma być prywatną pamięcią zewnętrzną dla jednego użytkownika. Użytkownik pracuje domyślnie na komputerze osobistym lub laptopie z Linuksem, chce uruchamiać aplikację jak zwykły program i zachować pełną kontrolę nad danymi. Projekt powstaje od zera na podstawie dokumentacji.

Aplikacja docelowo potrzebuje adaptacyjnego interfejsu, wielu wyspecjalizowanych modułów, trwałych relacji danych, bezpiecznych migracji, kopii i prostego lokalnego uruchomienia. V1 dostarcza bezstanową powłokę i bezpieczne uruchomienie; pierwsze działające moduły, narzędzia i prompty, rozpoczynają się w v2.

## Rozważane opcje

### Aplikacja webowa z zewnętrznym backendem

Ułatwia dostęp z wielu urządzeń, ale wymaga hostingu, uwierzytelnienia, ochrony danych w sieci, synchronizacji i stałego utrzymania. Nie odpowiada zakresowi jednoosobowego narzędzia lokalnego.

### Aplikacja statyczna z `localStorage` lub IndexedDB

Nie wymaga procesu serwerowego, lecz wiąże dane z profilem przeglądarki, komplikuje kopie i odzyskiwanie oraz utrudnia kontrolę plików należących do użytkownika.

### Natywna aplikacja desktopowa

Daje naturalny cykl życia procesu, ale wymaga osobnej powłoki desktopowej, systemu UI i dodatkowej ścieżki dystrybucji. Ten koszt nie jest potrzebny do pierwszego wydania, którego interfejs może działać w lokalnej przeglądarce.

### Lokalny Blazor Web App z SQLite

ASP.NET Core zapewnia proces z dostępem do lokalnego systemu plików, Blazor daje komponentowy interfejs w C#, a SQLite obsługuje transakcje, relacje i migracje bez zewnętrznej usługi. Interfejs działa w aktualnej przeglądarce, a launcher może ukryć techniczny charakter lokalnego serwera.

## Decyzja

Nowy projekt korzysta z:

- ASP.NET Core i Blazor Web App,
- trybu Interactive Server dla interaktywnych widoków,
- C#, komponentów Razor i izolowanych stylów CSS,
- od v2 EF Core z dostawcą SQLite oraz wersjonowanych migracji,
- od v2 jednej bazy SQLite w katalogu danych XDG użytkownika,
- serwisów oddzielających domenę, przypadki użycia, trwałość i prezentację,
- Kestrela nasłuchującego wyłącznie na dynamicznym porcie loopback,
- bootstrapu pojedynczej instancji otwierającego domyślną przeglądarkę oraz, od v2, blokady bazy utrzymywanej przez długowieczny host,
- publikacji `self-contained` i skrótu `.desktop` dla wydania użytkowego,
- od v2 wersjonowanego eksportu JSON oraz kopii SQLite tworzonych bezpiecznym API,
- interfejsu przeznaczonego dla komputerów osobistych i laptopów, bez wersji na telefony i tablety.

## Granice

- Pierwsze wydanie nie zapewnia dostępu z LAN, Internetu ani wielu urządzeń.
- Pierwsze wydanie nie wspiera telefonów, tabletów ani układu mobilnego.
- Nie ma kont, formularza logowania ani ról. Techniczna sesja procesu inicjowana przez launcher zgodnie z ADR 0005 chroni lokalny host, ale nie reprezentuje tożsamości użytkownika.
- Od v2 SQLite nie jest szyfrowane przez samą aplikację; ochronę spoczynkową zapewnia system operacyjny do czasu osobnej decyzji.
- Zamknięcie karty przeglądarki nie musi zatrzymywać procesu. Interfejs zapewnia jawną akcję „Zamknij Servandę”, a kolejne uruchomienie używa istniejącej instancji.
- W v1 błąd startu kończy się bezpiecznym komunikatem launchera. Od v2 błąd otwarcia bazy lub migracji nie uruchamia normalnej aplikacji, a host pozostaje na loopbacku w ograniczonym stanie `recovery`.
- Przyszłe moduły otrzymują własne tabele i migracje dopiero po zaakceptowaniu specyfikacji.

## Konsekwencje

- Użytkownik otrzymuje prostą drogę uruchomienia bez SDK i terminala.
- Proces aplikacji musi działać, gdy używany jest interfejs, a przerwanie circuitu wymaga czytelnego odzyskania widoku.
- Od v2 baza i kopie muszą pozostawać poza katalogiem programu.
- Od v2 wiele kart wymaga kontroli `revision`, mimo że działa jeden proces.
- Publikacja powstaje osobno dla każdej wspieranej architektury Linuksa.
- Dostęp sieciowy lub synchronizacja wymagają nowego ADR i modelu uwierzytelnienia.

## Sposób weryfikacji

- host wiąże adres wyłącznie do `127.0.0.1` i/lub `::1`,
- publikacja uruchamia się na czystym wspieranym Linuksie bez zainstalowanego SDK .NET,
- drugie uruchomienie v1 nie tworzy drugiego procesu, a od v2 także drugiego procesu zapisującego bazę,
- launcher v1 otwiera wyłącznie potwierdzony stan `ready`; od v2 może również otworzyć potwierdzony stan `recovery`, ale nigdy adres z osieroconego deskryptora,
- od v2 baza powstaje w oczekiwanym katalogu XDG z prywatnymi uprawnieniami,
- od v2 nieudana migracja zachowuje bazę i zweryfikowaną kopię,
- od v2 zapis agregatu jest transakcyjny, a nieaktualna `revision` powoduje konflikt,
- od v2 eksport z jednej bazy może odtworzyć kolekcję w nowej bazie,
- testy hosta potwierdzają brak nasłuchu na interfejsie sieciowym.
