# Model bezpieczeństwa lokalnego hosta

> Status: obowiązujący od v1; operacje danych stosują go od v2

## Cel i granice zaufania

Servanda działa na loopbacku, ale sam adres `127.0.0.1` lub `::1` nie uwierzytelnia przeglądarki. Zewnętrzna strona otwarta przez użytkownika może próbować wysyłać żądania do lokalnych portów, a inny użytkownik systemu może łączyć się z loopbackiem tej samej maszyny. Host wymaga więc lokalnej sesji zainicjowanej przez launcher oraz niezależnych zabezpieczeń `Host`, `Origin`, antiforgery i CSP.

Chronione zasoby v1:

- powłoka aplikacji i możliwość zamknięcia procesu,
- sekret sterujący launchera, bilety startowe i ciasteczka sesji,
- dostępność procesu i prywatne pliki runtime.

Od v2 ochrona obejmuje dodatkowo:

- treść bazy, kopii, eksportów i ekranów,
- operacje zapisu, importu i odtworzenia,
- integralność lokalnej bazy.

W zakresie zagrożeń są:

- złośliwa strona internetowa próbująca komunikować się z loopbackiem,
- DNS rebinding i sfałszowany nagłówek `Host`,
- cross-site POST, negocjacja SignalR i WebSocket z obcego originu,
- zgadywanie dynamicznego portu lub biletu startowego,
- osadzenie interfejsu w ramce i załadowanie zewnętrznego skryptu, fontu albo obrazu,
- odczyt plików runtime przez innego użytkownika systemowego,
- ujawnienie sekretu w URL, historii, logu albo nagłówku `Referer`.

Poza zakresem pierwszego wydania pozostają proces działający pod tym samym UID, administrator `root`, przejęty profil przeglądarki i złośliwe rozszerzenie przeglądarki. Są one częścią tej samej granicy zaufania co użytkownik. Aplikacja nie może jednak osłabiać ochrony przed innymi UID ani stronami internetowymi tylko dlatego, że nie broni się przed przejęciem własnego konta.

## Kanoniczny origin

- Host wiąże wyłącznie jawny adres loopback i po starcie ustala jeden kanoniczny origin zawierający schemat, adres oraz rzeczywisty port.
- Launcher otwiera wyłącznie ten origin. Aplikacja nie generuje adresów na podstawie niezweryfikowanego nagłówka żądania.
- Host Filtering ma allowlistę odpowiadającą jawnie związanym nazwom lub adresom loopback bez wildcardów. Żądanie z innym `Host` lub `:authority` jest odrzucane przed routingiem.
- Aplikacja nie używa Forwarded Headers Middleware, nie ufa nagłówkom proxy i nie ma konfiguracji publicznej domeny w pierwszym wydaniu.

## Bootstrap i sesja procesu

Każdy start hosta generuje kryptograficznie losowy sekret sterujący o co najmniej 256 bitach. Sekret:

- znajduje się w osobnym pliku runtime z trybem `0600` w katalogu `0700`, nie w publicznej części deskryptora instancji,
- jest dostępny wyłącznie hostowi i launcherowi bieżącego UID,
- nie trafia do argumentów procesu, zmiennych adresu, logów, odpowiedzi diagnostycznych ani przeglądarki,
- przestaje być ważny po zakończeniu procesu.

Przepływ otwarcia interfejsu:

1. Launcher potwierdza deskryptor instancji i przez bezpośrednie żądanie loopback uwierzytelnione sekretem sterującym prosi host o bilet startowy.
2. Host wydaje losowy bilet co najmniej 192-bitowy, ważny najwyżej 60 sekund i tylko do jednego użycia.
3. Launcher otwiera `/bootstrap#ticket=...`. Fragment nie jest wysyłany w żądaniu HTTP.
4. Mały skrypt dostarczony z paczki odczytuje fragment, usuwa go przez `history.replaceState` i wysyła bilet w ciele żądania `POST /session/bootstrap` do kanonicznego originu.
5. Host sprawdza bilet, jego termin, jednorazowość, `Host`, dokładny `Origin` i rozmiar żądania. Po sukcesie unieważnia bilet oraz tworzy losową sesję przechowywaną w pamięci procesu.
6. Przeglądarka otrzymuje nieutrwalane ciasteczko sesyjne `HttpOnly`, `SameSite=Strict`, `Path=/`, bez atrybutu `Domain`. Ciasteczko nie zawiera biletu ani sekretu launchera.
7. Zwykłe trasy aplikacji wymagają ważnej sesji; od v2 dotyczy to również recovery. Wejście bez sesji pokazuje jedynie komunikat „Otwórz Servandę przez launcher” bez danych użytkownika.

