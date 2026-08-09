# Wizja produktu Servanda

> Status: obowiązujący  
> Opisywany stan: kierunek produktu

## Problem

Ważne informacje, sprawdzone zasoby i osobiste ustalenia są rozproszone między pamięcią, zakładkami, historią rozmów, notatkami i dokumentami. Użytkownik musi pamiętać, gdzie ich szukać, ponownie odtwarzać wcześniejsze decyzje albo stale nosić w głowie sprawy, które powinny mieć własne, zaufane miejsce.

Dotyczy to zarówno narzędzi i promptów, jak i notatek, ważnych dat, informacji o bliskich, sprawdzonych produktach oraz innych elementów codzienności, do których warto wracać.

## Cel produktu

Zapewnić jedno zaufane miejsce, które przechowuje i porządkuje ważne informacje, zasoby oraz osobiste ustalenia, aby można było łatwo do nich wrócić bez ciągłego noszenia ich w głowie.

Produkt jest prywatną pamięcią zewnętrzną dla ważnych spraw codzienności. Nie umniejsza wartości przechowywanych treści: odciąża pamięć i uwagę użytkownika, zachowując to, czego nie chce on zgubić ani wielokrotnie odtwarzać.

## Obietnica

> Nie musisz pamiętać o wszystkim, żeby niczego ważnego nie zgubić.

Uzupełniający przekaz produktu brzmi: **Ważne rzeczy zawsze pod ręką.** Myśl przewodnią wyraża dewiza *Ad maiora natus sum* — produkt przejmuje ciężar pamiętania i powtarzalnych, codziennych ustaleń, aby uwaga użytkownika mogła pozostać przy sprawach wymagających jego obecności.

## Nazwa produktu

Produkt nosi nazwę **Servanda**. Łacińskie *servanda* oznacza „rzeczy, które należy zachować, chronić lub przechować”. Nazwa wyraża rolę aplikacji jako zaufanego miejsca dla treści, których użytkownik nie chce zgubić ani wielokrotnie odtwarzać.

## Odbiorca, platforma i model użycia

Bieżącym odbiorcą jest właściciel kolekcji. Produkt jest narzędziem `local-first`, przeznaczonym dla jednego użytkownika i uruchamianym w zaufanym środowisku. Linux na komputerze osobistym lub laptopie jest podstawową i jedyną platformą pierwszego wydania. Telefony i tablety nie są obecnie wspierane ani objęte projektowaniem interfejsu. Docelowe wydanie uruchamia się z menu aplikacji i otwiera lokalny interfejs bez wymagania od użytkownika znajomości terminala, Node ani SDK .NET.

Nie jest obecnie publicznym katalogiem, usługą zespołową ani platformą do publikowania promptów.

## Obszary, biblioteki i rozwój treści

Docelowo ekran główny jest pulpitem obszarów: centralnie prezentuje kafle różnych zagadnień, a panel boczny pozwala przechodzić między nimi i nimi zarządzać. Obszar jest najwyższym poziomem organizacji produktu; może zawierać bibliotekę, planer lub inny wyspecjalizowany moduł. Szczegółowy kontrakt tego kierunku opisuje `features/areas-dashboard.md`.

Wydanie v1 realizuje fundament uruchomienia i docelową powłokę z kaflami planowanych obszarów. Pierwsze działające biblioteki pojawiają się w v2:

| Biblioteka | Potrzeba | Rezultat |
|---|---|---|
| Narzędzia | odnaleźć sprawdzoną stronę lub aplikację | otwarcie właściwego narzędzia |
| Prompty | ponownie wykorzystać dobrą instrukcję | gotowy prompt z uzupełnionymi zmiennymi |

Obie biblioteki korzystają ze wspólnego sposobu porządkowania: hierarchicznych kategorii, wyszukiwania, kart oraz lokalnej edycji. Powstają w v2 jako nowe moduły zgodne z dokumentacją.

Poza narzędziami i promptami docelowy kierunek obejmuje następujące rodzaje osobistych treści:

| Rodzaj treści | Przykładowa wartość dla użytkownika |
|---|---|
| Ludzie i relacje | zachowanie urodzin, rocznic, preferencji bliskich, rozmiarów, pomysłów na prezenty i ważnych informacji o relacji |
| Ulubione produkty | zapisanie sprawdzonej marki, wariantu, miejsca zakupu, ceny orientacyjnej i powodu wyboru |
| Notatki | zachowanie obserwacji, pomysłów, wniosków i informacji, do których warto wrócić |
| Procedury i checklisty | ponowne wykorzystanie sprawdzonego sposobu działania, na przykład podczas pakowania, konfiguracji lub formalności |
| Przepisy i posiłki | zachowanie sprawdzonych przepisów, własnych modyfikacji, składników i zestawów posiłków |
| Miejsca i usługi | powrót do sprawdzonych restauracji, lekarzy, fachowców, sklepów i innych usługodawców |
| Zakupy | zachowanie planowanych zakupów, wymaganych parametrów, rozważanych opcji i informacji, czego unikać |
| Wiedza praktyczna | szybkie odnalezienie krótkich instrukcji, rozwiązań problemów, komend i konfiguracji |
| Materiały i inspiracje | zebranie książek, filmów, artykułów, cytatów, rekomendacji i pomysłów |
| Szablony | ponowne użycie wiadomości, dokumentów, list kontrolnych i innych powtarzalnych struktur |
| Ważne daty | zachowanie wydarzenia, powiązanej osoby, znaczenia daty i potrzebnego kontekstu |

Lista określa kierunek produktu, a nie kolejność implementacji. Rodzaje treści mogą być samodzielnymi obszarami, częścią obszaru albo funkcją wyspecjalizowanego modułu. Nie należą do bieżącego etapu i przed wdrożeniem wymagają własnego kontraktu zachowania, modelu danych oraz miejsca w roadmapie. Rozwój powinien zachować wspólną obietnicę produktu, ale nie zakładać, że każdy rodzaj treści ma identyczną strukturę lub sposób użycia. Sam zapis ważnej daty nie oznacza jeszcze kalendarza, przypomnienia ani automatyzacji.

## Zasady produktu

- Najpierw szybkość odnalezienia i użycia, potem liczba rodzajów przechowywanych treści.
- Zapisany zasób powinien mieć wystarczający opis, aby po czasie nadal był zrozumiały.
- Produkt powinien zmniejszać obciążenie pamięci i liczbę ponownie podejmowanych decyzji, a nie tworzyć kolejne miejsce wymagające stałego porządkowania.
- Dane użytkownika pozostają pod jego kontrolą i muszą dać się odzyskać.
- Interfejs powinien dobrze obsługiwać klawiaturę oraz wspierane szerokości okna na komputerach osobistych i laptopach. Wersje na telefony i tablety są poza bieżącym zakresem.
- Funkcje edycji nie mogą udawać bezpiecznych, jeżeli grożą utratą danych.
- Prosta lokalna architektura jest zaletą, dopóki odpowiada rzeczywistemu sposobowi użycia.
- Synchronizacja, konta i współpraca są osobnym kierunkiem produktu, nie domyślnym rozszerzeniem.

## Pożądany rezultat

Użytkownik powinien:

1. zapisać ważną rzecz w odpowiednim rodzaju treści bez budowania złożonego systemu organizacji,
2. znaleźć ją przez kategorię, filtr lub wyszukiwanie, gdy jest potrzebna,
3. wykorzystać ją zgodnie z jej przeznaczeniem, na przykład otworzyć narzędzie albo przygotować prompt,
4. zaufać, że pozostanie dostępna bez konieczności pamiętania jej treści lub miejsca zapisania.

## Filtr nowych funkcji

Nowa funkcja powinna spełniać co najmniej jeden warunek i nie naruszać pozostałych:

1. zmniejsza potrzebę pamiętania informacji lub ponownego podejmowania tej samej decyzji,
2. skraca znalezienie zasobu,
3. skraca przygotowanie zasobu do użycia,
4. poprawia jakość opisu lub organizacji kolekcji,
5. chroni dane lub ułatwia ich odzyskanie,
6. usuwa realne ograniczenie obecnego lokalnego przepływu.

Funkcja wymagająca kont, wielu użytkowników albo zewnętrznego backendu wymaga wcześniejszej zmiany zakresu i decyzji architektonicznej.
