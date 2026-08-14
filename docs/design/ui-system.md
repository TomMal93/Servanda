# System interfejsu Servandy

> Status: obowiązujący od v1  
> Implementacja: tokeny globalne i style komponentów Blazor tworzone w nowym projekcie

## Charakter

Servanda używa ciemnego, zwartego interfejsu narzędziowego. Ma przypominać osobisty pulpit pracy, nie publiczny katalog marketingowy. Najważniejsze są czytelna hierarchia, szybkie skanowanie kafli i kart oraz wyraźne stany interakcji.

## Kolory

Obowiązujące tokeny bazowe:

| Token | Wartość | Rola |
|---|---:|---|
| `--bg` | `#0b0d11` | tło aplikacji |
| `--bg-raised` | `#0f1218` | powierzchnie podniesione |
| `--panel` | `#101319` | panele i dialogi |
| `--panel-soft` | `#141820` | pola i karty pomocnicze |
| `--border` | `#252a34` | główne obramowania |
| `--border-soft` | `#1c2028` | subtelne podziały |
| `--text` | `#f3f5f7` | tekst główny |
| `--muted` | `#8b929e` | tekst drugorzędny |
| `--muted-light` | `#b9bec7` | tekst pomocniczy o wyższym kontraście |
| `--acid` | `#b7f34b` | główna akcja, fokus i stan aktywny |
| `--acid-on` | `#101400` | tekst i ikona na tle `--acid` |
| `--border-interactive` | `#667085` | granica pól i kontrolek; co najmniej 3:1 względem `--panel-soft` |
| `--danger` | `#ff7b8a` | błąd i akcja destrukcyjna |
| `--danger-soft` | `#35171d` | tło komunikatu błędu |
| `--warning` | `#f6c453` | ostrzeżenie i konflikt wymagający decyzji |
| `--warning-soft` | `#332713` | tło ostrzeżenia |
| `--success` | `#73d69f` | zapis zakończony i stan poprawny |
| `--success-soft` | `#123021` | tło komunikatu sukcesu |
| `--info` | `#7cc4ff` | informacja, postęp i ponowne łączenie |
| `--info-soft` | `#12283b` | tło komunikatu informacyjnego |

Sześć dekoracyjnych akcentów kategorii ma wartości: `--accent-0: #b7f34b`, `--accent-1: #7cc4ff`, `--accent-2: #b5a1ff`, `--accent-3: #ff9f68`, `--accent-4: #73d69f` i `--accent-5: #ff7baf`. Akcent nie służy samodzielnie jako kolor tekstu i nie przenosi znaczenia bez etykiety.

Tekst zwykły spełnia kontrast co najmniej 4,5:1, duży tekst co najmniej 3:1, a granice i stany kontrolek co najmniej 3:1 względem sąsiadującego tła. Kolor nie może być jedyną informacją o stanie: aktywny element potrzebuje także tekstu, ikony, obramowania lub właściwego atrybutu ARIA. `--border` i `--border-soft` służą wyłącznie do podziałów dekoracyjnych; pola i inne kontrolki używają `--border-interactive`.

## Typografia i ikony

- `--font-sans` ma wartość `ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`. Pierwsze wydanie używa fontów systemowych i nie pakuje ani nie pobiera zewnętrznego kroju.
- `--font-mono` ma wartość `ui-monospace, "Cascadia Mono", "Segoe UI Mono", monospace` i służy wyłącznie treści wymagającej stałej szerokości znaków.
- Skala rozmiaru i interlinii wynosi: `--font-size-xs: 0.75rem` / `--line-height-xs: 1rem`, `--font-size-sm: 0.875rem` / `--line-height-sm: 1.25rem`, `--font-size-md: 1rem` / `--line-height-md: 1.5rem`, `--font-size-lg: 1.125rem` / `--line-height-lg: 1.625rem`, `--font-size-xl: 1.5rem` / `--line-height-xl: 2rem` oraz `--font-size-2xl: 2rem` / `--line-height-2xl: 2.5rem`.
- Dostępne grubości to `--weight-regular: 400`, `--weight-semibold: 600` i `--weight-bold: 700`. Tekst podstawowy używa pary `md`, a metadane nie schodzą poniżej pary `xs`.
- Ikony są lokalnymi SVG z ustalonym `viewBox`, domyślnie mają `1.25rem`, dziedziczą `currentColor` i nie są dostarczane jako font ikonowy. Ikona dekoracyjna ma `aria-hidden="true"`; samodzielna kontrolka ikonowa otrzymuje dostępną nazwę.

