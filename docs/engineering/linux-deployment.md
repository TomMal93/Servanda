# Uruchomienie i publikacja na Linuksie

> Status: obowiązujący; uruchomienie v1 i rozszerzenie danych v2

## Doświadczenie użytkownika

Standardowy przepływ nie wymaga terminala:

1. użytkownik wybiera „Servanda” z menu aplikacji,
2. launcher sprawdza prywatny deskryptor instancji i potwierdza, czy wskazany host nadal działa,
3. jeżeli nie ma poprawnej instancji, uruchamia host i czeka na atomowo opublikowany stan `ready`,
4. przez prywatny sekret sterujący pobiera jednorazowy bilet i otwiera bootstrap potwierdzonego adresu loopback w domyślnej przeglądarce,
5. host w stanie `ready` pokazuje ekran główny.

Środowisko procesu przeglądarki MUSI zachować dostęp do sesji graficznej użytkownika i jej D-Bus. Jeżeli tryb deweloperski ustawia izolowany `XDG_RUNTIME_DIR` dla plików Servandy, launcher nie może przekazać tej wartości bezpośrednio do systemowego mechanizmu otwierania adresu. Przeglądarka otrzymuje właściwy runtime sesji użytkownika, natomiast host i pliki Servandy pozostają w katalogu deweloperskim. Sekret sterujący ani bilet nie mogą przy tym trafić do zmiennych środowiskowych przeglądarki.

Ponowne kliknięcie skrótu nie uruchamia drugiego hosta. Otwiera istniejącą instancję. Interfejs zawiera akcję „Zamknij Servandę”, która po potwierdzeniu kończy lokalny proces. Samo zamknięcie karty nie musi kończyć aplikacji. Od v2 launcher akceptuje również potwierdzony stan `recovery` i otwiera wtedy wyłącznie ekran odzyskiwania.

„Launcher” oznacza odpowiedzialność bootstrapu, nie wymusza osobnego projektu ani długowiecznego procesu. Może być krótkotrwałym trybem tego samego pliku wykonywalnego co host. Po pierwszym uruchomieniu blokadę instancji utrzymuje część hostująca; przy kolejnych uruchomieniach proces bootstrapu otwiera istniejący adres i kończy się. Od v2 host utrzymuje także osobną blokadę kanonicznej bazy.

## Artefakt wydania

- Wydanie użytkowe jest publikacją .NET `self-contained` dla jawnego RID Linuksa.
- Paczka zawiera pliki wykonywalne, zależności, statyczne zasoby UI, launcher, wersję aplikacji i plik `.desktop` albo odwracalny instalator użytkownika tworzący ten wpis.
- Instalacja nie wymaga Node, npm, SDK .NET ani uprawnień administratora.
- Pliki programu są tylko do odczytu podczas normalnej pracy. Aktualizacja programu nie przenosi ani nie zastępuje danych użytkownika.
- Pierwsza wspierana architektura pozostaje decyzją OPEN-001; nazwa artefaktu MUSI zawierać RID.

## Katalogi XDG

| Rodzaj | Zmienna i domyślny katalog | Zawartość |
|---|---|---|
| dane, od v2 | `XDG_DATA_HOME`, domyślnie `~/.local/share/servanda` | baza, blokada bazy, kopie i eksporty; v1 nie tworzy tego magazynu |
| konfiguracja | `XDG_CONFIG_HOME`, domyślnie `~/.config/servanda` | zarezerwowane; v1 i zatwierdzony zakres v2 nie tworzą katalogu ani pliku bez zdefiniowanego ustawienia |
| stan | `XDG_STATE_HOME`, domyślnie `~/.local/state/servanda` | diagnostyka i logi o ograniczonej retencji |
| cache | `XDG_CACHE_HOME`, domyślnie `~/.cache/servanda` | dane odtwarzalne, które można bezpiecznie usunąć |
| runtime | `XDG_RUNTIME_DIR` | prywatny deskryptor adresu i stanu instancji oraz osobny sekret sterujący launchera |

