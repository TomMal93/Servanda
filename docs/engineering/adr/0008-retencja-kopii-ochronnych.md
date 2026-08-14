# ADR 0008: retencja kopii ochronnych

Status: accepted

Data: 2026-08-14

Zakres produktu: P3 i v2

## Kontekst

Operacje zmieniające schemat albo większą część kolekcji wymagają zweryfikowanej kopii SQLite. Bez retencji kolejne migracje, importy, resety i operacje masowe powodowałyby nieograniczony wzrost katalogu danych. Zbyt agresywne porządkowanie mogłoby natomiast usunąć jedyny zgodny punkt odzyskania albo materiał potrzebny do diagnozy uszkodzonej lub nowszej kopii.

Retencja musi być rozdzielona od tworzenia kopii i od operacji, którą kopia chroni. Utworzenie poprawnej kopii jest warunkiem rozpoczęcia operacji danych, natomiast niepowodzenie późniejszego porządkowania nie może zmienić udanej operacji w awarię bazy ani uruchomić trybu `recovery`.

## Decyzja

Automatyczna retencja obejmuje wyłącznie kopie o powodzie innym niż `manual`, które bezpośrednio przed kwalifikacją przeszły weryfikację integralności, kluczy obcych i zgodności historii migracji. Aplikacja zachowuje:

- 10 najnowszych zweryfikowanych kopii ochronnych niezależnie od daty,
- najnowszą zweryfikowaną kopię ochronną z każdego dnia UTC w ruchomym oknie ostatnich 30 dni,
- wszystkie kopie ręczne,
- wszystkie kopie niezgodne, niepoprawne, nieczytelne lub datowane w przyszłości względem zegara aplikacji.

Ten sam identyfikator może spełniać regułę ostatnich operacji i kopii dziennej. Okno 30 dni obejmuje bieżący dzień UTC i 29 poprzednich dni. Retencja nie tworzy kopii na podstawie kalendarza; zachowuje dzienny punkt spośród kopii powstałych z rzeczywistych operacji ochronnych.

Porządkowanie uruchamia się po udanej normalnej inicjalizacji bazy oraz będzie uruchamiane po zakończeniu przyszłych operacji chronionych. Nie uruchamia się podczas tworzenia kopii, migracji, importu, resetu, operacji masowej ani w stanie `recovery`. Bezpośrednio przed usunięciem kandydat jest ponownie weryfikowany. Zniknięcie, zmiana albo błąd dostępu powodują pozostawienie katalogu; niepowodzenie usunięcia nie blokuje startu i nie usuwa kolejnych danych na siłę.

## Odrzucone alternatywy

### Limit wyłącznie liczbowy

Prosty limit katalogów jest przewidywalny, ale seria operacji w jednym dniu mogłaby usunąć wszystkie starsze punkty czasowe. Połączenie ostatnich 10 operacji z rotacją dzienną zachowuje zarówno krótką, jak i miesięczną historię.

### Automatyczne usuwanie kopii ręcznych

Pozwoliłoby ograniczyć zajętość dysku, ale odbierałoby użytkownikowi jawnie utworzony punkt odzyskania bez osobnego interfejsu i potwierdzenia. Kopie ręczne pozostają poza automatyczną retencją.

### Usuwanie kopii niezgodnych lub uszkodzonych

Takie katalogi nie są automatycznymi kandydatami recovery, ale mogą pochodzić z nowszej wersji aplikacji albo być materiałem diagnostycznym. Automatyczna retencja nie rozstrzyga ich wartości i ich nie usuwa.

## Konsekwencje

- liczba zweryfikowanych kopii ochronnych jest ograniczana, ale katalog nie ma twardego limitu rozmiaru ze względu na zachowanie kopii ręcznych i problematycznych,
- co najmniej 10 najnowszych zgodnych kopii ochronnych pozostaje dostępnych, więc retencja nie usuwa jedynej poprawnej kopii,
- zmiana wartości 10 lub 30 zmienia kontrakt odzyskiwania i wymaga aktualizacji tego ADR, modelu danych oraz testów,
- przyszłe przypadki użycia importu, resetu i operacji masowych muszą wywołać retencję dopiero po własnym pomyślnym zakończeniu.

## Sposób weryfikacji

- test polityki potwierdza zachowanie 10 najnowszych kopii oraz jednej najnowszej na dzień w 30-dniowym oknie,
- test integracyjny potwierdza fizyczne usunięcie wyłącznie nadmiarowych, ponownie zweryfikowanych kopii ochronnych,
- testy potwierdzają zachowanie kopii ręcznych, niepoprawnych i niezgodnych,
- test migracji potwierdza, że kopia chroniąca migrację istnieje i jest zweryfikowana po zakończeniu inicjalizacji.
