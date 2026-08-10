# Wymagania jakościowe Servandy

> Status: obowiązujący od v1; wymagania danych i modułów dotyczą v2

## Integralność danych od v2

- Nieudana operacja nie może pozostawić częściowo zmienionego agregatu.
- Nieudana migracja, import ani operacja chroniona nie mogą naruszyć ostatniej poprawnej bazy i zweryfikowanej kopii.
- Walidacja domenowa działa niezależnie od formularza.
- Konflikt `revision` nie może zostać zamieniony w automatyczne nadpisanie.
- Konflikt `ordering_scope` wycofuje cały reorder; lista pozostaje unikalna, gęsta i deterministyczna.
- Import przekraczający limity jest odrzucany, nie przycinany.
- Import zastępujący jest stosowany w jednej transakcji dopiero po pełnej walidacji w izolowanym stagingu i utworzeniu zweryfikowanej kopii.
- Po udanym imporcie wszystkie dane domenowe odpowiadają dokumentowi; element nieobecny w eksporcie nie może pozostać po cichu w kolekcji.
- Kopia musi dać się otworzyć przed rozpoczęciem operacji, którą chroni.
- Test odtworzenia jest wymagany; samo utworzenie pliku kopii nie dowodzi odzyskiwalności.
- Aktualizacja i odinstalowanie programu domyślnie zachowują dane użytkownika.

## Dostępność

- Wszystkie funkcje są dostępne z klawiatury.
- Fokus jest widoczny i nie pozostaje poza aktywnym dialogiem.
- Przyciski ikonowe mają dostępne nazwy.
- Stan aktywny, planowany, błąd i ważność informacji nie zależą wyłącznie od koloru.
- Status połączenia jest ogłaszany przez `aria-live`; od v2 dotyczy to również zapisu, konfliktu i kopiowania.
- Dialog ma nazwę, kontrolkę zamknięcia i przewidywalny powrót fokusu.
- Interfejs respektuje `prefers-reduced-motion`.
- Tekst zwykły ma kontrast co najmniej 4,5:1, duży tekst i wizualne granice kontrolek co najmniej 3:1; pola używają kontrastowego `--border-interactive`, a główna akcja pary `--acid`/`--acid-on`.
- Cele wskaźnika spełniają WCAG 2.2 2.5.8; kontrolki projektowane przez Servandę mają domyślnie co najmniej 32 × 32 px CSS.
- Element z fokusem nie jest całkowicie zasłonięty przez sticky panel, toast, popover ani stopkę dialogu.
- Zmiana odstępów tekstu zgodna z WCAG 1.4.12 nie powoduje utraty treści ani funkcji.
- Krytyczne widoki nie mają automatycznie wykrywanych naruszeń WCAG 2.2 AA.

Automatyczna kontrola nie zastępuje ręcznego przejścia klawiaturą i sprawdzenia czytnikiem ekranu reprezentatywnych przepływów.

## Widoki komputerowe i przeglądarki

- Linux jest obowiązkowo weryfikowaną platformą.
- Pierwsze wydanie wspiera aktualne stabilne wersje Chromium i Firefox na Linuksie.
- Pierwsze wydanie jest przeznaczone wyłącznie na komputery osobiste i laptopy. Telefony i tablety nie są wspierane ani objęte kryteriami akceptacji.
- Widoki działają w szerokościach od `1024px` bez poziomego przewijania całej strony i bez utraty akcji formularza.
- Reprezentatywne szerokości widoku to 1024, 1280, 1440 i 1920 px.
- Panel boczny v1, a od v2 także dialogi danych, Prompt Studio i zarządzanie obszarami, są sprawdzane co najmniej na najmniejszej i największej szerokości oraz przy właściwych breakpointach.
- Przy powiększeniu 200% dla widoku 1024 px oraz 400% dla widoku 1280 px krytyczne przepływy zachowują treść, fokus i akcje bez poziomego przewijania strony; wyjątki dla prawdziwie dwuwymiarowych regionów są nazwane i przewijają się lokalnie.
- Długie etykiety v1, a od v2 także nieprzerwane URL-e, maksymalne tagi, rozbudowane błędy i treść promptu, nie nakładają się, nie wypychają akcji poza dostępny obszar i mają dostęp do pełnej wartości.
- Obsługa Safari, Windows i macOS nie jest kryterium pierwszego wydania.

## Bezpieczeństwo i prywatność

