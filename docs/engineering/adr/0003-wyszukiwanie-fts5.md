# ADR 0003: lokalne wyszukiwanie przez SQLite FTS5

Status: accepted

Data: 2026-08-09

Zakres produktu: v2

## Kontekst

Biblioteka promptów przeszukuje pełną treść bieżących wariantów, a katalog narzędzi — nazwy, opisy, adresy, kategorie i tagi. Skanowanie tych pól przez `LIKE '%...%'` nie zapewnia przewidywalnego czasu odpowiedzi i nie daje poprawnej, wspólnej semantyki wielkości liter oraz polskich znaków.

Wyszukiwanie pozostaje lokalne, musi działać w SQLite i nie może wymagać osobnej usługi ani indeksu poza kanoniczną bazą.

## Rozważane opcje

### `LIKE` na tabelach domenowych

Jest proste dla małych danych, lecz prowadzi do pełnych skanów przy wyszukiwaniu fragmentów i wymaga powielania reguł normalizacji w wielu zapytaniach. Domyślne zachowanie SQLite poza ASCII nie spełnia kontraktu polskich znaków.

### FTS5 z tokenizerem trigramowym

Zapewnia wyszukiwanie dowolnego fragmentu tekstu, ale zwiększa indeks, komplikuje krótkie zapytania i nie jest potrzebne do podstawowej pętli „wpisz początek zapamiętanego słowa”.

### FTS5 z dokumentem wyszukiwania na agregat

Zapewnia indeks tokenów, zapytania prefiksowe, ranking BM25 i pozostaje częścią SQLite. Dokument może agregować pola należące do kilku tabel domenowych, a jego przebudowa może uczestniczyć w tej samej transakcji co zapis agregatu.

## Decyzja

Pierwsze wydanie używa SQLite FTS5 z konfiguracją `tokenize='unicode61 remove_diacritics 0'` oraz indeksami prefiksów długości 2, 3 i 4. Usuwanie znaków diakrytycznych realizuje jedna funkcja aplikacyjna, dzięki czemu indeks i zapytanie mają identyczną regułę obejmującą również `ł`. Zapytanie użytkownika nie jest przekazywane jako surowa składnia FTS. Aplikacja normalizuje i tokenizuje zwykły tekst, bezpiecznie buduje koniunkcję prefiksów, a wszystkie wartości przekazuje parametrami.

Przed indeksowaniem aplikacja tworzy pochodną postać tekstu:

1. normalizuje Unicode do formy kompatybilnej NFKD,
2. stosuje niezmienną kulturowo normalizację wielkości liter,
3. usuwa znaki łączące alfabetu łacińskiego,
4. jawnie mapuje `ą→a`, `ć→c`, `ę→e`, `ł→l`, `ń→n`, `ó→o`, `ś→s`, `ź→z` i `ż→z`, również dla wielkich liter,
5. pozostawia podział liter i cyfr tokenizerowi `unicode61` oraz normalizuje kolejne separatory.

Ta sama funkcja normalizuje treść indeksowaną i zapytanie. Oryginalne wartości domenowe nie są zmieniane. Reguły mają testy dla postaci złożonych i rozłożonych Unicode, wszystkich polskich znaków, wielkości liter, adresów URL i tekstu przypominającego operatory FTS.

Powstają dwie zwykłe, aplikacyjnie utrzymywane tabele wirtualne FTS5:

- jeden dokument `tool_search` na narzędzie,
- jeden dokument `prompt_search` na prompt, zawierający połączone pola wszystkich jego bieżących wariantów.

Indeks przechowuje pochodną projekcję wyszukiwalnych pól oraz domenowy identyfikator jako pole `UNINDEXED`. Nie jest zewnętrzną tabelą content FTS5. Dodanie, edycja, przeniesienie, zmiana tagu lub kategorii i usunięcie elementu aktualizują odpowiedni dokument indeksu w tej samej transakcji co dane domenowe. Import buduje indeks w stagingu, a migracja tworząca indeks wykonuje pełny rebuild.

Wagi BM25 są częścią pierwszej implementacji i mogą zostać skorygowane wyłącznie na podstawie testowego korpusu bez zmiany kolejności grup ważności z kontraktu produktu. Początkowe wagi:

| Dokument | Pole | Waga |
|---|---|---:|
| narzędzie | nazwa | 10 |
| narzędzie | tagi | 6 |
| narzędzie | ścieżka kategorii | 5 |
| narzędzie | URL | 3 |
| narzędzie | opis | 2 |
| prompt | tytuł | 10 |
| prompt | tagi | 6 |
| prompt | ścieżka kategorii | 5 |
| prompt | nazwy wariantów | 4 |
| prompt | przeznaczenia wariantów | 3 |
| prompt | opis | 2 |
| prompt | treść wariantów | 1 |

Aplikacja sprawdza dostępność FTS5 podczas testu artefaktu i przy inicjalizacji infrastruktury. Brak FTS5 jest błędem niezgodnego artefaktu, a nie powodem do cichego przejścia na pełny skan `LIKE`.

## Spójność i odbudowa

Indeks jest danymi pochodnymi i nie wchodzi do eksportu. Musi dać się odbudować wyłącznie z tabel domenowych. Narzędzie diagnostyczne i testy infrastruktury zapewniają:

- pełny rebuild obu indeksów w transakcji,
- porównanie liczby dokumentów z niearchiwalnymi i archiwalnymi elementami podlegającymi wyszukiwaniu,
- test reprezentatywnych wartości każdego indeksowanego pola,
- polecenie integralności FTS5,
- wykrycie i naprawę indeksu po nieudanej aktualizacji lub migracji bez zmiany danych domenowych.

## Konsekwencje

- Baza przechowuje dodatkową, odbudowywalną kopię tekstu wyszukiwawczego.
- Zmiana nazwy wspólnej kategorii lub tagu może aktualizować wiele dokumentów FTS, dlatego musi być wykonana transakcyjnie i zmierzona na zbiorze granicznym.
- Wyszukiwanie gwarantuje tokeny i prefiksy, nie dowolne fragmenty ze środka słowa.
- Pełna treść wszystkich bieżących wariantów jest przeszukiwalna bez pobierania jej do komponentu Blazor.
- Zmiana tokenizera albo normalizacji wymaga migracji przebudowującej indeks oraz testów zgodności zapytań.

## Sposób weryfikacji

- test artefaktu potwierdza obecność FTS5,
- ten sam korpus zwraca te same wyniki dla `Łódź`, `łódź` i `lodz`,
- zapis agregatu i dokumentu FTS jest atomowy,
- odbudowa indeksu daje te same wyniki i kolejność co indeks utrzymywany przyrostowo,
- plan zapytania i pomiary spełniają budżety z `quality-requirements.md`,
- zapytanie użytkownika zawierające cudzysłowy, minusy, gwiazdki lub słowa `AND`, `OR`, `NOT` nie jest wykonywane jako surowa składnia FTS.

## Źródło techniczne

Mechanizm opiera się na oficjalnym opisie [SQLite FTS5](https://www.sqlite.org/fts5.html), w szczególności tokenizerze `unicode61`, indeksach prefiksowych, rankingu BM25, odbudowie i kontroli integralności.
