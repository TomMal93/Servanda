# ADR 0007: dystrybucja Linuksa dla v1

Status: accepted

Data: 2026-08-10

Zakres produktu: P1 i wydanie v1

## Kontekst

P1 wymaga pierwszego jawnego RID oraz uruchomienia Servandy z menu aplikacji bez Node, npm, SDK .NET i uprawnień administratora. Otwarte decyzje OPEN-001 i OPEN-003 pozostawiały wybór architektury oraz formy instalacji do czasu przygotowania artefaktu.

## Decyzja

Pierwszym artefaktem v1 jest publikacja `self-contained` dla RID `linux-x64`, nazwana `Servanda-linux-x64`. Odpowiada ona architekturze komputera używanego do budowy i weryfikacji P1. Kolejna architektura wymaga osobnego, niezależnie testowanego artefaktu z RID w nazwie.

Minimalnym ABI systemowym jest glibc 2.27, zgodnie z bazowym wymaganiem .NET 10 dla Linux x64. Artefakt nie obsługuje musl. Interop zależny od układu struktur systemowych pozostaje jawnie ograniczony do ABI x86-64; dodanie innej architektury wymaga osobnej implementacji i testu artefaktu.

V1 jest dystrybuowana jako przenośny katalog. Dołączony odwracalny skrypt użytkownika instaluje w `${XDG_DATA_HOME:-$HOME/.local/share}/applications` wyłącznie wpis `servanda.desktop`, wskazujący plik wykonywalny w bieżącym katalogu pakietu. Skrypt odinstalowujący usuwa wyłącznie ten wpis. Nie kopiuje, nie przenosi i nie usuwa katalogu programu ani przyszłych danych użytkownika.

## Odrzucone alternatywy

### Pakiet zależny od systemowego runtime .NET

Zmniejszyłby rozmiar pobierania, ale narusza wymaganie uruchomienia bez instalowania runtime'u lub SDK .NET.

### Instalator systemowy wymagający roota

Zapewniałby standardową lokalizację programu, ale zwiększałby zakres dystrybucji i naruszał wymaganie instalacji użytkownika bez uprawnień administratora.

### Skrypt kopiujący program do katalogu użytkownika

Ułatwiałby zachowanie stałej ścieżki, ale musiałby definiować atomową aktualizację działającego procesu. Przenośny katalog pozwala zamknąć P1 bez tworzenia przedwcześnie osobnego mechanizmu aktualizacji.

## Konsekwencje

- użytkownik musi zachować katalog pakietu w niezmienionej lokalizacji po instalacji skrótu,
- aktualizacja polega na zamknięciu Servandy i zastąpieniu katalogu programu; ponowne uruchomienie skryptu aktualizuje ścieżkę skrótu,
- pliki runtime i stan pozostają poza katalogiem programu zgodnie z XDG,
- artefakt może być uruchamiany bez wpisu `.desktop` bezpośrednio przez plik `Servanda`.

## Sposób weryfikacji

- `dotnet publish` z profilem `linux-x64` tworzy artefakt zawierający natywny plik `Servanda`, zasoby aplikacji i oba skrypty,
- uruchomienie pliku `Servanda` w środowisku bez SDK .NET uruchamia launcher,
- repozytoryjny test artefaktu uruchamia natywny host w oczyszczonym środowisku bez polecenia `dotnet`, potwierdza origin loopback i stan `ready`, a następnie sprawdza łagodne usunięcie deskryptora,
- instalacja skrótu nie wymaga roota, a jego usunięcie nie usuwa katalogu programu ani katalogów XDG Servandy,
- nazwa katalogu artefaktu zawiera `linux-x64`.
- test zgodności uruchamia artefakt na najstarszej wspieranej glibc 2.27 lub zgodnym, nadal wspieranym systemie o najbliższej wersji ABI.
