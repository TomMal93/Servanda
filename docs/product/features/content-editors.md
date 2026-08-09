# Edytory i zapis danych

> Status: obowiązujący dla v2

## Cel

Edytory umożliwiają zarządzanie obszarami, narzędziami, promptami i ich strukturą bez dostępu do bazy. Kontrakt modelu definiuje [data-model.md](../../engineering/data-model.md).

## Wspólny kontrakt edytora

- Edytor otwiera roboczą kopię agregatu oraz zapamiętuje `revision` jego korzenia. Dzieci posiadane nie mają niezależnych rewizji.
- Zmiana pól nie jest przedstawiana jako zapisana, dopóki serwerowa komenda nie zakończy transakcji.
- Formularz waliduje dane dla wygody, a warstwa domenowa powtarza pełną walidację przed zapisem.
- Zapis agregatu odbywa się w jednej transakcji SQLite.
- Jeżeli bazowa `revision` korzenia jest nieaktualna, zapis całego agregatu kończy się konfliktem bez nadpisania danych.
- Interfejs pozwala odświeżyć aktualną wersję; automatyczne scalanie nie należy do v2.
- Status ma co najmniej stany: `niezapisane zmiany`, `zapisywanie`, `zapisano`, `błąd` i `konflikt`.
- Status jest widoczny tekstowo i ogłaszany przez `aria-live`.

Granice agregatów, właścicieli rewizji i techniczne wymagania zapisu definiują `data-model.md` oraz ADR 0004.

## Niezapisana sesja edycji

- Pierwsze wydanie przechowuje niezapisany formularz wyłącznie w bieżącym komponencie i circuitcie. Nie zapisuje treści formularza w `localStorage`, `sessionStorage` ani IndexedDB.
- Sesja edycji zawiera identyfikator, bazową `revision` i `content_epoch`. Zapis z niezgodnym tokenem kończy się konfliktem bez zmiany bazy.
- Karta z niezapisanymi zmianami rejestruje lokalne ostrzeżenie `beforeunload`; treść i sposób pokazania komunikatu mogą zależeć od przeglądarki.
- Utrata circuitu albo zamknięcie karty może utracić niezapisane zmiany. Komunikat rozróżnia tę sytuację od danych wcześniej zapisanych w SQLite.
- Trwałe drafty przeglądarki należą do późniejszej wersji i wymagają osobnego kontraktu prywatności, retencji, czyszczenia i wielu profili przeglądarki.

## Zarządzanie obszarami

Edytor obszarów umożliwia:

- utworzenie obszaru planowanego,
- edycję nazwy, opisu, ikony i akcentu,
- zmianę kolejności kafli,
- ukrycie i ponowne pokazanie,
- archiwizację i przywrócenie obszaru.

Użytkownik nie może sam przypisać dowolnego `module_key`. Aktywacja wyspecjalizowanego modułu wynika z obsługiwanej konfiguracji aplikacji. Archiwizacja nie usuwa danych. Trwałe usuwanie obszaru nie należy do v2.

## Zmiana kolejności

- Widok każdej uporządkowanej listy otrzymuje `orderingRevision` jej zakresu niezależnie od rewizji treści elementów.
- Komenda przekazuje przenoszony identyfikator, oczekiwaną rewizję zakresu źródłowego i docelowego oraz identyfikator elementu docelowego albo polecenie dopisania na końcu.
- Przeniesienie między rodzicami, kategoriami lub grupami przekazuje również oczekiwaną `revision` przenoszonego korzenia.
- Serwer sprawdza tokeny, przelicza pełną kolejność i zapisuje oba zakresy w jednej transakcji. Interfejs nie wysyła samodzielnych zapisów kolejnych rekordów.
- Konflikt kolejności pokazuje komunikat, że lista zmieniła się w innej karcie, i pozwala wczytać aktualny układ. Aplikacja nie powtarza automatycznie gestu na nowej liście.
- Reorder wariantów i zmiennych odbywa się wewnątrz edytora promptu i używa rewizji promptu, nie osobnego `orderingRevision`.
- Zwykły zapis formularza nie wysyła starego `sort_order` ani nie zmienia członkostwa listy. Utworzenie i usunięcie elementu przekazują oczekiwane rewizje dotkniętych zakresów; nowy element trafia na koniec listy.

