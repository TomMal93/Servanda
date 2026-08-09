# ADR 0002: import zastępujący całą kolekcję

Status: accepted

Data: 2026-08-09

Zakres produktu: v2

## Kontekst

V2 musi przenieść pełną kolekcję między bazami i bezpiecznie odtworzyć ją z eksportu. Import typu merge albo upsert wymagałby reguł rozstrzygania konfliktów dla każdego agregatu, obsługi częściowo wspólnych hierarchii, mapowania seedowanych obszarów i zachowania historii pochodzącej z kilku kolekcji. Taki zakres nie jest potrzebny w jednoosobowym wydaniu local-first.

Import nie może modyfikować kanonicznej bazy przed pełną walidacją dokumentu ani pozostawić częściowo zastąpionej kolekcji. Musi również unieważnić otwarte sesje edycji powiązane ze stanem sprzed importu.

## Rozważane opcje

### Merge

Zachowuje dane obecne w bazie i dodaje dane z dokumentu, lecz wymaga interfejsu rozwiązywania konfliktów nazw, identyfikatorów, hierarchii i historii. Nie daje prostego znaczenia pojęciu „odtwórz eksport”.

### Upsert po ULID

Aktualizuje rekordy o znanych identyfikatorach i dodaje pozostałe, ale nie rozstrzyga, co zrobić z rekordami nieobecnymi w eksporcie. Może też połączyć dane dwóch niezależnych kolekcji bez świadomej decyzji użytkownika.

### Pełne zastąpienie

Po walidacji usuwa dotychczasowe dane domenowe i odtwarza dokładnie kolekcję reprezentowaną przez dokument. Jest operacją destrukcyjną, ale ma jednoznaczny podgląd skutków, prostą atomowość i czytelne odzyskanie z kopii ochronnej.

## Decyzja

V2 obsługuje wyłącznie import zastępujący całą kolekcję. Merge, częściowy import i upsert nie należą do v2.

Import przebiega następująco:

1. Aplikacja odczytuje dokument bez zmiany kanonicznej bazy i sprawdza limit pliku, poprawność JSON oraz wersję koperty.
2. Dokument bieżącej wersji jest walidowany względem normatywnego JSON Schema. Starsza jawnie wspierana wersja jest najpierw przekształcana do bieżącego modelu; wersja przyszła albo nieznana jest odrzucana.
3. Wszystkie reguły relacyjne, limity domenowe i migawki wersji są sprawdzane w izolowanej bazie stagingowej utworzonej w katalogu tymczasowym aplikacji. Staging nigdy nie używa produkcyjnego katalogu XDG.
4. Aplikacja porównuje stabilne identyfikatory i pokazuje liczbę elementów dodawanych, zastępowanych i usuwanych w każdym rodzaju danych. Podgląd informuje również o unieważnieniu wszystkich otwartych sesji edycji.
5. Po potwierdzeniu aplikacja tworzy i weryfikuje kopię ochronną kanonicznej bazy.
6. Dane domenowe są zastępowane w jednej transakcji SQLite. Tabele techniczne EF Core i stan schematu bazy nie są importowane ani usuwane; zakresy kolejności i indeksy FTS są odbudowywane z importowanych danych w ramach tej samej operacji.
7. Po zatwierdzeniu transakcji aplikacja zmienia `content_epoch`, odświeża widoki i odrzuca późniejszy zapis z sesji edycji otwartej dla wcześniejszej kolekcji. Błąd przed zatwierdzeniem wycofuje całą transakcję.

Stabilne ULID, czasy utworzenia i aktualizacji są zachowywane. Techniczne wartości `revision` nie należą do formatu przenośnego; wszystkie importowane korzenie agregatów i odbudowane zakresy kolejności rozpoczynają w nowej bazie od `revision = 1`. Nowy `content_epoch` zapobiega potraktowaniu formularza otwartego przed importem jako aktualnego nawet wtedy, gdy jego identyfikator i rewizja są takie same.

Dokument musi zawierać oba wymagane aktywne obszary pierwszych modułów v2: `tools` i `prompts`, każdy dokładnie raz. Brak wymaganego obszaru, nieznany aktywny `module_key` albo relacja do nieistniejącego elementu powoduje odrzucenie całego dokumentu. Obszary planowane nieobecne w imporcie są usuwane jak pozostałe dane domenowe; importer nie odtwarza ich po cichu przez seed.

`applicationVersion` jest metadanym źródła, a nie kluczem zgodności. O możliwości importu rozstrzyga `schemaVersion` koperty i zestaw jawnie wspieranych adapterów.

## Zakres eksportu

Format eksportu w wersji 1, wprowadzany w produkcie v2, obejmuje:

- wszystkie aktywne, ukryte i zarchiwizowane obszary,
- wszystkie kategorie i tagi,
- narzędzia wraz z relacjami tagów,
- prompty wraz z relacjami tagów, wariantami, zmiennymi i zachowanymi wersjami,
- historię użycia promptów wraz z migawkami nazw.

Nie obejmuje migracji EF Core, `content_epoch`, blokad i deskryptorów runtime, logów, cache, kopii SQLite, plików eksportu ani otwartych sesji edycji.

## Konsekwencje

- Import ma jednoznaczny i testowalny skutek: po sukcesie dane domenowe odpowiadają dokumentowi.
- Każdy import wymaga świadomego potwierdzenia i zweryfikowanej kopii ochronnej.
- Użytkownik nie może użyć importu do łączenia dwóch kolekcji; taka funkcja wymaga osobnego kontraktu i interfejsu konfliktów.
- Eksport może być duży, ponieważ zachowuje wersje promptów i historię użycia. Limity i sposób przetwarzania muszą zapobiegać niekontrolowanemu zużyciu pamięci.
- Import nie zmienia wersji schematu fizycznej bazy; aplikacja importuje dane do schematu aktualnie obsługiwanego przez zainstalowaną wersję.

## Sposób weryfikacji

- eksport każdej obsługiwanej kolekcji przechodzi JSON Schema i walidację domenową,
- import do nowej bazy odtwarza wszystkie dane i relacje objęte formatem,
- element nieobecny w dokumencie znika po zatwierdzonym imporcie zastępującym,
- uszkodzony dokument, nieznana wersja, brak relacji albo przekroczenie limitu nie zmienia kanonicznej bazy,
- awaria w dowolnym miejscu przed zatwierdzeniem transakcji pozostawia poprzednią kolekcję,
- po imporcie formularz ze starszym `content_epoch` nie może zostać zapisany bez ponownego otwarcia edytora,
- kopia utworzona przed importem pozwala odtworzyć stan sprzed operacji.