Host działa na HTTP loopback, dlatego dokumentacja nie obiecuje szyfrowania transportu. Atrybut `Secure` może być wymagany dopiero po potwierdzeniu zachowania obu wspieranych przeglądarek dla wybranego adresu loopback; brak tego atrybutu nie pozwala wysłać ciasteczka poza dokładny host i ścieżkę wynikające z powyższego kontraktu.

Publicznie dostępne bez sesji mogą być wyłącznie statyczne zasoby bootstrapu oraz minimalny endpoint potwierdzający identyfikator i stan instancji. Nie ujawniają ścieżek, a od v2 także wersji danych, błędu migracji ani informacji o kolekcji. Endpoint wydający bilet wymaga sekretu sterującego, przyjmuje wyłącznie małe żądanie i nie zezwala na CORS.

## Ochrona żądań i circuitu

| Rodzaj | Wymagane zabezpieczenia |
|---|---|
| bezpieczny GET aplikacji | poprawny `Host` i ciasteczko sesji |
| POST formularza lub endpoint zmieniający stan | sesja, dokładny `Origin`, token antiforgery i metoda inna niż GET |
| negocjacja oraz transport SignalR | sesja, dokładny kanoniczny `Origin`, brak cross-origin CORS |
| WebSocket | sesja i allowlista `AllowedOrigins` zawierająca wyłącznie kanoniczny origin |
| shutdown oraz, od v2, import i odtworzenie | wszystkie zabezpieczenia operacji zmieniającej stan oraz jawne potwierdzenie w UI |
| żądanie launchera o bilet | sekret sterujący, loopback, poprawny `Host`, limit rozmiaru i tempa; brak wymogu ciasteczka przeglądarki |

- `UseAntiforgery` pozostaje jawnie w potoku. Antiforgery chroni klasyczne POST-y, ale nie jest traktowane jako zabezpieczenie zdarzeń przesyłanych istniejącym circuitem SignalR.
- CORS nie jest włączany dla aplikacji. Odpowiedzi nie zawierają `Access-Control-Allow-Origin` dla obcych originów.
- WebSocket i wszystkie transporty SignalR mają osobną kontrolę dokładnego originu. Brak lub niezgodność `Origin` na przeglądarkowym transporcie interaktywnym powoduje odrzucenie.
- Jeżeli przeglądarka wysyła `Sec-Fetch-Site`, wartość `cross-site` jest dodatkowym powodem odrzucenia operacji zmieniającej stan. Nagłówki fetch metadata są ochroną warstwową, nie substytutem sesji ani antiforgery.
- Endpoint shutdown nie obsługuje GET, nie przyjmuje żądania bez aktywnej sesji i kończy proces dopiero po odesłaniu odpowiedzi potwierdzającej przyjęcie operacji.
- Nieudane uwierzytelnienie, błędny origin i limit żądań zwracają ogólny rezultat bez informacji pozwalającej odróżnić poprawny sekret, bilet lub identyfikator kolekcji.

## Zasoby, CSP i nagłówki

Wszystkie skrypty, style, fonty, ikony i obrazy interfejsu pochodzą z paczki aplikacji albo z bezpiecznych wartości `data:` jawnie dopuszczonych dla konkretnego rodzaju zasobu. Pierwsze wydanie nie ładuje Google Fonts, CDN, analityki, zewnętrznych map importu ani zdalnych obrazów interfejsu.

CSP jest wysyłane jako nagłówek odpowiedzi i co najmniej:

