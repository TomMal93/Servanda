# ADR 0004: agregaty, rewizje i współbieżna zmiana kolejności

Status: accepted

Data: 2026-08-09

Zakres produktu: v2

## Kontekst

Interfejs Blazor może działać w kilku kartach, a każda komenda używa krótkotrwałego `DbContext`. Edytor otrzymuje odłączony model, więc zapis musi porównać rewizję widzianą przy otwarciu z bieżącą wartością w SQLite.

Nie każdy wiersz jest jednak niezależnie edytowany. Warianty, zmienne i relacje tagów promptu są zapisywane jako część promptu, a zmiana kolejności może dotknąć wiele rodzeństwa jednocześnie. Zwiększanie rewizji każdego przenumerowanego rekordu powodowałoby konflikty niezwiązane ze zmianą jego treści; sprawdzanie tylko rewizji przenoszonego elementu nie wykrywałoby równoległej zmiany tej samej listy.

## Rozważane opcje

### Rewizja każdego fizycznego wiersza

Jest mechanicznie jednolita, ale wystawia szczegóły tabel dzieci do komend aplikacyjnych. Reorder N elementów wymaga przesłania i zmiany N rewizji, a edytor treści może konfliktować wyłącznie dlatego, że inna karta zmieniła pozycję elementu.

### Rewizja wyłącznie korzenia agregatu

Dobrze chroni prompt wraz z wariantami i zmiennymi, ale nie daje jednego korzenia dla kolejności obszarów, kategorii ani elementów należących do wspólnej listy.

### Rewizje korzeni i osobne rewizje zakresów kolejności

Treść jest chroniona przez rewizję agregatu, a członkostwo i kolejność listy — przez techniczny rekord zakresu. Komenda przeniesienia może atomowo sprawdzić jeden albo dwa zakresy bez zmiany rewizji treści wszystkich rodzeństw.

## Decyzja

Optymistyczna współbieżność używa dwóch rodzajów tokenów:

- `revision` korzenia agregatu chroni jego edytowalną treść i dane posiadane,
- `ordering_scopes.revision` chroni członkostwo oraz `sort_order` elementów w jednej uporządkowanej liście.

Granice agregatów v2:

| Korzeń | Dane posiadane lub operacje |
|---|---|
| obszar | nazwa, opis, ikona, akcent, widoczność, dostępność i archiwizacja |
| kategoria | nazwa, opis i relacja rodzica; usunięcie poddrzewa jest osobną operacją masową |
| tag | nazwa i znormalizowana nazwa |
| narzędzie | pola narzędzia i relacje `tool_tags` |
| prompt | pola promptu, `prompt_tags`, bieżące warianty, zmienne i tworzenie lub przywracanie wersji |

`prompt_variants`, `prompt_variables`, `tool_tags` i `prompt_tags` nie mają własnej `revision`. Każda ich zmiana zwiększa rewizję odpowiednio promptu albo narzędzia. `prompt_versions` i `prompt_usage` są rekordami historii, a nie osobnymi edytowalnymi agregatami. Dodanie wpisu użycia i retencja historii nie zwiększają rewizji promptu.

Zmiana nazwy kategorii lub tagu zwiększa wyłącznie rewizję tej kategorii albo tagu. Aktualizacja zależnych dokumentów FTS jest pochodnym skutkiem tej samej transakcji i nie zwiększa rewizji narzędzi ani promptów.

Szybka komenda zmiany `is_favorite` przekazuje oczekiwaną rewizję promptu i zwiększa ją po sukcesie. Jeżeli w innej karcie trwa edycja tego promptu, późniejszy zapis starego formularza kończy się konfliktem zamiast przywrócić poprzedni stan ulubionego.

## Zakresy kolejności

Techniczna tabela `ordering_scopes` przechowuje `scope_key`, `revision` i `updated_at`. Klucz ma kanoniczną postać:

- `areas`,
- `categories:{areaId}:{parentId|root}`,
- `tools:{categoryId}:{featured|regular}`,
- `prompts:{categoryId}`.

Kolejność wariantów i zmiennych należy do agregatu promptu i nie otrzymuje osobnego zakresu. Ich reorder jest zwykłym zapisem całego promptu z oczekiwaną rewizją promptu.

Każda lista używa gęstych, nieujemnych wartości `sort_order` od `0` do `n-1` oraz deterministycznego odczytu `ORDER BY sort_order, id`. Ograniczenia unikalne właściwe zakresowi zapobiegają dwóm elementom o tej samej pozycji. Dodanie elementu domyślnie umieszcza go na końcu listy.

