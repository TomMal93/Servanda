# ADR 0005: lokalna sesja inicjowana przez launcher

Status: accepted

Data: 2026-08-09

Zakres produktu: od v1

## Kontekst

Loopback ogranicza dostęp sieciowy, ale nie odróżnia właściwej przeglądarki od zewnętrznej strony próbującej komunikować się z lokalnym portem ani od innego użytkownika systemu. Servanda nie ma kont i nie powinna wprowadzać pełnego logowania, lecz operacje na prywatnej kolekcji wymagają dowodu, że interfejs został otwarty przez launcher bieżącego UID.

Token w query stringu trafiałby do historii, logów i potencjalnie nagłówka `Referer`. Długowieczny token dostępny JavaScriptowi albo zapisany w Web Storage zwiększałby skutek XSS i pozostawał po zamknięciu hosta.

## Decyzja

Każdy proces hosta generuje prywatny sekret sterujący dla launchera. Uwierzytelniony launcher pobiera krótko żyjący, jednorazowy bilet i przekazuje go przeglądarce wyłącznie we fragmencie URL. Bootstrap wymienia bilet na losową, pamięciową sesję procesu reprezentowaną ciasteczkiem `HttpOnly`, `SameSite=Strict`, bez `Domain`.

Zwykłe trasy aplikacji, SignalR i operacje zmieniające stan wymagają sesji od v1; od v2 dotyczy to również recovery i operacji danych. Bilet, sekret launchera i ciasteczko nie są zapisywane w Web Storage. Host kończy wszystkie sesje przy zakończeniu procesu.

Origin pozostaje warstwą ochrony przed przeglądarkowym cross-site, nie mechanizmem uwierzytelnienia. WebSocket ma osobną allowlistę originów, ponieważ reguły CORS nie chronią handshake'u WebSocket. Klasyczne POST-y dodatkowo używają tokenów antiforgery.

Pełny protokół, threat model i wymagania nagłówków definiuje `security-model.md`.

## Konsekwencje

- Ręczne wejście na odgadnięty port nie ujawnia kolekcji.
- Każde ponowne otwarcie z launchera może utworzyć nową sesję bez konta i hasła.
- Bootstrap wymaga małego, lokalnego modułu JavaScript do obsługi fragmentu i usunięcia go z historii.
- Ten sam UID pozostaje granicą zaufania i może uzyskać bilet przez prywatny sekret runtime.
- Host HTTP na loopbacku nie zapewnia szyfrowania transportu; poufność wobec procesów przejętego konta użytkownika nie jest obietnicą produktu.

## Sposób weryfikacji

- bilet użyty drugi raz albo po terminie jest odrzucany,
- fragment znika przed przejściem do aplikacji i nie trafia do logów,
- ciasteczko jest `HttpOnly`, sesyjne, `SameSite=Strict`, bez `Domain`,
- obcy origin nie może wymienić biletu, otworzyć circuitu ani wykonać shutdownu,
- restart hosta unieważnia poprzedni sekret, bilety i sesje,
- pliki runtime mają prywatnego właściciela i uprawnienia.

## Źródła techniczne

Decyzja uwzględnia oficjalne zalecenia dotyczące [bezpieczeństwa SignalR](https://learn.microsoft.com/aspnet/core/signalr/security), [ograniczania originów WebSocket](https://learn.microsoft.com/aspnet/core/fundamentals/websockets), [antiforgery ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/anti-request-forgery) i [Host Filtering Kestrela](https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel#host-filtering).
