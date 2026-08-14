# Decyzje techniczne Servandy

> Status: obowiązujący dla nowego projektu

## Decyzje przyjęte

| ID | Decyzja | Konsekwencja |
|---|---|---|
| TD-001 | Servanda jest prywatną aplikacją `local-first` dla jednego użytkownika, a Linux jest platformą podstawową. | Brak kont, ról, synchronizacji i publicznego hostingu w pierwszym wydaniu. |
| TD-002 | Aplikacja powstaje od zera w ASP.NET Core 10 i Blazor Web App na .NET 10 LTS, z target framework `net10.0` i SDK przypiętym w `global.json`. | Implementacja rozpoczyna się od rozwiązania zgodnego z bieżącą dokumentacją; aktualizacje w obrębie .NET 10 stosują wspierane poprawki serwisowe bez zmiany target framework. |
| TD-003 | Interaktywne widoki używają trybu Interactive Server. | Proces lokalny utrzymuje circuit; utrata połączenia musi mieć czytelny stan, a dane krytyczne nie mogą istnieć wyłącznie w pamięci circuitu. |
| TD-004 | Od v2 kanonicznym magazynem jest jedna lokalna baza SQLite obsługiwana przez EF Core i wersjonowane migracje. | V1 nie tworzy bazy ani danych domenowych. Moduły v2 korzystają z transakcji, kluczy obcych i jednego kontraktu trwałości; pliki JSON służą do eksportu, nie do bieżącego zapisu. |
| TD-005 | Dane, konfiguracja, stan i pliki runtime'u korzystają ze standardowych katalogów XDG. | Aktualizacja lub przeniesienie plików programu nie narusza danych użytkownika. |
| TD-006 | Kestrel nasłuchuje wyłącznie na loopbacku i domyślnie wybiera wolny port lokalny. | Aplikacja nie jest dostępna z LAN; launcher otwiera właściwy adres w domyślnej przeglądarce. |
| TD-007 | Wydanie użytkowe dla Linuksa jest publikowane jako `self-contained` i uruchamiane przez launcher oraz skrót `.desktop`. | Użytkownik nie instaluje Node, npm ani SDK .NET; architektura paczki jest jawna dla każdego artefaktu wydania. |
| TD-008 | Druga instancja nie może używać tego samego stanu aplikacji. | V1 blokuje pojedynczą instancję procesu. Od v2 długowieczny host dodatkowo utrzymuje wyłączną blokadę bazy. Launcher nie jest właścicielem blokad; odnajduje gotowy host albo uruchamia nowy. |
| TD-009 | Kopie ochronne i wersjonowany eksport rozpoczynają się w v2 razem z kanoniczną bazą. | V1 nie pokazuje tych operacji. Od v2 migracja, import i operacja masowo destrukcyjna nie rozpoczynają się bez zweryfikowanej kopii. |
| TD-010 | Obszar jest najwyższym poziomem organizacji, a każdy wyspecjalizowany moduł ma własny kontrakt i model. | Nie powstaje jedna dowolna tabela „elementów” ani schemat JSON próbujący obsłużyć wszystkie przyszłe dziedziny. |
| TD-011 | Pierwsze wydanie wspiera wyłącznie komputery osobiste i laptopy z Linuksem oraz widokiem od `1024px`. | Telefony, tablety i układ mobilny nie są projektowane ani testowane bez wcześniejszej zmiany zakresu. |
| TD-012 | V1 ma stany startu `starting` i `ready`; v2 dodaje `recovery`. | W v1 błąd startu kończy się komunikatem launchera. Od v2 błąd otwarcia bazy lub migracji pozostawia host na loopbacku w ograniczonym trybie odzyskiwania. |
| TD-013 | Import v2 zastępuje całą kolekcję po pełnej walidacji i utworzeniu kopii ochronnej. | Merge, upsert i import częściowy nie są obsługiwane; stabilne identyfikatory są zachowywane, a brakujące dane domenowe są usuwane atomowo. |
| TD-014 | Wyszukiwanie bibliotek od v2 używa aplikacyjnie utrzymywanego indeksu SQLite FTS5 z jednym dokumentem na agregat. | Wyszukiwanie działa na znormalizowanych tokenach i prefiksach; indeks jest pochodny, transakcyjnie aktualizowany i odbudowywalny z danych domenowych. Brak FTS5 jest błędem artefaktu, nie uruchamia fallbacku `LIKE`. |
| TD-015 | Od v2 treść jest chroniona rewizją korzenia agregatu, a uporządkowane listy osobną rewizją `ordering_scope`. | Dzieci promptu i relacje tagów nie mają własnej rewizji; reorder nie zmienia rewizji treści rodzeństwa, lecz konfliktuje po zmianie tej samej listy. |
| TD-016 | Dostęp do lokalnego interfejsu wymaga sesji procesu inicjowanej jednorazowym biletem launchera. | Odgadnięcie portu nie ujawnia kolekcji; sekret launchera pozostaje w prywatnym runtime, bilet trafia do fragmentu URL, a sesja do ciasteczka `HttpOnly`. `Host`, `Origin`, antiforgery i CSP pozostają niezależnymi warstwami ochrony. |
| TD-017 | Style pierwszego wydania powstają w zwykłym CSS oraz izolowanych plikach `.razor.css`, bez SASS/SCSS i frontendowego kroku npm. | Jeden normatywny zestaw tokenów definiuje system wizualny, a build i publikacja nie otrzymują dodatkowego toolchainu Node. |
| TD-018 | V1 jest bezstanową powłoką bez SQLite, modułów dziedzinowych, migracji, kopii, recovery, importu i eksportu. | Wszystkie kafle v1 mają status „Planowane”. Pierwszy kanoniczny magazyn i operacje danych powstają razem w v2, bez prowizorycznej bazy wymagającej późniejszej konwersji. |
| TD-019 | Automatyczna retencja usuwa wyłącznie zweryfikowane kopie ochronne po zakończeniu chronionej operacji, zachowując 10 najnowszych oraz po jednej dziennej z ostatnich 30 dni. | Kopie ręczne, niezgodne, niepoprawne i nieczytelne pozostają nietykalne; awaria porządkowania nie blokuje normalnego startu ani nie uruchamia recovery. |

