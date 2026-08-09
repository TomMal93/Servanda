# Wyszukiwanie w bibliotekach

> Status: obowiązujący dla v2

## Cel

Wspólne wyszukiwanie pozwala znaleźć narzędzie albo prompt po kilku zapamiętanych słowach bez znajomości dokładnej pisowni polskich znaków. Ten dokument jest źródłem prawdy dla zakresu indeksowanych pól, zachowania wyszukiwarki, normalizacji zapytania, kolejności wyników i stronicowania. Specyfikacje modułów odwołują się do tego kontraktu zamiast definiować konkurencyjne reguły.

## Semantyka zapytania

- Zapytanie jest zwykłym tekstem. Pierwsze wydanie nie udostępnia użytkownikowi składni operatorów FTS, wyrażeń logicznych, filtrów kolumn ani cudzysłowów specjalnych.
- Wielkość liter oraz polskie znaki diakrytyczne nie zmieniają dopasowania. Przykładowo `lodz`, `Łódź` i `łódź` są równoważne dla wyszukiwania.
- Znaki interpunkcyjne i kolejne białe znaki rozdzielają tokeny. Wyszukiwanie adresu `docs.example.com` może odbywać się po tokenach `docs`, `example` albo `com`; dowolny fragment ze środka tokenu nie jest gwarantowanym dopasowaniem.
- Każdy token jest traktowany jako prefiks. Zapytanie `kon prom` dopasowuje dokument zawierający token zaczynający się od `kon` oraz token zaczynający się od `prom`.
- Wszystkie tokeny muszą wystąpić w jednym dokumencie wyszukiwania, choć mogą pochodzić z różnych indeksowanych pól tego samego elementu.
- Puste zapytanie pokazuje wyniki wynikające wyłącznie z aktywnego filtra. Zapytanie jest wykonywane po wprowadzeniu co najmniej dwóch znormalizowanych znaków; pojedynczy znak nie uruchamia wyszukiwania i otrzymuje krótką informację pomocniczą.
- Znaki mające specjalne znaczenie dla FTS są traktowane jak zwykłe dane albo separatory. Niepoprawny tekst użytkownika nie może powodować błędu składni SQL ani FTS.

Normalizacja dotyczy wyłącznie indeksu i zapytania. Oryginalna treść pozostaje przechowywana i wyświetlana bez zmian.

## Zakres pól

| Moduł | Pola indeksowane |
|---|---|
| narzędzia | nazwa, opis, URL, pełna ścieżka kategorii i nazwy tagów |
| prompty | tytuł, opis, pełna ścieżka kategorii, nazwy tagów oraz nazwa, przeznaczenie i pełna treść każdego bieżącego wariantu |

Zachowane wersje promptów i historia użycia nie wchodzą do bieżącego wyszukiwania biblioteki. Zmiana nazwy kategorii albo tagu aktualizuje dokumenty wyszukiwania wszystkich elementów, które z nich korzystają, w tej samej transakcji co zmiana danych domenowych.

Wyszukiwanie i aktywny filtr modułu działają łącznie. Dopasowanie po polu niewidocznym na karcie, na przykład piątym tagu albo treści wariantu, nadal może zwrócić element; karta pokazuje wtedy tekstową informację „Dopasowanie w treści” albo „Dopasowanie w tagach” bez ujawniania całej prywatnej treści w wynikach.

## Kolejność wyników

Wyniki są uporządkowane deterministycznie:

1. dokładne dopasowanie znormalizowanej nazwy narzędzia albo tytułu promptu,
2. dopasowanie prefiksu nazwy albo tytułu,
3. ważony wynik trafności pełnotekstowej,
4. `updated_at` malejąco,
5. `id` rosnąco jako stabilny tie-breaker.

Wynik pełnotekstowy nadaje najwyższą wagę nazwie lub tytułowi, następnie tagom i ścieżce kategorii, dalej nazwie wariantu, przeznaczeniu i URL, następnie opisowi, a najniższą pełnej treści wariantów. Dokładne wagi należą do kontraktu technicznego i mają test porządku reprezentatywnych wyników.

## Reakcja interfejsu

- Pole stosuje debounce `250 ms`. Nowe zapytanie anuluje poprzednie oczekujące wyszukiwanie i jego wynik nie może zastąpić nowszej odpowiedzi.
- Pierwsza strona zawiera maksymalnie 50 elementów. Kolejne strony są pobierane jawną akcją „Pokaż więcej”; interfejs nie pobiera pełnej kolekcji ani pełnej treści wariantów do kart.
- Licznik pokazuje liczbę elementów po połączeniu wyszukiwania i aktywnego filtra, nie liczbę dopasowanych tokenów ani wariantów.
- Stan ładowania, liczba wyników i stan pusty są widoczne tekstowo. Zmiana liczby wyników jest ogłaszana przez właściwy, nieagresywny obszar `aria-live`.
- `Ctrl+K` ustawia fokus w polu wyszukiwania aktywnego modułu, o ile fokus nie znajduje się w edytorze lub innym polu tekstowym. Widoczna etykieta i możliwość przejścia klawiaturą pozostają alternatywą dla skrótu.
- `Escape` czyści zapytanie tylko wtedy, gdy fokus znajduje się w polu wyszukiwania; nie zmienia aktywnego filtra.

## Kryteria akceptacji

1. Zapytania z polskimi znakami, bez nich oraz z inną wielkością liter zwracają ten sam zestaw wyników.
2. Wielowyrazowe zapytanie wymaga dopasowania wszystkich tokenów, a wpisywany ostatni token działa jako prefiks.
3. Tekst przypominający składnię FTS nie powoduje błędu ani nie zmienia znaczenia zapytania na operator logiczny.
4. Filtr kategorii lub statusu zawęża wyniki przed wyliczeniem strony i licznika.
5. Kolejność pozostaje stabilna przy identycznym wyniku trafności.
6. Wynik pasujący wyłącznie do treści wariantu albo ukrytego tagu wyjaśnia przyczynę dopasowania.
7. Odpowiedź starszego, anulowanego zapytania nie zastępuje wyników nowszego tekstu.
8. Strona nie pobiera więcej niż 50 kart i nie pobiera pełnych treści wariantów do ich renderowania.

Indeks, normalizację techniczną i sposób pomiaru definiują `../../engineering/data-model.md`, `../../engineering/adr/0003-wyszukiwanie-fts5.md` oraz `../../engineering/quality-requirements.md`.
