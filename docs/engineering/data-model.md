# Model danych Servandy

> Status: obowiązujący dla v2  
> Magazyn kanoniczny: lokalna baza SQLite

## Zasady ogólne

- Jedna instalacja użytkownika korzysta z jednej kanonicznej bazy `servanda.db`.
- Baza znajduje się w prywatnym katalogu danych użytkownika opisanym w `linux-deployment.md`, nigdy w katalogu plików wykonywalnych ani repozytorium.
- Wszystkie identyfikatory domenowe są stabilnymi tekstowymi ULID generowanymi przez aplikację.
- Daty trwałe są zapisywane w UTC. Interfejs prezentuje je w lokalnej strefie użytkownika.
- Każdy niezależnie edytowalny korzeń agregatu ma `created_at`, `updated_at` i dodatnią całkowitą `revision` zwiększaną przy zmianie jego treści. Dzieci posiadane, relacje łącznikowe, rekordy historii i techniczne projekcje mogą nie mieć własnej rewizji; ich właściciela i skutek zapisu określa sekcja „Kontrola współbieżności”.
- Relacje korzystają z kluczy obcych, a po otwarciu połączenia aplikacja włącza `PRAGMA foreign_keys = ON`.
- Usunięcie danych użytkownika nie może wynikać wyłącznie z kaskady technicznej. Reguła archiwizacji albo usunięcia należy do kontraktu funkcji.
- Zmiana schematu wymaga migracji, testu migracji oraz aktualizacji tego dokumentu.

Nazwy tabel i kolumn poniżej są kontraktem logicznym. Drobna zmiana nazwy fizycznej jest dozwolona tylko wtedy, gdy mapowanie pozostaje jednoznaczne i nie zmienia reguł domenowych.

## Stan kolekcji

### `app_state`

Tabela zawiera dokładnie jeden techniczny rekord z polem `content_epoch` będącym ULID. Wartość powstaje przy utworzeniu bazy i zmienia się po każdej operacji zastępującej całą kolekcję, w tym po imporcie i resecie. Nie jest eksportowana.

Każda serwerowa sesja edycji i komenda zapisu przekazuje `content_epoch` obok identyfikatora agregatu i jego bazowej `revision`. Niezgodny epoch oznacza formularz otwarty przed zastąpieniem kolekcji i blokuje zapis nawet wtedy, gdy identyfikator oraz rewizja przypadkowo są zgodne. Zmiana epoch odbywa się w tej samej transakcji co operacja zastępująca dane. Pierwsze wydanie nie utrwala sesji edycji w Web Storage.

### `ordering_scopes`

Techniczna tabela przechowuje `scope_key` jako klucz główny, dodatnią `revision` i `updated_at`. Jeden rekord chroni członkostwo oraz kolejność jednej listy:

- `areas`,
- `categories:{areaId}:{parentId|root}`,
- `tools:{categoryId}:{featured|regular}`,
- `prompts:{categoryId}`.

Zakres powstaje wraz z właścicielem listy, również gdy lista jest pusta: `areas` przy inicjalizacji bazy, zakres kategorii głównych wraz z obszarem, a zakresy dzieci i elementów modułu wraz z kategorią. Dzięki temu dwie próby dodania pierwszego elementu używają tego samego tokenu. Zakres jest aktualizowany zgodnie z [ADR 0004](adr/0004-agregaty-rewizje-i-kolejnosc.md). Jest stanem współbieżności, nie danymi użytkownika: nie wchodzi do eksportu, a import i odbudowa tworzą istniejące zakresy z `revision = 1`.

## Katalogi i pliki

```text
<XDG_DATA_HOME>/servanda/
├── servanda.db
├── backups/
└── exports/
```

Jeżeli `XDG_DATA_HOME` nie jest ustawione, aplikacja używa standardowego linuksowego katalogu `~/.local/share/servanda/`. Katalog aplikacji powinien mieć uprawnienia `0700`, a baza, kopie i eksporty zawierające dane prywatne — `0600`, o ile system plików obsługuje uprawnienia POSIX.

## Wersja schematu i migracje

