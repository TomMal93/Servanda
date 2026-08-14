# Biblioteka promptów i Prompt Studio

> Status: obowiązujący dla v2

## Cel

Biblioteka przechowuje wielokrotnego użytku instrukcje, a Prompt Studio zamienia wybrany wariant i wartości zmiennych w gotową treść do skopiowania.

## Organizacja i wyszukiwanie

- Prompty należą do jednej hierarchicznej kategorii.
- Widok udostępnia filtry: wszystkie, ulubione, ostatnio używane i wybrana kategoria.
- Kategorie oraz pozostałe filtry są wybierane w panelu bocznym. Główna część widoku pozostaje przeznaczona dla kart promptów, wyszukiwania i informacji o aktywnym filtrze.
- Wspólną semantykę zapytania, zakres indeksowanych pól, polskich znaków, prefiksów, rankingu, stronicowania i skrótów definiuje [search.md](search.md).
- Zapytanie działa łącznie z aktywnym filtrem.
- Aktywny filtr może być zapisany w parametrze zapytania lokalnego URL, aby odświeżenie zachowało widok.

## Siatka i dodawanie

- Prompty są prezentowane jako kafle w siatce po trzy na wiersz we wspieranym widoku komputerowym. Reflow i powiększenie mogą zmniejszyć liczbę kolumn, aby nie powodować poziomego przewijania ani ucinania treści.
- Stała akcja „Dodaj prompt” ma postać pływającego przycisku w prawym dolnym rogu obszaru treści. Przycisk nie może zasłaniać kart, komunikatów ani fokusowanych kontrolek.
- „Dodaj prompt” otwiera modalny edytor zawierający wybór kategorii oraz wszystkie pola wymagane do utworzenia promptu, jego wariantów i zmiennych.
- Jeżeli użytkownik otwiera edytor z widoku wybranej kategorii, ta kategoria jest wstępnie wybrana. Z widoku wszystkich promptów albo filtra przekrojowego użytkownik wybiera kategorię w dialogu.
- Wstępny wybór jest edytowalny przed zapisem i nie zmienia aktywnego filtra listy.

## Karta promptu

Karta pokazuje tytuł, opis, maksymalnie cztery tagi, liczbę wariantów i datę ostatniego użycia. Pozwala:

- przełączyć ulubiony status,
- otworzyć Prompt Studio,
- otworzyć edytor promptu.

## Prompt Studio

- Użytkownik wybiera jeden wariant promptu.
- Zmienne zapisane w treści jako `{{nazwa}}` otrzymują osobne pola.
- Pole używa skonfigurowanej etykiety, wartości domyślnej, wymagalności i trybu jedno- lub wielowierszowego.
- Podgląd aktualizuje się przy zmianie wartości.
- Pusta wartość zastępuje znacznik pustym tekstem. Jeżeli zmienna jest wymagana, przycisk kopiowania pozostaje wyłączony i wskazuje brakujące pola; pusta zmienna opcjonalna nie blokuje kopiowania.
- Skopiowanie zapisuje wpis historii użycia z promptem, wariantem i czasem.
- Jeżeli API schowka jest niedostępne, aplikacja nie zgłasza fałszywego sukcesu: pokazuje gotową treść w polu możliwym do zaznaczenia oraz instrukcję ręcznego skopiowania.
- Historia użycia jest ograniczona do 500 najnowszych wpisów całej biblioteki.

## Wersje

- Zapis edytowanego promptu tworzy wersję poprzedniej treści, jeżeli zmieniły się warianty lub konfiguracja zmiennych.
- Jeden prompt przechowuje maksymalnie 50 najnowszych wersji.
- Przywrócenie wersji kopiuje jej warianty i zmienne do bieżącego promptu; nie zmienia jego tożsamości.

## Stan pusty

Brak pasujących promptów pokazuje komunikat i możliwość wyczyszczenia wyszukiwania oraz filtra.

## Kryteria akceptacji

- przy standardowej szerokości wspieranego widoku główna lista prezentuje trzy karty w wierszu,
- pływający przycisk otwiera modalny formularz, a zamknięcie dialogu zwraca fokus do przycisku,
- aktywna kategoria jest wstępnie wybrana przy tworzeniu promptu z jej widoku,
- filtry ulubionych i ostatnio używanych odzwierciedlają aktualne dane,
- kategoria obejmuje prompty ze swoich podkategorii,
- zmiana wariantu przebudowuje pola i podgląd,
- specjalne znaki z danych nie są interpretowane jako HTML,
- kopiowanie gotowego promptu dodaje wpis historii i przekazuje treść do schowka,
- niedostępny schowek zapewnia ręczną drogę skopiowania bez wysyłania treści poza aplikację,
- wersję można przywrócić bez utraty identyfikatora promptu,
- licznik wyników i stan pusty odpowiadają widocznym kartom.
- warianty pisowni z polskimi znakami i bez nich zwracają ten sam zestaw promptów,
- dopasowanie wyłącznie w treści wariantu jest wyjaśnione na karcie bez pobierania pełnej treści do listy,
- zapis promptu, wariantów, zmiennych i wersji jest jedną transakcją,
- próba zapisu nieaktualnej wersji pokazuje konflikt i nie nadpisuje nowszych danych.