- Host nasłuchuje wyłącznie na `127.0.0.1` i/lub `::1`.
- Test automatyczny wykrywa przypadkowe wiązanie do `0.0.0.0`, adresu LAN albo publicznego interfejsu.
- Dostęp do normalnej aplikacji i SignalR wymaga sesji procesu zainicjowanej jednorazowym biletem launchera zgodnie z `security-model.md`; od v2 dotyczy to także recovery.
- Host Filtering odrzuca każdy `Host` i `:authority` spoza jawnie związanych adresów loopback; aplikacja nie generuje adresu na podstawie nagłówka żądania.
- Operacje zmieniające stan wymagają sesji, dokładnego kanonicznego `Origin` i tokenu antiforgery. WebSocket oraz pozostałe transporty SignalR mają osobną kontrolę originu, ponieważ CORS nie chroni WebSocketów.
- Shutdown nie jest dostępny przez GET i wymaga jawnego potwierdzenia; od v2 ten sam kontrakt obejmuje import i odtworzenie.
- CORS dla obcych originów i Forwarded Headers Middleware są wyłączone.
- Dynamiczny tekst jest kodowany, a renderowanie HTML wymaga jawnego oczyszczenia.
- Od v2 URL narzędzia dopuszcza wyłącznie HTTP(S).
- Odpowiedź i interfejs nie ujawniają ścieżek systemowych, SQL, treści plików ani stosu wywołań.
- Logi nie zawierają treści promptów ani przyszłych danych rodzinnych, zdrowotnych i finansowych.
- Dane nie są wysyłane do zewnętrznej usługi bez osobnej funkcji, opisu zakresu i świadomej zgody.
- Skrypty, style, fonty, ikony i obrazy interfejsu nie są pobierane z CDN ani innych zewnętrznych originów.
- Wymuszana CSP co najmniej blokuje osadzanie, obiekty, zewnętrzne skrypty, niedozwolone połączenia i zmianę bazowego URL. Wymagane skrypty inline używają nonce lub hashy; wyjątek stylów inline wymaga raportu zgodnie z `security-model.md`, a `unsafe-eval` jest zabronione.
- Sekret sterujący launchera, bilety i sesje nie są zapisywane w Web Storage ani logowane; od v2 zakaz obejmuje także treść formularzy.
- Prywatne pliki runtime v1 oraz katalogi i pliki danych v2 mają prywatne uprawnienia użytkownika.
- Od v2 eksport jest traktowany jak dokument prywatny i otrzymuje prywatne uprawnienia.
- Od v2 brak szyfrowania SQLite jest jawnie opisany; interfejs nie obiecuje szyfrowania, którego aplikacja nie realizuje.

## Wydajność

W v1 ekran główny powinien być gotowy do interakcji w ciągu 2 sekund od gotowości lokalnego procesu na referencyjnym komputerze użytkownika. Pozostałe budżety tej sekcji rozpoczynają się w v2.

Cele v2 mierzymy na dwóch deterministycznych profilach danych:

| Profil | Narzędzia | Prompty | Treść promptów | Przeznaczenie |
|---|---:|---:|---|---|
| referencyjny | 2 000 | 1 000 | średnio 5 bieżących wariantów na prompt i 2 000 znaków na wariant; co najmniej jeden prompt ma 20 wariantów po 30 000 znaków | kryterium v2 |
| graniczny | 10 000 | 5 000 | ten sam deterministyczny rozkład długości i wariantów co profil referencyjny | kontrolowana degradacja i wykrywanie nieograniczonych operacji |

Oba profile zawierają hierarchie kategorii, maksymalne liczby tagów, polskie znaki, podobne prefiksy oraz 500 wpisów historii użycia. Generator, stałe ziarno i zestaw zapytań pomiarowych MUSZĄ zostać zapisane w repozytorium wraz z testami P4. Zestaw zapytań obejmuje dokładną nazwę, prefiks, kilka tokenów, kategorię, tag, URL, polskie znaki i dopasowanie wyłącznie w treści wariantu.

Każdy raport podaje wersję aplikacji, .NET, SQLite i przeglądarki, tryb publikacji, CPU, RAM, rodzaj dysku i system plików. Pomiar wykonuje się na wydaniu `Release` z lokalną bazą po 10 rozgrzewających zapytaniach i co najmniej 100 mierzonych zapytaniach na wspieraną przeglądarkę. Raportuje się medianę i p95; osobno mierzy się pierwsze zapytanie po otwarciu procesu bez wcześniejszego rozgrzania indeksu.

- Debounce wyszukiwania wynosi `250 ms`. Na profilu referencyjnym p95 pełnej pętli od zakończenia debounce do wyrenderowania pierwszej strony nie przekracza `250 ms`, czyli p95 od ostatniego naciśnięcia klawisza nie przekracza `500 ms`.
- Na profilu granicznym p95 pełnej pętli od zakończenia debounce do wyrenderowania pierwszej strony nie przekracza `750 ms`; interfejs pokazuje stan wyszukiwania i pozostaje responsywny.
- Samo zapytanie SQLite na rozgrzanym profilu referencyjnym ma p95 nie większe niż `100 ms`. Pierwsze zapytanie po starcie jest raportowane osobno i nie może przekroczyć `750 ms`.
- Zapis pojedynczego agregatu bez kopii powinien kończyć się w ciągu 500 ms w typowym przypadku.
- Widok nie pobiera pełnej treści wszystkich wariantów promptów, jeżeli pokazuje wyłącznie karty.
- Pierwsza i każda kolejna strona wyników zawiera maksymalnie 50 elementów. Zapytania list mają projekcję i limit; rozbudowa danych wymaga pomiaru planu zapytania i indeksów.
- Nowe zapytanie anuluje poprzednie, a spóźniony wynik nie może zastąpić nowszej odpowiedzi.
- Aktualizacja dokumentów FTS po zmianie kategorii lub tagu jest mierzona również na profilu granicznym i nie może pobierać pełnej treści agregatów do komponentu UI.
- Reorder największego zakresu profilu granicznego jest mierzony osobno; nie ma budżetu interaktywnego zapisu 500 ms, ale pokazuje stan zajętości, wykonuje jedną transakcję i nie rośnie kwadratowo względem liczby elementów.
- Nowa zależność kliencka wymaga uzasadnienia rzeczywistą potrzebą.