## Edytor katalogu

Umożliwia:

- tworzenie i edycję kategorii,
- zagnieżdżanie kategorii bez umieszczania jej we własnym poddrzewie,
- tworzenie i edycję narzędzi,
- przenoszenie narzędzia do innej kategorii i grupy,
- zmianę kolejności elementów,
- usuwanie narzędzia,
- usuwanie kategorii wraz z podkategoriami i narzędziami dopiero po pokazaniu podsumowania skutków.

## Edytor promptów

Umożliwia:

- tworzenie i edycję kategorii promptów,
- tworzenie, edycję, przenoszenie i usuwanie promptów,
- dodawanie, edycję i usuwanie wariantów przy zachowaniu co najmniej jednego,
- wykrywanie nazw zmiennych z zapisu `{{nazwa}}`,
- konfigurację etykiety, wartości domyślnej, wymagalności i pola wielowierszowego,
- zmianę kolejności kategorii i promptów,
- oznaczenie promptu jako ulubionego,
- przywrócenie jednej z zachowanych wersji bez zmiany tożsamości promptu.

Szybkie oznaczenie promptu jako ulubionego jest komendą zapisu z oczekiwaną rewizją promptu. Konflikt odświeża kartę i nie stosuje zasady ostatniego zapisu.

## Operacje destrukcyjne

- Pojedyncze usunięcie wymaga komunikatu nazywającego element i skutek.
- Usunięcie kategorii pokazuje liczbę podkategorii i elementów, które znikną.
- Reset, import zastępujący dane i operacja masowa wymagają zweryfikowanej kopii przed rozpoczęciem.
- Potwierdzenie destrukcyjne nie może być domyślnie aktywne ani ukrywać zakresu operacji.
- Błąd w połowie operacji wycofuje całą transakcję.

## Import i eksport

- Eksport nie wymaga edycji, nie zmienia danych i obejmuje pełny zakres opisany w `data-model.md`.
- V2 obsługuje wyłącznie import zastępujący całą kolekcję. Merge, upsert i import wybranych sekcji nie są dostępne.
- Import najpierw pokazuje wynik walidacji, wersję schematu oraz liczbę elementów dodawanych, zastępowanych i usuwanych dla każdego rodzaju danych.
- Podgląd nazywa operację zastąpieniem kolekcji, ostrzega o usunięciu danych nieobecnych w dokumencie oraz o unieważnieniu wszystkich otwartych sesji edycji.
- Po jawnym potwierdzeniu aplikacja tworzy i weryfikuje kopię ochronną bezpośrednio przed rozpoczęciem transakcji zastępującej dane.
- Niepoprawny lub nieobsługiwany dokument nie zmienia bazy.
- Po imporcie interfejs odświeża dane. Nowy `content_epoch` powoduje, że próba zapisu formularza otwartego przed importem kończy się konfliktem i wymaga ponownego otwarcia edytora.

## Kryteria akceptacji

- formularz nie zapisuje brakujących ani niepoprawnych danych,
- walidacja domenowa chroni bazę niezależnie od interfejsu,
- zapis agregatu jest atomowy,
- błąd nie pozostawia częściowo zmienionych relacji,
- konflikt nie nadpisuje nowszej wersji,
- konflikt reorderu nie pozostawia częściowo przenumerowanej listy ani elementu w niewłaściwym zakresie,
- użytkownik rozróżnia niezapisany stan bieżącej karty od danych zapisanych w SQLite,
- zamknięcie karty z niezapisanym formularzem uruchamia dostępne ostrzeżenie przeglądarki,
- operacja destrukcyjna pokazuje zakres i respektuje wymagania kopii,
- statusy są dostępne dla czytnika ekranu,
- żadna operacja zapisu nie jest dostępna spoza lokalnej instancji aplikacji.