## Skale i warstwy

Skala odstępów jest wielokrotnością `0.25rem`:

| Token | Wartość | Typowe użycie |
|---|---:|---|
| `--space-0` | `0` | brak odstępu |
| `--space-1` | `0.25rem` | ikona z etykietą pomocniczą |
| `--space-2` | `0.5rem` | zwarte grupy kontrolek |
| `--space-3` | `0.75rem` | wnętrze małej kontrolki |
| `--space-4` | `1rem` | standardowy odstęp komponentu |
| `--space-5` | `1.5rem` | wnętrze karty lub sekcji |
| `--space-6` | `2rem` | odstęp między sekcjami |
| `--space-8` | `3rem` | główny rytm strony |

- Promienie: `--radius-sm: 0.25rem`, `--radius-md: 0.5rem`, `--radius-lg: 0.75rem`, `--radius-pill: 999px`.
- Linie: `--border-width: 1px`, `--focus-width: 2px`, `--focus-offset: 2px`.
- Kontrolki: `--control-sm: 2rem`, `--control-md: 2.5rem`, `--control-lg: 3rem`. Standardowy formularz używa `--control-md`; kontrolka ikonowa nie ma celu mniejszego niż `2rem × 2rem`.
- Cienie: `--shadow-raised: 0 0.25rem 1rem rgb(0 0 0 / 0.28)` i `--shadow-dialog: 0 1rem 3rem rgb(0 0 0 / 0.48)`. Obramowanie nadal określa granicę powierzchni; cień nie jest jedynym rozróżnieniem.
- Warstwy: `--z-base: 0`, `--z-sticky: 100`, `--z-popover: 200`, `--z-backdrop: 900`, `--z-dialog: 1000`, `--z-toast: 1100`. Komponent nie wprowadza wartości spoza tej skali bez aktualizacji kontraktu.
- Ruch: `--motion-fast: 120ms`, `--motion-normal: 180ms`, `--motion-slow: 240ms`, z krzywą `--ease-standard: cubic-bezier(0.2, 0, 0, 1)`. Animowana jest wyłącznie przezroczystość lub transformacja; `prefers-reduced-motion: reduce` usuwa ruch niekonieczny.

## Układ

- System interfejsu jest projektowany wyłącznie dla komputerów osobistych i laptopów, przy szerokości widoku od `1024px`. Nie definiuje układu na telefony ani tablety.
- Na szerokim ekranie boczna nawigacja ma szerokość `--sidebar-width: 18.25rem`, a treść zajmuje resztę widoku.
- Na ekranie głównym treść tworzy responsywna siatka kafli obszarów wyśrodkowana w dostępnej części widoku; szerokość kafli i odstępy mają zachować czytelny rytm bez rozciągania siatki na całą szerokość dużego monitora.
- Po wejściu do aktywnego obszaru jego elementy są prezentowane jako czytelne kafle w siatce trzech kolumn. Liczba trzech kolumn obowiązuje dla standardowych wspieranych szerokości; przy reflow, powiększeniu lub niewystarczającym miejscu siatka przechodzi do mniejszej liczby kolumn.
- Poniżej `1200px` upraszczany jest układ Prompt Studio i siatka promptów, ale panel boczny pozostaje stałym elementem nawigacji.
- Breakpoint wynika z miejsca potrzebnego treści, nie z etykiety konkretnego urządzenia.

Przy powiększeniu tekstu lub strony układ reflow nie jest traktowany jako wersja mobilna produktu. Na komputerze przy efektywnej szerokości poniżej `64rem`:

- kolumny Prompt Studio i formularzy układają się pionowo,
- panel boczny staje się nazwanym przyciskiem otwierającym modalną szufladę z pułapką fokusu i obsługą `Escape`,
- główne akcje pozostają w kolejności dokumentu i nie są przyklejane nad fokusowanymi kontrolkami,
- strona nie przewija się poziomo przy efektywnej szerokości `20rem`; wyjątkiem może być jawnie nazwany region dwuwymiarowej tabeli lub bloku kodu.

Weryfikacja reflow obejmuje co najmniej 200% przy widoku 1024 px oraz 400% przy widoku 1280 px. Nie oznacza to wsparcia telefonów ani tabletów.

## Nawigacja

Panel boczny zawiera:

1. markę i wejście do ekranu głównego,
2. w v1 statyczną listę planowanych obszarów,
3. po wejściu do aktywnego obszaru wybór jego kategorii i filtrów,
4. od v2 na dole stałą akcję „Ustawienia” z ikoną ustawień,
5. stałą akcję zamknięcia aplikacji.

Od v2 panel dodaje aktywne obszary, kontekstowe filtry, drzewo kategorii i podsumowanie zasobów. Kategorie są dostępne bezpośrednio w panelu podczas przeglądania modułu, aby ich wybór nie zajmował głównej części widoku.

Przycisk „Ustawienia” przełącza panel i treść w prosty tryb ustawień. Tryb zawiera co najmniej wejścia „Zarządzaj obszarami” oraz „Dane kolekcji”, obejmujące istniejące operacje importu i eksportu. Powrót z ustawień przywraca zwykłą nawigację obszarów i kategorii. Recovery nadal działa na osobnym ekranie. Ten tryb jest miejscem nawigacji do zatwierdzonych operacji, a nie zgodą na zunifikowane centrum diagnostyki, retencji kopii ani nowe ustawienia produktu.

Obszar planowany ma widoczną etykietę „Planowane” i nie otrzymuje kontrolek aktywnego modułu. Od v2 aktywny obszar i aktywny filtr muszą być odróżnialne. Szczegółowy kontrakt definiuje `../product/features/areas-dashboard.md`.

Aktywny element jest widoczny wizualnie i programowo przez `aria-pressed` albo `aria-selected` zgodnie z rolą kontrolki.

## Karty

- Karta jest powierzchnią informacyjną; główna akcja pozostaje jawnym linkiem lub przyciskiem.
- Nazwa i opis mają pierwszeństwo przed metadanymi.
- Tagi są informacją pomocniczą, a nie jedyną nazwą kategorii.
- Karty `featured` mogą mieć mocniejszy akcent, ale zachowują ten sam kontrakt treści.
- Przycisk edycji i ulubionych musi mieć dostępną nazwę niezależną od samego symbolu.
- Karta nie ma stałej wysokości zależnej od przewidywanej długości nazwy. Nazwa, opis, tagi i akcje zawijają się bez zasłaniania sąsiedniej treści.

## Pływająca akcja dodawania

- Widok aktywnego modułu udostępnia jedną główną pływającą akcję „Dodaj” w prawym dolnym rogu obszaru treści. Dostępna nazwa precyzuje typ elementu, na przykład „Dodaj narzędzie” albo „Dodaj prompt”.
- Przycisk pozostaje widoczny podczas przeglądania kart, ale zachowuje odstęp od prawej i dolnej krawędzi oraz uwzględnia panel boczny. Lista otrzymuje wystarczające dolne wypełnienie, aby ostatnia karta i jej akcje nie znalazły się pod przyciskiem.
- Przycisk ma tekst lub dostępną nazwę niezależną od ikony, pełny stan `focus-visible` i cel nie mniejszy niż `--control-lg`.
- Aktywacja otwiera modalny edytor tworzenia. Zamknięcie lub anulowanie zwraca fokus do przycisku, a zapis przenosi fokus do czytelnego potwierdzenia albo nowo utworzonej karty.

## Stany komponentów

| Stan | Kontrakt wizualny i programowy |
|---|---|
| `hover` | subtelna zmiana powierzchni lub granicy; nie zastępuje fokusu |
| `focus-visible` | obrys `var(--focus-width) solid var(--acid)` z odsunięciem `--focus-offset`; nie jest przycinany ani zasłaniany |
| aktywny/wybrany | akcent, mocniejsza granica oraz `aria-current`, `aria-pressed` albo `aria-selected` zgodne z rolą |
| `disabled` | natywny stan `disabled` lub `aria-disabled`; obniżony kontrast jest tylko dodatkiem, a przyczyna niedostępności jest dostępna w kontekście |
| zajęty/loading | tekst opisujący operację, `aria-busy="true"` na aktualizowanym regionie i zablokowanie wyłącznie konfliktujących akcji; kontrolka zachowuje szerokość |
| pusty | nazwa pustego zbioru, krótkie wyjaśnienie i dostępna akcja utworzenia albo zmiany filtra |
| błąd | `--danger`/`--danger-soft`, ikona lub etykieta, komunikat przy polu albo operacji i możliwość ponowienia, jeśli jest bezpieczna |
| konflikt/ostrzeżenie | `--warning`/`--warning-soft`, jawny tekst i akcje rozwiązania bez automatycznego nadpisania |
| sukces | `--success`/`--success-soft`, krótki komunikat `aria-live`; komunikat inline pozostaje do następnej akcji, a toast jest możliwy do zamknięcia, zatrzymuje czas po fokusie lub najechaniu i jest widoczny co najmniej 5 sekund |
| informacja/łączenie | `--info`/`--info-soft`, tekst stanu i postęp bez polegania wyłącznie na animacji |