Uzasadnienie głównych wyborów zawierają [ADR 0001](adr/0001-architektura-lokalnej-aplikacji-linux.md), [ADR 0002](adr/0002-import-zastepujacy-kolekcje.md), [ADR 0003](adr/0003-wyszukiwanie-fts5.md), [ADR 0004](adr/0004-agregaty-rewizje-i-kolejnosc.md), [ADR 0005](adr/0005-lokalna-sesja-launchera.md), [ADR 0006](adr/0006-bezstanowy-zakres-v1.md), [ADR 0007](adr/0007-dystrybucja-linux-v1.md) i [ADR 0008](adr/0008-retencja-kopii-ochronnych.md).

## Decyzje otwarte

| ID | Pytanie | Kiedy rozstrzygnąć | Domyślny kierunek |
|---|---|---|---|
| OPEN-004 | Czy szyfrowanie aplikacyjne jest konieczne dla przyszłych danych rodzinnych, zdrowotnych i finansowych | przed aktywacją pierwszego z tych modułów | polegać na szyfrowaniu systemowym w v2; nie tworzyć własnego zarządzania kluczem bez modelu odzyskiwania |

Otwarte decyzje nie pozwalają rozszerzać zakresu. Gdy rozstrzygnięcie zmienia magazyn, bezpieczeństwo, dystrybucję albo wymaga kosztownej migracji, powstaje ADR.

## Zasada implementacyjna

Dokumentacja jest źródłem zaakceptowanego zachowania. Po rozpoczęciu implementacji kod, migracje i testy są źródłem szczegółów faktycznie wdrożonych, ale nie mogą po cichu zmieniać kontraktów dokumentacji.