Brak `XDG_RUNTIME_DIR` wymaga prywatnego katalogu zastępczego należącego do bieżącego UID. Aplikacja nie może użyć wspólnej, zapisywalnej przez innych lokalizacji bez sprawdzenia właściciela i uprawnień.

## Host lokalny

- Kestrel wiąże dynamiczny port wyłącznie na loopbacku.
- Launcher otrzymuje rzeczywisty adres z deskryptora instancji; nie zakłada stałego portu.
- Deskryptor zawiera co najmniej wersję formatu, identyfikator instancji, PID hosta, adres loopback i stan `starting` albo `ready`; od v2 dopuszcza również `recovery`. Nie zawiera danych domenowych ani surowego opisu błędu.
- Sekret sterujący nie jest częścią deskryptora. Znajduje się w osobnym pliku `0600`, ma co najmniej 256 bitów entropii, powstaje dla każdego procesu i służy wyłącznie launcherowi do pobrania jednorazowego biletu przeglądarki.
- Host zapisuje nową wersję deskryptora do pliku tymczasowego w tym samym prywatnym katalogu i publikuje ją przez atomową zmianę nazwy. Częściowo zapisany plik nigdy nie oznacza gotowości.
- Launcher ufa deskryptorowi dopiero po sprawdzeniu właściciela i prywatnych uprawnień oraz potwierdzeniu pod wskazanym adresem tego samego identyfikatora instancji i stanu gotowości.
- W v1 stan `ready` oznacza, że host, ochrona sesji i statyczna powłoka są gotowe do interaktywnych połączeń. Od v2 oznacza dodatkowo, że baza została otwarta, a migracje zakończyły się sukcesem.
- Stan `recovery` istnieje od v2 i oznacza, że host działa, lecz normalny router aplikacji i operacje domenowe pozostają niedostępne. Dostępny jest wyłącznie ograniczony przepływ diagnostyki, odtworzenia i ponownej próby startu.
- Informacja runtime'u o adresie jest dostępna tylko dla bieżącego użytkownika. Osierocony lub niepotwierdzony deskryptor można zastąpić, ale sam fakt istnienia pliku nie dowodzi działania procesu.
- Normalna aplikacja i SignalR wymagają sesji procesu opisanej w `security-model.md`; od v2 ten sam wymóg obejmuje recovery. Odgadnięcie adresu i portu nie wystarcza do otwarcia powłoki ani danych.
- Host odrzuca niekanoniczny `Host`, obcy `Origin` dla operacji stanowych i transportów interaktywnych oraz WebSocket spoza dokładnej allowlisty originu.
- Host nie ufa nagłówkom proxy i nie włącza CORS dla zewnętrznych originów.
- Funkcja zmiany nasłuchu na LAN nie istnieje w pierwszym wydaniu.
- Akcja zamknięcia procesu jest dostępna wyłącznie przez uwierzytelnioną sesję tej instancji, wymaga dokładnego originu, antiforgery i potwierdzenia w UI; metoda GET nigdy nie kończy procesu.

## Blokada pojedynczej instancji