- Wersję schematu przechowuje mechanizm migracji EF Core.
- Migracje są jednokierunkowe w normalnym uruchomieniu; cofnięcie wersji odbywa się przez odtworzenie zgodnej kopii, nie przez automatyczny downgrade.
- Przed pierwszą migracją zmieniającą istniejącą bazę aplikacja tworzy zweryfikowaną kopię.
- Migracja odbywa się przed udostępnieniem normalnego routera aplikacji i przyjęciem interaktywnych połączeń do funkcji domenowych.
- Błąd otwarcia bazy lub migracji zatrzymuje normalny start, zachowuje bazę i kopię oraz przełącza host w ograniczony stan `recovery` opisany w `linux-deployment.md`. Ekran odzyskiwania nie ujawnia surowego wyjątku ani nie pozwala korzystać z normalnych operacji domenowych.
- Każda migracja ma test od pustej bazy oraz od poprzedniej wspieranej wersji.

## Obszary

### `areas`

| Pole | Typ | Reguła |
|---|---|---|
| `id` | text | ULID, klucz główny |
| `name` | text | wymagane, 1–80 znaków |
| `description` | text | maks. 300 znaków |
| `icon_key` | text | klucz ikony z obsługiwanego zestawu, maks. 60 znaków |
| `accent_key` | text | klucz akcentu z systemu UI, maks. 40 znaków |
| `module_key` | text | stabilny klucz rodzaju modułu, maks. 60 znaków, np. `prompts`, `tools`, `home`; nie wynika z nazwy |
| `availability` | text | `active` albo `planned` |
| `sort_order` | integer | kolejność w pulpicie i panelu, nieujemna |
| `is_hidden` | integer | boolean; ukrywa kafel bez archiwizacji |
| `archived_at` | text/null | UTC; wartość oznacza archiwizację |
| `created_at`, `updated_at` | text | UTC ISO 8601 |
| `revision` | integer | dodatnia wersja do kontroli współbieżności |

Reguły:

- początkowy zestaw obszarów jest seedowany idempotentnie przy tworzeniu nowej bazy,
- zmiana nazwy nie zmienia `id` ani `module_key`,
- aktywny `module_key` może wskazywać tylko moduł zarejestrowany w aplikacji,
- jeden aktywny `module_key` może być przypisany tylko do jednego niearchiwalnego obszaru; wiele obszarów planowanych może używać klucza `custom`,
- `sort_order` jest unikalne w całej liście obszarów, a wartości tworzą gęsty zakres od `0` do `n-1`; odczyt używa `ORDER BY sort_order, id`,
- obszar planowany nie ma tabel domenowych udających treść modułu,
- archiwizacja zachowuje rekord i powiązane dane,
- trwałe usunięcie obszaru nie należy do v2.

## Wspólne kategorie i tagi

### `categories`

| Pole | Typ | Reguła |
|---|---|---|
| `id` | text | ULID, klucz główny |
| `area_id` | text | wymagany klucz obcy do `areas` |
| `parent_id` | text/null | kategoria nadrzędna w tym samym obszarze |
| `name` | text | wymagane, 1–60 znaków |
| `description` | text | maks. 240 znaków |
| `sort_order` | integer | nieujemna kolejność rodzeństwa |
| `created_at`, `updated_at` | text | UTC ISO 8601 |
| `revision` | integer | dodatnia wersja |

Maksymalna głębokość drzewa wynosi 12. Kategoria nie może stać się własnym przodkiem. `sort_order` tworzy gęsty zakres od `0` do `n-1` wśród rodzeństwa, a odczyt używa `ORDER BY sort_order, id`. Unikalność zapewniają dwa indeksy: `(area_id, sort_order)` dla `parent_id IS NULL` oraz `(area_id, parent_id, sort_order)` dla `parent_id IS NOT NULL`, aby semantyka `NULL` w SQLite nie osłabiała ograniczenia korzeni.

Relacja rodzica oraz relacje elementów do kategorii MUSZĄ potwierdzać zgodność `area_id` przez ograniczenie złożone w bazie albo równoważną, transakcyjnie testowaną regułę integralności. Sam klucz `category_id` bez sprawdzenia obszaru jest niewystarczający.