Zwykła komenda edycji treści nie zapisuje `sort_order` ani pól członkostwa kontrolowanych przez zakres, takich jak `parent_id`, `category_id` lub `group_key`. Zmieniają je wyłącznie dedykowane komendy utworzenia, przeniesienia, reorderu i usunięcia z oczekiwanymi rewizjami zakresów. Dzięki temu formularz otwarty przed reorderem nie może później przywrócić starej pozycji przy zapisie treści. Wyjątkiem są warianty i zmienne, których kolejność należy do zapisywanego agregatu promptu.

Komenda reorderu przekazuje:

- identyfikator przenoszonego elementu,
- klucz i oczekiwaną rewizję zakresu źródłowego,
- klucz i oczekiwaną rewizję zakresu docelowego, jeżeli są różne,
- identyfikator elementu, przed którym należy wstawić, albo jawne polecenie dopisania na końcu.

Serwer ładuje aktualne członkostwo obu zakresów, sprawdza ich rewizje i obecność elementu oraz kotwicy, wylicza pełną kolejność i przenumerowuje ją w jednej transakcji. Sukces zwiększa rewizję każdego zmienionego zakresu dokładnie o 1. Konflikt któregokolwiek zakresu wycofuje całą operację i zwraca aktualną listę do odświeżenia; automatyczne ponowienie na nowej kolejności nie jest dozwolone.

Reorder w tym samym zakresie zmienia `sort_order` i rewizję zakresu, ale nie `revision` ani `updated_at` treści poszczególnych elementów. Przeniesienie do innego rodzica, kategorii lub grupy zmienia także członkostwo agregatu przenoszonego elementu, dlatego wymaga jego oczekiwanej `revision` i zwiększa ją wraz z `updated_at`. Utworzenie, usunięcie lub trwałe odłączenie elementu również zwiększa rewizję odpowiednich zakresów.

Komenda tworzenia przekazuje oczekiwaną rewizję zakresu docelowego i dopisuje element na końcu. Komenda usunięcia przekazuje oczekiwaną rewizję korzenia oraz wszystkich zakresów, z których usuwa członkostwo. Brak zgodności któregokolwiek tokenu wycofuje całą operację.

## Implementacja EF Core

`revision` jest aplikacyjnie zarządzanym tokenem współbieżności, ponieważ SQLite nie zapewnia automatycznego odpowiednika `rowversion`. Implementacja może:

- załadować korzeń w krótkotrwałym śledzącym `DbContext`, porównać oczekiwaną rewizję i zapisać zmianę z tokenem współbieżności, albo
- wykonać warunkowe `UPDATE ... WHERE id = ... AND revision = ...` i uznać zero zmienionych wierszy za konflikt.

Jeżeli odłączona encja zostaje dołączona bez ponownego odczytu, oczekiwana rewizja z komendy MUSI zostać ustawiona jako `OriginalValue` tokenu. Ustawienie jej wyłącznie jako wartości bieżącej nie zapewnia właściwego warunku `WHERE`. Komenda nie może automatycznie odświeżyć tokenu i ponowić zapisu według zasady „client wins”.

## Konsekwencje

- Edycja treści nie konfliktuje wyłącznie z przenumerowaniem tej samej listy.
- Dwie równoległe zmiany tej samej kolejności nie nadpisują się po cichu.
- Przeniesienie między listami atomowo chroni zakres źródłowy, docelowy i treść przenoszonego elementu.
- `ordering_scopes` jest stanem technicznym: nie wchodzi do eksportu, a po imporcie lub pełnej odbudowie każdy istniejący zakres zaczyna od rewizji 1.
- Gęste przenumerowanie może aktualizować wiele wierszy i musi być mierzone na profilu granicznym, ale upraszcza unikalność, import i deterministyczne testy.

## Sposób weryfikacji

- zmiana wariantu, zmiennej albo relacji tagu zwiększa tylko rewizję właściwego korzenia,
- równoległe przełączenie ulubionego i zapis edytora powodują konflikt starej komendy,
- dwa reorder'y z tą samą rewizją zakresu dają dokładnie jeden sukces,
- przeniesienie między zakresami nie pozostawia elementu w obu ani w żadnym z nich,
- konflikt zakresu docelowego wycofuje również zmiany zakresu źródłowego i agregatu,
- po dodaniu, usunięciu i reorderze wartości są unikalne, gęste i odczytywane deterministycznie,
- test odłączonego zapisu potwierdza, że oczekiwana rewizja trafia do warunku aktualizacji.

## Źródła techniczne

Decyzja opiera się na oficjalnej dokumentacji EF Core dotyczącej [obsługi konfliktów współbieżności](https://learn.microsoft.com/ef/core/saving/concurrency), [wartości oryginalnych odłączonych encji](https://learn.microsoft.com/ef/core/change-tracking/entity-entries) i [warunkowych aktualizacji bez change trackera](https://learn.microsoft.com/ef/core/saving/execute-insert-update-delete#concurrency-control-and-rows-affected).
