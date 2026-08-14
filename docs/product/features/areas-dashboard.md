# Obszary i ekran główny

> Status: obowiązujący; statyczny pulpit w v1, aktywne moduły i zarządzanie od v2

## Cel

Ekran główny jest punktem wejścia do różnych zagadnień codzienności przechowywanych w Servandzie. Zamiast otwierać od razu jedną bibliotekę, pozwala szybko wybrać obszar, w którym użytkownik chce pracować.

**Obszar** jest najwyższym poziomem organizacji produktu. Może prowadzić do biblioteki treści, planera albo innego wyspecjalizowanego modułu. Nie jest kategorią: kategorie porządkują zawartość wewnątrz obszaru.

## Ekran główny

- Główna część ekranu MUSI prezentować centralnie umieszczoną, responsywną siatkę kafli obszarów.
- Obszary pełnią na ekranie głównym rolę głównych kategorii produktu. Interfejs może opisywać je użytkownikowi jako kategorie, ale model danych i dokumentacja techniczna zachowują nazwę „obszar”, aby nie mylić ich z kategoriami porządkującymi elementy wewnątrz modułu.
- Każdy kafel MUSI mieć nazwę, krótki opis i rozpoznawalną ikonę lub akcent wizualny. Kafel aktywny prowadzi do modułu, a planowany komunikuje swój status bez udawania działającej funkcji.
- Kafel MUSI pozostawać zrozumiały bez polegania wyłącznie na kolorze lub ikonie.
- Układ POWINIEN eksponować zagadnienia, a nie szczegóły ich wewnętrznej struktury. Kategorie, filtry i elementy treści pojawiają się dopiero po wejściu do obszaru.
- Siatka MUSI zachować wizualne wyśrodkowanie i czytelny rytm we wspieranych widokach komputerów osobistych i laptopów od `1024px`. Telefony, tablety i układ mobilny nie należą do pierwszego wydania.

Początkowo przewidziane obszary:

| Obszar | Przeznaczenie |
|---|---|
| Skarbiec promptów | przechowywanie, przygotowywanie i ponowne używanie promptów |
| Przechowalnia narzędzi | katalog sprawdzonych stron i aplikacji |
| Dom | harmonogram prac porządkowych i innych obowiązków domowych |
| Rodzina | dbanie o bliskich, ważne informacje, potrzeby, daty i relacje |
| Witalność | zdrowie, biohacking, dieta i trening |
| Przechowalnia notatek | zapisywanie pomysłów, obserwacji i informacji do późniejszego użycia |
| Budżet domowy | planowanie miesięcznego budżetu gospodarstwa domowego |

Nazwy i zakresy są punktem wyjścia. Każdy moduł poza narzędziami i promptami wymaga przed implementacją własnego kontraktu zachowania, modelu danych i kryteriów akceptacji.

W v1 wszystkie kafle mają status „Planowane” i nie prowadzą do pustego widoku udającego gotową funkcję. „Skarbiec promptów” i „Przechowalnia narzędzi” stają się aktywne w v2 po wdrożeniu ich kontraktów. Każdy pozostały obszar może zostać aktywowany w v2 dopiero po dodaniu własnej specyfikacji, modelu danych i kryteriów akceptacji.

## Panel boczny

- Panel boczny MUSI umożliwiać przejście do ekranu głównego oraz między dostępnymi obszarami.
- W v1 panel pokazuje statyczny zestaw planowanych obszarów i nie zawiera akcji „Zarządzaj obszarami”.
- Od v2 panel boczny MUSI zawierać jawne wejście „Zarządzaj obszarami”.
- Od v2 „Zarządzaj obszarami” nie jest pozycją głównej nawigacji. Jest dostępne w trybie „Ustawienia”, otwieranym stałym przyciskiem z ikoną ustawień umieszczonym na dole panelu bocznego.
- Zarządzanie obszarami od v2 MUSI obejmować dodawanie i edycję obszaru, zmianę kolejności, kontrolę widoczności oraz archiwizację.
- Archiwizacja ukrywa obszar z głównej nawigacji, lecz zachowuje jego dane i umożliwia przywrócenie.
- Trwałe usunięcie obszaru i jego zawartości nie należy do v2.
- Po wejściu do obszaru panel może pokazywać jego filtry i kategorie, ale musi zachować czytelną drogę powrotu do listy obszarów.

## Kryteria akceptacji kierunku

1. Po uruchomieniu aplikacji użytkownik widzi ekran główny z centralnie ułożonymi kaflami obszarów.
2. W v1 każdy kafel jednoznacznie komunikuje planowany charakter i nie otwiera pozornego modułu.
3. Od v2 wybranie aktywnego kafla otwiera moduł spełniający własny kontrakt, a kafel planowany nadal jednoznacznie komunikuje brak aktywnej funkcji.
4. W v1 z panelu bocznego można wrócić do ekranu głównego; od v2 można również przejść do aktywnego obszaru i otworzyć zarządzanie obszarami.
5. Układ pozostaje użyteczny z klawiaturą oraz we wszystkich wspieranych szerokościach widoku komputerowego.
6. Od v2 zarządzanie obszarami nie usuwa danych bez jednoznacznego ostrzeżenia i odzyskiwalnego mechanizmu ochronnego.
7. Funkcje administracyjne nie konkurują w panelu bocznym z wyborem obszaru i kategorii; „Zarządzaj obszarami” jest dostępne w trybie „Ustawienia”.

## Poza tym kontraktem

Ten dokument nie definiuje:

- szczegółowych przepływów harmonogramu domu, rodziny, witalności, notatek i budżetu,
- przypomnień, automatyzacji, integracji kalendarza ani danych medycznych,
- współdzielenia obszarów z innymi użytkownikami.

Model i trwałość obszarów definiuje `../../engineering/data-model.md`.