### `tags`

Tag ma `id`, `area_id`, `name` długości 1–60 znaków, znormalizowane `normalized_name` o maksymalnej długości 60 znaków, pola audytowe i dodatnią `revision`. `normalized_name` jest unikalne w obrębie obszaru. Tag jest osobnym korzeniem agregatu. Relacje wiele-do-wielu używają osobnych tabel łącznikowych właściwych modułowi i należą do agregatu narzędzia albo promptu, nie tagu.

## Przechowalnia narzędzi

### `tools`

| Pole | Typ | Reguła |
|---|---|---|
| `id` | text | ULID, klucz główny |
| `area_id` | text | aktywny obszar modułu narzędzi |
| `category_id` | text | kategoria należąca do tego samego obszaru |
| `name` | text | wymagane, maks. 70 znaków |
| `description` | text | wymagane, maks. 280 znaków |
| `url` | text | poprawny bezwzględny URL HTTP(S), maks. 2048 znaków |
| `group_key` | text | `featured` albo `regular` |
| `sort_order` | integer | kolejność w grupie i kategorii |
| `created_at`, `updated_at` | text | UTC ISO 8601 |
| `revision` | integer | dodatnia wersja |

`tool_tags(tool_id, tag_id)` ma unikalną parę kluczy. Narzędzie może mieć maksymalnie 8 tagów. Reguły URL obowiązują zarówno w formularzu, jak i warstwie domenowej przed zapisem.

W obrębie `(category_id, group_key)` wartości `sort_order` są unikalne i gęste od `0` do `n-1`. Odczyt używa `ORDER BY sort_order, id`. Zmiana relacji `tool_tags` zwiększa rewizję narzędzia; wiersz łącznikowy nie ma własnej rewizji.

## Skarbiec promptów

### `prompts`

| Pole | Typ | Reguła |
|---|---|---|
| `id` | text | ULID, klucz główny |
| `area_id` | text | aktywny obszar modułu promptów |
| `category_id` | text | kategoria należąca do tego samego obszaru |
| `title` | text | wymagane, maks. 100 znaków |
| `description` | text | wymagane, maks. 400 znaków |
| `is_favorite` | integer | boolean |
| `sort_order` | integer | kolejność w kategorii |
| `created_at`, `updated_at` | text | UTC ISO 8601 |
| `revision` | integer | dodatnia wersja |

Prompt ma maksymalnie 12 tagów przez `prompt_tags(prompt_id, tag_id)`.

W obrębie `category_id` wartości `sort_order` promptów są unikalne i gęste od `0` do `n-1`. Odczyt używa `ORDER BY sort_order, id`. Zmiana relacji `prompt_tags` zwiększa rewizję promptu; wiersz łącznikowy nie ma własnej rewizji.

### `prompt_variants`

- `id`, `prompt_id`, `name`, opcjonalne `target`, `content`, `sort_order` i pola audytowe;
- nazwa ma maks. 80 znaków, `target` 80 znaków, a `content` 30 000 znaków;
- prompt ma od 1 do 20 wariantów;
- usunięcie ostatniego wariantu jest odrzucane;
- wariant jest dzieckiem promptu i nie ma własnej `revision`; każda zmiana zwiększa rewizję promptu;
- `sort_order` jest unikalne w promptcie, gęste od `0` do `n-1`, a odczyt używa `ORDER BY sort_order, id`.

### `prompt_variables`

- `id`, `prompt_id`, `name`, `label`, `default_value`, `is_required`, `is_multiline`, `sort_order` i pola audytowe;
- `name` ma maks. 50 znaków, zaczyna się literą lub `_`, dalej dopuszcza litery, cyfry, `_` i `-`;
- nazwa zmiennej jest unikalna w promptcie;
- prompt ma maksymalnie 50 zmiennych;
- etykieta ma maks. 80 znaków, a wartość domyślna 4000 znaków;
- zmienna jest dzieckiem promptu i nie ma własnej `revision`; każda zmiana zwiększa rewizję promptu;
- `sort_order` jest unikalne w promptcie, gęste od `0` do `n-1`, a odczyt używa `ORDER BY sort_order, id`.