Wartości są budżetami inżynierskimi, nie gwarancją dla każdego sprzętu. Przekroczenie wymaga pomiaru, opisu i decyzji, nie arbitralnego zwiększenia limitu.

## Odporność i diagnostyka

- Utrata circuitu pokazuje stan ponownego połączenia. Od v2 nie sugeruje utraty wcześniej zapisanych danych.
- W v1 launcher otwiera adres dopiero po potwierdzeniu identyfikatora instancji i stanu `ready`; osierocony deskryptor nie jest dowodem gotowości.
- Od v2 awaria otwarcia bazy lub migracji publikuje `recovery` i prowadzi do ograniczonego ekranu odzyskiwania, nie do pustej kolekcji ani normalnej powłoki.
- Od v2 w stanie `recovery` router obszarów i operacje domenowe są niedostępne, a host zachowuje wyłączną blokadę bazy.
- Brak miejsca na dysku, brak uprawnień i konflikt instancji mają różne klasy wyniku.
- Diagnostyka zawiera identyfikator zdarzenia i kategorię, lecz nie prywatną treść.
- Od v2 nagłe zakończenie procesu podczas testu zapisu nie może pozostawić logicznie częściowego agregatu.

## Strategia testów

1. V1 — testy hosta: loopback, pojedyncza instancja, atomowa publikacja `ready`, osierocone pliki runtime, prywatny sekret, bilet, sesja, `Host`, `Origin`, antiforgery, WebSocket i prywatne uprawnienia.
2. V1 — testy komponentów i E2E: bootstrap, ponowne otwarcie instancji, odrzucenie obcego originu, CSP, shutdown, statyczny pulpit oraz brak edytorów i operacji danych.
3. V1 — axe oraz ręczna kontrola klawiatury, kontrastu, celu 24 × 24 px CSS, fokusu, odstępów tekstu, zoomu 200%/400% i wspieranych szerokości.
4. V2 — testy jednostkowe domeny i przypadków użycia: limity, hierarchie, walidacja, interpolacja, retencja, konflikt, anulowanie, normalizacja i mapowanie błędów.
5. V2 — testy integracyjne SQLite: transakcje, rewizje, kolejność, migracje, kopie, eksport, import, `content_epoch`, FTS5, integralność i odbudowa indeksu.
6. V2 — E2E: recovery, odtworzenie kopii, zarządzanie obszarami, wyszukiwanie, narzędzia, Prompt Studio, konflikt, import, eksport i odzyskanie — osobno w Chromium i Firefox.

Testy trwałości używają wyłącznie katalogów tymczasowych i nigdy nie otwierają produkcyjnej bazy użytkownika.

## Polecenia weryfikacyjne

Po utworzeniu rozwiązania README MUSI wskazywać rzeczywiste polecenia. Minimalny zestaw:

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build --no-restore
dotnet test --no-build
```

Publikacja wydania dodatkowo uruchamia test artefaktu `self-contained` na wspieranym Linuksie oraz E2E na zbudowanej wersji, nie wyłącznie na serwerze deweloperskim.

Repozytoryjny skrypt `tests/Servanda.E2E/run-browser-tests.sh` publikuje `Servanda-linux-x64` i uruchamia przepływ hosta w przypiętych silnikach Chromium i Firefox. Playwright oraz pobierane przeglądarki należą wyłącznie do narzędzi testowych i nie są częścią artefaktu użytkowego.

## Definition of Done

Zmiana kodu jest ukończona, gdy:

- spełnia kontrakt funkcji i nie rozszerza po cichu zakresu,
- od v2 zachowuje lub jawnie migruje dane,
- ma test adekwatny do ryzyka,
- nie obniża bezpieczeństwa hosta i danych,
- aktualizuje dokumentację przy zmianie kontraktu,
- przechodzi formatowanie, build i testy,
- zmiana UI została sprawdzona klawiaturą i na właściwych szerokościach,
- artefakt użytkowy nadal uruchamia się na domyślnym Linuksie, jeżeli zmiana dotyczy hosta, publikacji lub zależności.