- W v1 długowieczny host utrzymuje systemową blokadę instancji w prywatnym katalogu runtime. Launcher ani krótko żyjący proces bootstrapu nie może być jej jedynym właścicielem.
- Od v2 host przed otwarciem SQLite uzyskuje dodatkową blokadę `<XDG_DATA_HOME>/servanda/servanda.lock` i utrzymuje ją przez cały czas używania bazy, również w stanie `recovery`.
- Blokada instancji, blokada bazy i deskryptor są osobnymi bytami. Deskryptor służy do odnalezienia hosta, a blokada bazy chroni magazyn także między sesjami graficznymi.
- Osierocony plik bez aktywnej blokady nie może trwale uniemożliwiać startu. O zajęciu bazy rozstrzyga próba uzyskania blokady systemowej, nie obecność pliku ani sam PID.
- Od v2, jeżeli baza jest zablokowana, lecz launcher nie może bezpiecznie potwierdzić deskryptora istniejącego hosta, nie zgaduje adresu i nie otwiera bazy. Pokazuje komunikat o instancji działającej w innej sesji lub o konieczności diagnostyki.
- Drugi launcher v1 otwiera istniejącą instancję dopiero po zweryfikowaniu deskryptora i stanu `ready`; od v2 dopuszcza również `recovery`.
- Testy v1 obejmują równoczesny start dwóch procesów i osierocony deskryptor. V2 dodaje osieroconą blokadę bazy oraz host w stanie `recovery`.

## Sekwencja startu v1

1. Proces hosta wyznacza i weryfikuje prywatne katalogi XDG.
2. Uzyskuje blokadę instancji procesu.
3. Wiąże Kestrel z dynamicznym adresem loopback i publikuje deskryptor w stanie `starting`.
4. Inicjalizuje sesję launchera, zabezpieczenia hosta i statyczną powłokę.
5. Po sukcesie publikuje stan `ready` i dopiero wtedy launcher otwiera przeglądarkę.
6. Błąd lub przekroczenie limitu czasu kończy się komunikatem launchera bez publikowania `recovery` i bez otwierania niepotwierdzonego adresu.

## Rozszerzenie startu i recovery w v2

Przed publikacją `ready` host uzyskuje blokadę bazy, otwiera SQLite, weryfikuje schemat, tworzy wymaganą kopię i wykonuje migracje. Błąd bazy albo migracji zachowuje artefakty i publikuje `recovery`. Odtworzenie zgodnej, zweryfikowanej kopii wymaga jawnego potwierdzenia, zachowuje nieudaną bazę jako osobny artefakt diagnostyczny i podmienia ją dopiero po zamknięciu połączeń oraz ponownej weryfikacji kopii.

## Uruchomienie deweloperskie

Po utworzeniu rozwiązania minimalny przepływ będzie używał przypiętego SDK:

```bash
dotnet restore
dotnet run --project src/Servanda.App
```

Rzeczywiste nazwy projektu i polecenia MUSZĄ zostać zsynchronizowane z README w chwili bootstrapu. Tryb deweloperski może używać katalogu danych repozytorium lub katalogu tymczasowego wyłącznie po jawnym ustawieniu środowiska deweloperskiego; nie może przypadkowo otworzyć produkcyjnej bazy użytkownika.

Izolacja deweloperskiego `XDG_RUNTIME_DIR` nie może psuć integracji pulpitu. Launcher otwiera adres przez `xdg-open` i, gdy runtime hosta jest odizolowany, przekazuje mechanizmowi pulpitu systemowy katalog `/run/user/<UID>`. Jeżeli nie może potwierdzić tego katalogu, nie przekazuje przeglądarce izolowanej wartości hosta.

## Aktualizacja i odinstalowanie

- Aktualizacja v1 zatrzymuje proces i zastępuje wyłącznie pliki programu; nie ma bazy do migracji.
- Od v2 aktualizacja tworzy zweryfikowaną kopię przed migracją schematu, a nieudana migracja nie usuwa starszej bazy ani kopii.
- Od v2 odinstalowanie programu domyślnie zachowuje dane użytkownika. Ich usunięcie jest osobną, jednoznacznie nazwaną operacją pokazującą lokalizację i zakres; nie może być domyślnie zaznaczone.

## Diagnostyka

- Logi nie zawierają treści promptów, notatek, danych rodzinnych, zdrowotnych ani finansowych.
- Komunikat startowy wskazuje kategorię problemu i bezpieczną drogę naprawy.
- Tryb diagnostyczny jest jawny i czasowy; nie zmienia zakresu nasłuchu.