Znacznik `{{name}}` w wariancie wskazuje użycie zmiennej. Nieużywana konfiguracja może pozostać zapisana dopiero po jawnym potwierdzeniu edytora; niezdefiniowany znacznik jest wykrywany przed zapisem.

### `prompt_versions`

Wersja przechowuje `id`, `prompt_id`, `created_at` oraz serializowaną migawkę wariantów i zmiennych w polu `snapshot_json`. Migawka ma własne `schema_version` i jest walidowana przed przywróceniem. Wersja jest nieedytowalnym rekordem historii i nie ma `updated_at` ani `revision`. Dla promptu przechowuje się maksymalnie 50 najnowszych wersji; usunięcie starszych jest jawną regułą retencji wykonywaną w tej samej transakcji co zapis nowej wersji.

### `prompt_usage`

Wpis zawiera `id`, `prompt_id`, `variant_id`, migawkę tytułu promptu i nazwy wariantu oraz `used_at`. Jest nieedytowalnym zdarzeniem i nie ma `updated_at` ani `revision`. Biblioteka zachowuje maksymalnie 500 najnowszych wpisów. Klucze do promptu i wariantu mogą zostać ustawione na `NULL` po ich usunięciu, natomiast migawki nazw pozostają czytelne. Dodanie wpisu i retencja nie zwiększają rewizji promptu.

## Indeksy wyszukiwania

Wyszukiwanie używa dwóch tabel wirtualnych SQLite FTS5 będących danymi pochodnymi:

| Indeks | Kolumny w kolejności wag BM25 |
|---|---|
| `tool_search` | `entity_id UNINDEXED`, `name`, `tags`, `category_path`, `url`, `description` |
| `prompt_search` | `entity_id UNINDEXED`, `title`, `tags`, `category_path`, `variant_names`, `variant_targets`, `description`, `variant_content` |

Każdy element domenowy ma dokładnie jeden dokument w odpowiednim indeksie. Wartości tekstowe są normalizowane zgodnie z [ADR 0003](adr/0003-wyszukiwanie-fts5.md), a konfiguracja FTS używa `tokenize='unicode61 remove_diacritics 0'` oraz indeksów prefiksowych długości 2, 3 i 4. `entity_id` przechowuje domenowy ULID, natomiast techniczny `rowid` FTS nie jest identyfikatorem domenowym i nie opuszcza infrastruktury.

Indeksy nie używają tabel domenowych jako external content. Zapis agregatu oraz odpowiadającego mu dokumentu FTS odbywa się w jednej transakcji. Zmiana nazwy kategorii lub tagu przebudowuje wszystkie zależne dokumenty w tej samej transakcji. Usunięcie elementu usuwa jego dokument, a pełny rebuild zastępuje zawartość indeksu wyłącznie na podstawie tabel domenowych.

Indeksy nie są częścią eksportu ani koperty importu. Import buduje je w bazie stagingowej przed walidacją końcową, a po zastosowaniu dokumentu kanoniczna baza zawiera indeks zgodny z zaimportowanymi danymi. Kontrola integralności i procedura odbudowy są wymagane przez `quality-requirements.md`.

## Kontrola współbieżności

