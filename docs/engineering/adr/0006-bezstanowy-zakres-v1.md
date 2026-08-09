# ADR 0006: bezstanowy zakres v1

Status: accepted

Data: 2026-08-09

Zakres produktu: v1 i granica v2

## Kontekst

Pierwotny zakres v1 łączył budowę lokalnego hosta i systemu UI z SQLite, migracjami, kopiami, recovery, importem, eksportem oraz pierwszymi modułami. Utrudniało to wydanie i wymagało zamknięcia całego kontraktu trwałości przed zweryfikowaniem launchera, bezpieczeństwa loopbacku i powłoki.

Utworzenie w v1 prowizorycznej bazy tylko dla kafli lub ustawień stworzyłoby stan wymagający migracji do właściwego modelu v2. Nie ma danych domenowych, które uzasadniałyby taki koszt.

## Decyzja

V1 jest bezstanową powłoką Servandy:

- uruchamia bezpieczny lokalny host i launcher,
- prezentuje system UI oraz statyczny pulpit,
- pokazuje wszystkie obszary jako „Planowane”,
- nie tworzy SQLite ani innego kanonicznego magazynu,
- nie udostępnia edytorów, zarządzania obszarami, migracji, kopii, recovery, importu ani eksportu.

V1 może zapisywać wyłącznie prywatne pliki runtime potrzebne do cyklu życia procesu oraz bezpieczne logi techniczne. Nie tworzy magazynu ustawień użytkownika.

V2 wprowadza pierwszy kanoniczny model SQLite, wersjonowane migracje, kopie i recovery. Import i eksport powstają razem z pierwszymi działającymi modułami. Ponieważ v1 nie ma kolekcji, przejście do v2 nie wymaga migracji danych użytkownika z v1.

## Odrzucone alternatywy

### Pełny zakres danych i modułów w v1

Zapewniałby użyteczną pętlę treści już w pierwszym wydaniu, ale łączył w jednym progu ryzyko hosta, UI, modelu danych, odzyskiwania i dwóch modułów.

### Tymczasowa baza v1 dla obszarów lub ustawień

Pozwalałaby zachować edycję pulpitu, ale tworzyłaby drugi, krótkowieczny kontrakt danych oraz obowiązek jego migracji. Statyczny pulpit nie potrzebuje takiego magazynu.

### Web Storage dla ustawień v1

Ograniczyłby kod serwera, ale wiązałby stan z profilem przeglądarki i tworzył konkurencyjne źródło trwałości przed SQLite.

## Konsekwencje

- V1 weryfikuje dystrybucję, bezpieczeństwo hosta, cykl życia procesu, dostępność i kierunek wizualny, ale nie realizuje jeszcze pętli „zapisz — znajdź — użyj”.
- Wszystkie działania sugerujące trwały zapis są w v1 nieobecne, a nie tylko wyłączone bez wyjaśnienia.
- P1 i P2 mogą powstać bez EF Core i dostawcy SQLite; projekty docelowych warstw mogą istnieć jako część struktury rozwiązania, lecz nie zawierają prowizorycznego modelu domenowego.
- Pierwsza migracja v2 tworzy schemat od pustego stanu; pierwsza migracja aktualizacyjna dotyczy kolejnej wersji schematu v2, nie przejścia z bezstanowego v1.

## Sposób weryfikacji

- artefakt v1 nie tworzy pliku SQLite, kopii ani eksportu,
- interfejs v1 nie zawiera edytorów, importu, eksportu, recovery ani akcji zarządzania obszarami,
- wszystkie kafle v1 mają widoczny status „Planowane” i nie otwierają pustych modułów,
- ponowne uruchomienie v1 weryfikuje wyłącznie cykl życia procesu, a nie trwałość kolekcji,
- utworzenie pierwszej bazy następuje dopiero w P3 v2.
