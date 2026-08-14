# Katalog narzędzi

> Status: obowiązujący dla v2

## Cel

Katalog umożliwia szybkie znalezienie sprawdzonego narzędzia i przejście do jego strony.

## Struktura

- Narzędzia należą do jednej kategorii, a kategorie mogą być zagnieżdżone.
- Każde narzędzie należy do grupy `featured` albo `regular`.
- Interfejs prezentuje te grupy jako „Lubię i szanuję” oraz „Fajne”.
- Licznik kategorii obejmuje narzędzia kategorii i wszystkich jej potomków.

## Nawigacja i filtrowanie

- Startowy widok pokazuje wszystkie narzędzia.
- Wybranie kategorii pokazuje narzędzia należące do niej lub jej potomków.
- Kategorie są wybierane w panelu bocznym. Główna część widoku pozostaje przeznaczona dla kart narzędzi, wyszukiwania i informacji o aktywnym filtrze.
- Identyfikator wybranej kategorii może być zapisany w parametrze zapytania lokalnego URL, aby odświeżenie zachowało widok.
- Nieznany albo należący do innego obszaru identyfikator wraca do widoku wszystkich narzędzi i nie ujawnia danych innego modułu.
- Drzewo kategorii można zwijać niezależnie od aktywnego filtra.

## Siatka i dodawanie

- Narzędzia są prezentowane jako kafle w siatce po trzy na wiersz we wspieranym widoku komputerowym. Reflow i powiększenie mogą zmniejszyć liczbę kolumn, aby nie powodować poziomego przewijania ani ucinania treści.
- Stała akcja „Dodaj narzędzie” ma postać pływającego przycisku w prawym dolnym rogu obszaru treści. Przycisk nie może zasłaniać kart, komunikatów ani fokusowanych kontrolek.
- „Dodaj narzędzie” otwiera modalny edytor zawierający wybór kategorii oraz wszystkie pola wymagane do utworzenia narzędzia.
- Jeżeli użytkownik otwiera edytor z widoku wybranej kategorii, ta kategoria jest wstępnie wybrana. Z widoku wszystkich narzędzi użytkownik wybiera kategorię w dialogu.
- Wstępny wybór jest edytowalny przed zapisem i nie zmienia aktywnego filtra listy.

## Wyszukiwanie

Wspólną semantykę zapytania, zakres indeksowanych pól, polskich znaków, prefiksów, rankingu, stronicowania i skrótów definiuje [search.md](search.md).

Zapytanie i filtr kategorii działają łącznie. Wynik dopasowany wyłącznie po tagu niewidocznym na karcie komunikuje przyczynę dopasowania zgodnie ze wspólnym kontraktem.

## Karta narzędzia

Karta pokazuje inicjały, domenę, nazwę, opis, maksymalnie trzy tagi i odnośnik. Odnośnik:

- dopuszcza wyłącznie protokół HTTP lub HTTPS,
- otwiera nową kartę,
- używa `noopener noreferrer`.

Przycisk edycji otwiera edytor wskazanego narzędzia.

## Stan pusty

Gdy filtr i wyszukiwanie nie dają wyników, aplikacja pokazuje komunikat i przycisk czyszczący oba kryteria.

## Kryteria akceptacji

- przy standardowej szerokości wspieranego widoku główna lista prezentuje trzy karty w wierszu,
- pływający przycisk otwiera modalny formularz, a zamknięcie dialogu zwraca fokus do przycisku,
- aktywna kategoria jest wstępnie wybrana przy tworzeniu narzędzia z jej widoku,
- licznik widocznych narzędzi odpowiada kartom po zastosowaniu obu kryteriów,
- puste grupy, sekcje i kategorie główne nie zajmują miejsca w wynikach,
- filtr kategorii uwzględnia jej potomków,
- wyszukiwanie obejmuje wszystkie pola narzędzia wymienione we wspólnym kontrakcie,
- warianty pisowni z polskimi znakami i bez nich zwracają ten sam zestaw narzędzi,
- niepoprawny lub niedozwolony URL nie staje się aktywnym linkiem,
- przy najmniejszej wspieranej szerokości komputerowej menu kategorii pozostaje dostępne i obsługiwane klawiaturą.
- zapis narzędzia i jego tagów jest jedną transakcją,
- próba zapisu nieaktualnej wersji pokazuje konflikt i nie nadpisuje nowszych danych.