- Każda komenda zmieniająca treść agregatu przekazuje oczekiwaną `revision` jego korzenia.
- Aktualizacja używa warunku `WHERE id = ... AND revision = ...` i zwiększa rewizję korzenia dokładnie o 1 w tej samej transakcji.
- Brak zmodyfikowanego wiersza oznacza konflikt, nie sukces ani automatyczne nadpisanie.
- Prompt jest zapisywany wraz z wariantami, zmiennymi, relacjami tagów, wersją i dokumentem FTS w jednej transakcji; każda taka zmiana zwiększa wyłącznie rewizję promptu.
- Narzędzie jest zapisywane wraz z relacjami tagów i dokumentem FTS w jednej transakcji; zmiana tych danych zwiększa rewizję narzędzia.
- Zmiana nazwy kategorii albo tagu zwiększa rewizję zmienianego korzenia, ale nie rewizje elementów, których dokument FTS został przebudowany jako skutek pochodny. Projekcja FTS zawsze pobiera aktualne nazwy współdzielone z bazy w transakcji zapisu, a nie z odłączonego modelu narzędzia lub promptu.
- Przełączenie `is_favorite` wymaga oczekiwanej rewizji promptu i zwiększa ją; nie jest operacją „last write wins”.
- Reorder wewnątrz jednej listy przekazuje oczekiwaną rewizję `ordering_scope`, zwiększa ją o 1 i nie zmienia rewizji ani `updated_at` treści przenumerowanych elementów.
- Przeniesienie między rodzicami, kategoriami lub grupami sprawdza rewizję zakresu źródłowego, docelowego i przenoszonego korzenia, a następnie zwiększa wszystkie trzy tokeny w jednej transakcji. Gdy zakres źródłowy i docelowy są identyczne, jego rewizja zwiększa się tylko raz.
- Dodanie albo usunięcie elementu uporządkowanej listy zwiększa rewizję właściwego zakresu. Usunięcie z poddrzewem aktualizuje lub usuwa wszystkie dotknięte zakresy w tej samej transakcji.
- Utworzenie elementu przekazuje oczekiwaną rewizję zakresu docelowego i dopisuje element na końcu. Usunięcie przekazuje oczekiwaną rewizję korzenia oraz dotkniętych zakresów.
- Zwykły zapis treści korzenia nie aktualizuje `sort_order`, `parent_id`, `category_id` ani `group_key`; pola członkostwa zmienia wyłącznie dedykowana komenda przeniesienia. Wyjątkiem są pozycje wariantów i zmiennych należące do agregatu promptu.
- Każdy zakres utrzymuje unikalne, gęste `sort_order`; zapis stosuje strategię aktualizacji, która nie narusza indeksu unikalnego również przejściowo.
- Interfejs pokazuje konflikt oraz pozwala odświeżyć dane; automatyczne scalanie nie należy do v2.

Pełny protokół komendy reorderu i mapę agregatów definiuje [ADR 0004](adr/0004-agregaty-rewizje-i-kolejnosc.md).

## Kopie zapasowe

- Kopia powstaje przez mechanizm bezpieczny dla aktualnego trybu SQLite, nie przez kopiowanie otwartego pliku zwykłą operacją systemową.
- Obowiązkowa kopia powstaje przed migracją, importem, resetem kolekcji i operacją masowo archiwizującą lub usuwającą dane.
- Kopia zawiera bazę oraz metadane: wersję schematu, wersję aplikacji, czas UTC i powód utworzenia.
- Aplikacja weryfikuje możliwość otwarcia kopii przed wykonaniem operacji chronionej.
- Polityka automatycznej retencji pozostaje decyzją otwartą; ręczna kopia i kopia ochronna nie mogą zostać usunięte w ramach tej samej operacji, którą chronią.

## Eksport i import

Eksport jest przenośnym dokumentem JSON, a nie kopią pliku SQLite. Normatywną wersję 1 formatu eksportu, wprowadzaną w produkcie v2, definiuje [JSON Schema eksportu](schemas/servanda-export-v1.schema.json). Koperta ma postać logiczną:

```json
{
  "schemaVersion": 1,
  "exportId": "01K25Q4Y7JZV6Q6T1M1CZ8M3QH",
  "exportedAt": "2026-08-08T12:00:00Z",
  "applicationVersion": "1.0.0",
  "areas": [],
  "categories": [],
  "tags": [],
  "tools": [],
  "prompts": [],
  "promptUsage": []
}
```

`exportId` identyfikuje operację eksportu, nie kolekcję ani bazę. `applicationVersion` jest informacją diagnostyczną. O zgodności importu rozstrzyga wyłącznie `schemaVersion` i lista jawnie wspieranych adapterów.

Obsługiwane wersje koperty:

| `schemaVersion` | Obsługa |
|---:|---|
| `1` | format bieżący, walidowany bezpośrednio przez `servanda-export-v1.schema.json` |

Dodanie kolejnej wersji wymaga nowego schematu oraz jawnej decyzji, które starsze wersje otrzymują testowany adapter. Brak wersji w tej tabeli oznacza odrzucenie dokumentu; aplikacja nie próbuje zgadywać zgodności na podstawie `applicationVersion`.