- ogranicza `default-src`, `script-src`, `style-src`, `font-src` i `connect-src` do zasobów aplikacji oraz dokładnego kanału SignalR,
- ustawia `object-src 'none'`, `base-uri 'none'`, `frame-ancestors 'none'` i `form-action 'self'`,
- nie używa `unsafe-eval`,
- nie używa `unsafe-inline` dla skryptów; wymagany skrypt inline albo mapa importu otrzymuje nonce lub hash właściwy zbudowanemu artefaktowi,
- nie dopuszcza szerokich źródeł schematowych takich jak dowolne `https:` lub `ws:`; `connect-src` jest generowane dla kanonicznego originu i jego transportu WebSocket.

Wyjątek `unsafe-inline` dla stylów jest dopuszczalny wyłącznie wtedy, gdy wymaga go przypięta wersja Blazor i test artefaktu wykaże brak węższego rozwiązania. Musi być opisany w raporcie bezpieczeństwa wydania. `frame-ancestors 'none'` pozostaje obowiązkowe również przy włączonej kompresji WebSocket.

Dodatkowe nagłówki obejmują co najmniej `Referrer-Policy: no-referrer`, `X-Content-Type-Options: nosniff` oraz politykę uprawnień blokującą niewykorzystywane API przeglądarki. Schowek jest dozwolony wyłącznie dla własnego originu i po jawnej akcji użytkownika.

## Dane przeglądarki

- V1 nie zapisuje biletu ani sesji w `localStorage`, `sessionStorage` lub IndexedDB i nie ma formularzy danych domenowych.
- Od v2 także treści formularzy, promptów i narzędzi nie trafiają do Web Storage. Niezapisany formularz istnieje wyłącznie w bieżącym komponencie i circuitcie.
- Od v2 karta z niezapisanym formularzem rejestruje lokalne ostrzeżenie `beforeunload`, a `content_epoch` odrzuca komendę otwartą przed importem albo resetem.
- Akcja zamknięcia procesu zawsze ostrzega, że rozłączy wszystkie karty. Od v2 bieżąca karta może dodatkowo nazwać własne niezapisane formularze, ale aplikacja nie obiecuje pełnego wykrycia stanu innych kart.

Trwałe drafty w przeglądarce wymagają osobnej przyszłej decyzji obejmującej retencję, czyszczenie, eksport, wiele profili przeglądarki i informację o prywatności.

## Limity i logowanie

- Nieuwierzytelnione endpointy bootstrapu mają małe, jawne limity ciała żądania i ograniczenie tempa per proces oraz adres loopback.
- Od v2 import, eksport i upload dokumentu mają limit wynikający z kontraktu formatu i przetwarzanie odporne na niekontrolowaną alokację; dokładny limit importu musi zostać ustalony przed ukończeniem P4.
- Logi nie zawierają ciasteczek, nagłówków autoryzacyjnych, sekretu sterującego, biletów, fragmentów URL, treści formularzy ani wartości antiforgery.
- Diagnostyka może rejestrować kategorię odrzucenia, identyfikator zdarzenia i zagregowany licznik, lecz nie surowy sekret, bilet ani prywatne dane.

## Kryteria akceptacji

1. Żądanie do poprawnego portu z obcym `Host` jest odrzucane przed aplikacją.
2. Cross-site POST, negocjacja SignalR i WebSocket z obcego originu nie uzyskują sesji ani nie zmieniają stanu.
3. Bilet startowy jest jednorazowy, wygasa i nigdy nie pojawia się w query stringu, logu lub nagłówku `Referer`.
4. Bez sesji nie można odczytać pulpitu; od v2 dotyczy to również recovery i danych kolekcji.
5. Shutdown wymaga sesji, właściwego originu, ochrony antiforgery i potwierdzenia; GET nie kończy procesu.
6. CSP blokuje osadzanie, zewnętrzny skrypt, font, połączenie i obraz spoza jawnej allowlisty bez blokowania zbudowanej aplikacji.
7. Artefakt nie zawiera odwołań runtime do zewnętrznych CDN ani usług analitycznych.
8. Inny UID nie może odczytać sekretu sterującego ani prywatnego deskryptora runtime.
9. Sekrety nie trafiają do Web Storage ani logów; od v2 ten sam zakaz obejmuje treść użytkownika.
10. Test v1 na Chromium i Firefox potwierdza bootstrap, ponowne uruchomienie launchera, SignalR i shutdown przy włączonej CSP. V2 rozszerza go o recovery, formularze i schowek.