Skeleton może jedynie zastępować znany układ podczas pierwszego odczytu, jest ukryty przed technologią asystującą i nie zastępuje tekstowego statusu. Spinner ma etykietę albo należy do nazwanego regionu zajętego.

## Dialogi i formularze

- Dostępny komponent oparty na natywnym `<dialog>` tworzy modalną warstwę prostych edytorów i Prompt Studio; złożone zarządzanie może używać osobnego widoku, jeżeli dialog byłby zbyt ciasny.
- Modal tworzenia elementu zawiera wybór kategorii oraz pełny formularz właściwy danemu typowi. Gdy dialog otwarto z aktywnego widoku kategorii, kategoria jest wstępnie wybrana, lecz pozostaje możliwa do zmiany.
- Każdy dialog ma nagłówek, jawną kontrolkę zamknięcia i etykietę przez `aria-labelledby`.
- Formularz łączy etykietę z kontrolką i tekstowo pokazuje pola wymagane oraz błędy.
- Akcja główna używa koloru `--acid`; akcja destrukcyjna musi być nazwana wprost i chroniona potwierdzeniem.
- Tekst i ikona głównej akcji na tle `--acid` zawsze używają `--acid-on`.
- Status zapisu i kopiowania trafia do obszaru `aria-live`.
- Pole ma widoczną granicę `--border-interactive` w stanie domyślnym. Błąd, wymaganie i format są opisane tekstem, nie samą zmianą koloru granicy.
- Stopka dialogu nie zasłania fokusowanej kontrolki. Treść dialogu ma ograniczenie do widoku i własne przewijanie, a nagłówek, treść i akcje zachowują logiczną kolejność fokusu.

## Fokus, klawiatura i ruch

- Przyciski, linki i pola używają stanu `focus-visible` zdefiniowanego w tabeli stanów. Sticky panel, toast, popover ani stopka dialogu nie mogą całkowicie zasłonić fokusowanego elementu.
- Każdy cel wskaźnika ma co najmniej `24 × 24` piksele CSS albo spełnia wyjątek odstępu WCAG 2.2; kontrolki projektowane przez Servandę mają domyślnie co najmniej `--control-sm`, czyli `32 × 32` piksele CSS.
- Zachowanie `Ctrl+K` i `Escape` w bibliotekach definiuje `../product/features/search.md`; skróty nie przechwytują pisania w edytorze ani innym polu tekstowym i mają widoczną alternatywę.
- Interfejs respektuje `prefers-reduced-motion: reduce`.
- Nowe animacje nie mogą być konieczne do zrozumienia zmiany stanu.

## Treść

- Interfejs jest obecnie polskojęzyczny.
- Nazwy akcji zaczynają się od czasownika: „Edytuj”, „Kopiuj”, „Wyczyść”, „Przywróć”.
- Komunikat błędu mówi, co się nie udało; nie ujawnia wewnętrznych danych serwera.
- Tekst nie przedstawia niezapisanego stanu formularza jako zapisu w kanonicznej bazie SQLite.
- Komunikat sukcesu pojawia się dopiero po zatwierdzeniu zapisu w lokalnej bazie; wcześniej interfejs mówi wprost o niezapisanych zmianach.
- Nazwy, tagi, adresy URL i inne ciągi użytkownika używają zawijania odpornego na długi nieprzerwany tekst (`overflow-wrap: anywhere`). Treść promptu zachowuje zamierzone nowe linie przez `white-space: pre-wrap` i nadal może się zawijać.
- Krytyczna nazwa, komunikat błędu ani główna akcja nie używają wielokropka lub `line-clamp`. Skrócenie treści pomocniczej jest dozwolone tylko z dostępną w tej samej klawiaturowej ścieżce akcją „Pokaż całość”. Sam atrybut `title` nie jest wystarczającym dostępem do pełnej treści.
- Tagi i grupy akcji przechodzą do kolejnego wiersza. Komponent rośnie wraz z tekstem; nie używa stałej wysokości, która ucina treść.
- Tabela o rzeczywiście dwuwymiarowej treści może przewijać się we własnym, nazwanym i fokusowalnym regionie. Dane formularzy oraz listy kart mają się przeorganizować bez poziomego przewijania.
- Nadpisanie odstępów tekstu do wartości WCAG 1.4.12 nie może ucinać, nakładać ani ukrywać treści lub funkcji.