Sekcje mają następującą reprezentację:

- `areas`, `categories` i `tags` zawierają pełne rekordy domenowe wraz ze stabilnym `id`, relacjami, kolejnością i polami audytowymi;
- `tools` zawiera narzędzia oraz tablicę `tagIds`; fizyczna tabela `tool_tags` nie jest osobną sekcją eksportu;
- `prompts` zawiera prompty oraz zagnieżdżone `tagIds`, `variants`, `variables` i `versions`; fizyczne tabele dzieci i `prompt_tags` nie są osobnymi sekcjami;
- `versions` przechowuje przenośny obiekt migawki zamiast tekstowej reprezentacji `snapshot_json`; każda migawka zachowuje własne `schemaVersion`;
- `promptUsage` pozostaje osobną, uporządkowaną sekcją i zachowuje migawki tytułu promptu oraz nazwy wariantu także przy pustych identyfikatorach relacji.

Eksport obejmuje wszystkie aktywne, ukryte i zarchiwizowane dane v2, wszystkie zachowane wersje promptów oraz całą historię użycia objętą retencją. Nie obejmuje `app_state`, `ordering_scopes`, rewizji technicznych, indeksów FTS, migracji EF Core, logów, cache, kopii, wcześniejszych eksportów ani otwartych sesji edycji.

Reguły importu:

- v2 obsługuje wyłącznie pełne zastąpienie danych domenowych zgodnie z [ADR 0002](adr/0002-import-zastepujacy-kolekcje.md); merge, upsert i wybór części sekcji są odrzucane;
- aplikacja najpierw parsuje dokument, sprawdza JSON Schema, limity, relacje i reguły domenowe w izolowanej bazie stagingowej, bez zmiany kanonicznej bazy;
- dokument musi zawierać dokładnie po jednym aktywnym obszarze dla `tools` i `prompts`; importer nie dodaje po cichu brakujących seedów;
- użytkownik widzi wersję schematu oraz liczbę elementów dodawanych, zastępowanych i usuwanych w każdej sekcji;
- zastosowanie importu wymaga jawnego potwierdzenia, tworzy zweryfikowaną kopię i zastępuje dane w jednej transakcji SQLite;
- rekord domenowy nieobecny w poprawnym, zatwierdzonym dokumencie zostaje usunięty; tabele schematu i `app_state` nie są importowane, a `ordering_scopes` oraz indeksy FTS są odbudowywane z nowych danych;
- ULID oraz `created_at` i `updated_at` są zachowywane, importowane korzenie agregatów otrzymują `revision = 1`, a każdy odbudowany zakres kolejności rozpoczyna od `revision = 1`;
- w tej samej transakcji aplikacja generuje nowy `content_epoch`, przez co wszystkie wcześniej otwarte sesje edycji stają się niezgodne z kolekcją;
- znana starsza wersja jest przekształcana przez jawnie testowany adapter, a wersja przyszła lub nieznana jest odrzucana bez częściowego importu;
- import nie może po cichu ucinać elementów przekraczających limity ani pomijać nieznanych pól.

## Prywatność i szyfrowanie

Baza, kopie i eksporty mogą zawierać dane rodzinne, finansowe i dotyczące witalności. V2 chroni je lokalnością, uprawnieniami systemu plików i niewystawianiem usługi do sieci. SQLite nie jest domyślnie szyfrowane. Szyfrowanie aplikacyjne wymaga osobnej decyzji o zarządzaniu kluczem; do tego czasu rekomendowane jest szyfrowanie dysku lub katalogu na poziomie systemu operacyjnego.

## Rozszerzanie modelu

Moduły notatek, domu, rodziny, witalności i budżetu nie otrzymują tabel przed zaakceptowaniem własnych specyfikacji. Dodanie modułu MUSI objąć:

1. specyfikację funkcji i kryteria akceptacji,
2. tabele, ograniczenia oraz relacje w tym dokumencie,
3. migrację i testy migracji,
4. eksport, import i kopie,
5. zasady archiwizacji i usuwania,
6. ocenę prywatności danych modułu.
