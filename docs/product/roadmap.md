# Roadmapa Servandy

> Status: obowiązujący co do kolejności  
> Zasada: etap rozpoczyna się po spełnieniu kryterium wyjścia poprzedniego etapu; roadmapa nie określa terminów

## D0 — kompletna specyfikacja

**Status:** ukończony

**Rezultat:** na podstawie dokumentacji można rozpocząć implementację aplikacji od zera bez odtwarzania nieopisanych założeń.

- ustalić zakres pierwszego wydania,
- opisać obszary i oba pierwsze moduły,
- przyjąć architekturę Blazor, SQLite i Linux,
- opisać model danych, migracje, kopie, import i eksport,
- zdefiniować uruchomienie użytkowe oraz deweloperskie,
- zamknąć sprzeczności oraz jednoznacznie opisać zakres wspieranych platform i urządzeń.

**Kryterium wyjścia:** wszystkie źródła prawdy mają spójny stan greenfield, linki dokumentacji działają, a otwarte decyzje nie blokują utworzenia rozwiązania.

## P1 — fundament hosta v1

**Status:** aktywny

**Wersja:** v1  
**Rezultat:** bezstanowa aplikacja uruchamia się bezpiecznie na Linuksie i pokazuje chronioną powłokę w lokalnej przeglądarce.

- utworzyć rozwiązanie .NET zgodne z `code-conventions.md`,
- przypiąć wspierane wydanie LTS SDK,
- utworzyć host Blazor Interactive Server nasłuchujący tylko na loopbacku,
- wdrożyć sesję procesu inicjowaną przez launcher, filtrowanie `Host`, kontrolę `Origin`, antiforgery i wymuszaną CSP zgodnie z `security-model.md`,
- wdrożyć prywatne katalogi XDG dla runtime i bezpiecznych logów; nie tworzyć pustego magazynu ustawień,
- wdrożyć blokadę pojedynczej instancji procesu oraz atomowy protokół publikowania adresu i stanu `ready`,
- utworzyć testy konfiguracji, bezpieczeństwa i cyklu życia hosta,
- przygotować publikację `self-contained`, launcher i wpis `.desktop`.

**Kryterium wyjścia:** czysta instalacja na wspieranym Linuksie uruchamia aplikację z menu, drugi launcher otwiera potwierdzoną istniejącą instancję, a niepowodzenie startu kończy się bezpiecznym komunikatem launchera bez otwierania niepotwierdzonego adresu.

## P2 — pulpit v1

**Status:** później  
**Wersja:** v1  
**Rezultat:** użytkownik widzi docelową powłokę i statyczny pulpit planowanych obszarów bez pozorowania gotowych modułów.

- wdrożyć tokeny, typografię, ikony, stany komponentów i zwykły CSS zgodnie z `../design/ui-system.md`,
- zbudować adaptacyjną powłokę, panel boczny i ekran główny,
- dodać statyczny początkowy zestaw obszarów oznaczonych „Planowane”,
- nie udostępniać edytorów, zapisu, wyszukiwania, operacji danych ani akcji „Zarządzaj obszarami”,
- dodać testy klawiatury, kontrastu, długich treści, reflow 200%/400%, wspieranych szerokości i głównych przepływów.

**Kryterium wyjścia v1:** artefakt użytkowy działa bez Node, npm i SDK .NET; kafle są użyteczne na wymaganych szerokościach, każdy obszar jest jednoznacznie planowany, a system UI spełnia własne kryteria akceptacji dostępności i reflow.

## P3 — fundament danych i odzyskiwania v2

**Status:** później  
**Wersja:** v2  
**Rezultat:** Servanda otrzymuje pierwszy kanoniczny magazyn danych, bezpieczne aktualizacje schematu oraz odzyskiwalność przed udostępnieniem zapisu modułom.