Wymagania reflow, odstępów tekstu, niezasłoniętego fokusu i rozmiaru celu wynikają odpowiednio z WCAG 2.2 [1.4.10](https://www.w3.org/WAI/WCAG22/Understanding/reflow), [1.4.12](https://www.w3.org/WAI/WCAG22/Understanding/text-spacing), [2.4.11](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum) i [2.5.8](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum).

## Zasoby interfejsu

- Fonty, ikony, skrypty, style i obrazy powłoki pochodzą wyłącznie z paczki aplikacji; interfejs nie odwołuje się do CDN ani zewnętrznych fontów.
- Strona bootstrapu bez sesji pokazuje jedynie komunikat o otwarciu aplikacji przez launcher i nie renderuje fragmentu prywatnej powłoki.
- CSP jest aktywna również dla bootstrapu, recovery i ekranów błędów. Komponent nie może osłabić jej własnym nagłówkiem ani znacznikiem `meta`.
- Dokument ustawia `color-scheme: dark`, aby natywne kontrolki, autofill i paski przewijania odpowiadały jedynemu wspieranemu motywowi.

## Stany procesu lokalnego

- Utrata połączenia z lokalnym procesem jest pokazana jako stan aplikacji z próbą ponownego połączenia.
- W v1 interfejs nie sugeruje istnienia zapisanych danych. Od v2 nie przedstawia utraty circuitu jako utraty danych wcześniej zapisanych w SQLite.
- Akcja „Zamknij Servandę” wymaga potwierdzenia i zawsze ostrzega, że rozłączy wszystkie karty. Bieżąca karta wskazuje własny niezapisany formularz, jeżeli go zna; aplikacja nie obiecuje kompletnego wykrycia niezapisanych formularzy w innych kartach.
- Od v2 karta z niezapisanym formularzem używa lokalnego `beforeunload`. Utrata circuitu może utracić dane niezapisane, ale nie narusza danych wcześniej zapisanych w SQLite.
- Od v2 błąd startu bazy lub migracji przełącza host w stan `recovery` i prowadzi do osobnego ekranu odzyskiwania; nie uruchamia normalnej powłoki ani pustego pulpitu.
- Ekran odzyskiwania v2 pokazuje bezpieczną kategorię błędu, stan kopii ochronnej i dostępne akcje. Nie renderuje nawigacji obszarów i nie pozwala wykonywać zwykłych operacji zapisu.
- Ekran recovery v2 pozwala po jawnym potwierdzeniu odtworzyć zgodną, zweryfikowaną kopię oraz ponowić start. Jeżeli bezpieczne odtworzenie nie jest możliwe, pokazuje drogę do diagnostyki bez ujawniania ścieżek i szczegółów wyjątku w przeglądarce.

## Kryteria akceptacji systemu

1. Powłoka, karty i komunikaty v1 oraz formularze, dialogi danych i recovery v2 korzystają ze wspólnych tokenów; test stylów nie znajduje równoległej lokalnej palety ani powtarzających się wartości zastępujących tokeny.
2. Automatyczna kontrola kontrastu potwierdza wymagane pary tekstu, `--acid`/`--acid-on`, statusów i `--border-interactive` na powierzchniach kontrolek.
3. Każdy interaktywny komponent ma stany właściwe swojej roli: domyślny, `hover`, `focus-visible`, aktywny lub wybrany, disabled oraz busy; formularze dodatkowo obsługują błąd i sukces.
4. Krytyczne przepływy przechodzą klawiaturą bez pułapki poza modalnym dialogiem lub szufladą, z widocznym i niezasłoniętym fokusem oraz przewidywalnym jego powrotem.
5. Cele interakcji, reflow 200%/400%, nadpisane odstępy tekstu i `prefers-reduced-motion` spełniają wymagania `../engineering/quality-requirements.md` w Chromium i Firefox na Linuksie.
6. Dane o maksymalnych długościach, długi ciąg bez spacji, maksymalny zestaw tagów i wielowierszowy prompt nie zasłaniają akcji, nie nakładają treści i nie wymagają poziomego przewijania całej strony.
7. Artefakt działa z `color-scheme: dark`, lokalnymi SVG i fontami systemowymi oraz bez pobierania fontów, ikon, CSS lub skryptów z sieci.