- utworzyć SQLite, pierwszą wersję schematu, migracje i transakcyjną warstwę trwałości,
- wdrożyć blokadę bazy utrzymywaną przez długowieczny host,
- wdrożyć kopie ochronne, wersjonowane migracje i stan `recovery`,
- przetestować odtworzenie zgodnej kopii oraz awarię migracji,
- wdrożyć trwały model obszarów, kolejność, widoczność i archiwizację,
- wdrożyć optymistyczną kontrolę współbieżności korzeni agregatów i zakresów kolejności zgodnie z ADR 0004,
- pokryć testami migracje, kopie, recovery, konflikt i krytyczne E2E fundamentu danych.

**Kryterium wyjścia:** aktualizacja schematu nie narusza poprzedniej bazy ani zweryfikowanej kopii, recovery potrafi odtworzyć zgodny stan, a konflikt między kartami nie powoduje cichego nadpisania.

## P4 — pierwsze moduły v2

**Status:** później  
**Wersja:** v2  
**Rezultat:** katalog narzędzi oraz biblioteka promptów stają się pierwszymi aktywnymi obszarami opartymi na fundamencie danych v2.

- wdrożyć katalog narzędzi i jego edytor,
- wdrożyć bibliotekę promptów, Prompt Studio, wersje i historię użycia,
- wdrożyć wspólne kategorie, tagi i wyszukiwanie FTS5 zgodnie z kontraktami,
- wdrożyć eksport zgodny z JSON Schema formatu 1 i walidowany import zastępujący całą kolekcję, z podglądem skutków i unieważnieniem wcześniej otwartych sesji edycji,
- objąć oba moduły kopiami i recovery,
- pokryć testami zapis, konflikt, wyszukiwanie, import, eksport i krytyczne E2E.

**Kryterium wyjścia:** oba moduły spełniają własne kryteria akceptacji, zachowują dane po restarcie i mogą zostać odtworzone z eksportu lub zgodnej kopii, a błędny import pozostawia kolekcję i kopię bez zmian.

## P5 — pozostałe obszary v2

**Status:** później  
**Wersja:** v2, etapami  
**Rezultat:** kolejne planowane kafle są aktywowane pojedynczo na podstawie osobnych kontraktów.

Każdy moduł jest częścią kierunku v2, ale nie może zostać aktywowany wyłącznie na podstawie nazwy kafla. Rekomendowana kolejność analizy, nie automatyczna kolejność implementacji:

1. Notatki — format treści, załączniki, linkowanie i historia zmian,
2. Dom — harmonogram prac porządkowych,
3. Rodzina — ważne informacje i daty dotyczące bliskich,
4. Witalność — zdrowie, dieta, trening i biohacking,
5. Budżet domowy — miesięczny plan wpływów, kosztów i celów.

Każdy moduł wymaga osobnej oceny prywatności, modelu danych, kryteriów akceptacji i mechanizmu eksportu. Moduł witalności nie może udawać narzędzia medycznego, a budżet nie może wykonywać operacji bankowych bez osobnej decyzji.

## P6 — synchronizacja warunkowa

**Status:** warunkowy  
**Rezultat:** synchronizacja powstaje wyłącznie po potwierdzeniu potrzeby wielu urządzeń.

Etap wymaga nowego ADR obejmującego uwierzytelnienie, szyfrowanie, magazyn zdalny, konflikty, kopie, migrację danych oraz koszty utrzymania. Do tego czasu aplikacja pozostaje lokalna i jednoosobowa.

## Rejestr pomysłów bez zatwierdzonego zakresu

- zunifikowane centrum ustawień, kopii, retencji, diagnostyki i operacji systemowych,
- trwałe drafty formularzy w przeglądarce wraz z retencją i czyszczeniem,
- przypomnienia systemowe,
- integracja z kalendarzem,
- semantyczne wyszukiwanie,
- automatyczne tagowanie,
- wykonywanie promptów przez API modeli,
- współdzielone obszary,
- wersje na telefony i tablety,
- rozszerzenie przeglądarki.

Pozycja na tej liście nie jest zgodą na implementację.
