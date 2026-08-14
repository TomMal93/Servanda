# Analiza kodu — 2026-08-11

Dokument nienormatywny. Jest migawką stanu kodu na dzień przeglądu, nie źródłem kontraktu.
Źródła prawdy pozostają w `docs/product/`, `docs/engineering/` i `docs/design/` zgodnie z `docs/README.md`.

- Zakres: całe rozwiązanie `.NET` (`src/`, `tests/`, konfiguracja budowania).
- Commit bazowy: `4b24bcf` (`test: domknij kryterium wyjścia P1`), gałąź `main`.
- Etap projektu: ukończone P1, otwarte P2.

## Stan wyjściowy

Zweryfikowane przed analizą:

| Sprawdzenie | Wynik |
|---|---|
| `dotnet build` | powodzenie, 0 ostrzeżeń |
| `dotnet format --verify-no-changes` | czysto (kod wyjścia 0) |
| `dotnet test --filter "Category!=Browser"` | 32/32 powodzenie (9 + 23) |
| uruchomienie hosta na izolowanych katalogach XDG + SIGTERM | `HOST_STARTING`, `HOST_READY`, `HOST_STOPPED` zapisane poprawnie |

Kod jest wyraźnie powyżej średniej. Model bezpieczeństwa hosta jest przemyślany i faktycznie
warstwowy. Poniższe uwagi uporządkowano według ważności.

---

## 1. Blokada wdrożeniowa: P/Invoke `stat` wymaga glibc ≥ 2.33

**Plik:** `src/Servanda.Infrastructure/Runtime/LinuxIdentity.cs:16`

Import symbolu `stat` z libc. Weryfikacja na maszynie deweloperskiej:

```
readelf --dyn-syms /lib64/libc.so.6 | grep ' stat'
696: 00000000000e9530  22 FUNC  WEAK  DEFAULT  4  stat@@GLIBC_2.33
```

`stat` stał się eksportowanym symbolem dopiero w glibc 2.33 (luty 2021). Wcześniej istniał
wyłącznie `__xstat`. Na Debianie 11, Ubuntu 20.04 i RHEL 8/9 (glibc 2.28–2.31) wystąpi
`EntryPointNotFoundException`, i to w **pierwszej linijce** `Program.Main:21`
(`CreateAndVerify` → `EnsureDirectory` → `GetOwnershipAndMode`), czyli zanim powstanie
`TechnicalLogWriter` i zanim launcher ma szansę pokazać `launcher-error.html`.
Użytkownik zobaczy goły stack trace albo nic.

Jest to sprzeczne z obietnicą z `README.md` o „przenośnym, samowystarczalnym katalogu bez
zależności". `self-contained` dotyczy runtime'u .NET, nie glibc.

**Naprawa:** fallback na `__xstat(1, path, out buf)` przy `EntryPointNotFoundException`, albo
pobieranie trybu przez `File.GetUnixFileMode(path)` (BCL, .NET 7+) i P/Invoke wyłącznie dla UID
właściciela.

**Przy okazji:** układ `LinuxStat` (`LinuxIdentity.cs:39-62`) jest specyficzny dla x86-64.
Jest zgodny z `RuntimeIdentifiers=linux-x64`, ale na arm64 dałby cichy błąd pamięci.
Warto to udokumentować w komentarzu przy strukturze.

---

## 2. Rate limiter jako wektor lokalnej blokady

**Pliki:** `src/Servanda.App/Program.cs:65-75`, `:92`, `:109`

`AddFixedWindowLimiter("launcher")` jest **globalny** (niepartycjonowany): 10 żądań/min,
współdzielony przez `/launcher/ticket` i `/session/bootstrap`. Kluczowe: `app.UseRateLimiter()`
działa **przed** weryfikacją sekretu, która siedzi dopiero w delegacie endpointu.

Port loopback jest widoczny dla wszystkich użytkowników maszyny. Dowolny lokalny proces może
wysłać 10 nieautoryzowanych POST-ów na `/launcher/ticket` i zablokować właścicielowi otwarcie
aplikacji na minutę — bez znajomości sekretu. Dodatkowo jeden normalny start zużywa 2 z 10
slotów (bilet + bootstrap), więc realny limit to 5 uruchomień na minutę.

**Naprawa:** przenieść uwierzytelnienie sekretem przed limiter (własne middleware albo filtr
endpointu), albo liczyć tylko udane wydania biletu.

---

## 3. Brak górnego handlera — awarie kończą się stack trace'em zamiast komunikatem

Wzorzec powtarza się w kilku miejscach:

- `Program.cs:21` — `CreateAndVerify()` poza jakimkolwiek `try`.
- `Program.cs:38` — `InstanceLock.TryAcquire` łapie tylko `IOException`;
  `UnauthorizedAccessException` z `VerifyPrivateFile` (plik lock ma zły tryb) leci na zewnątrz,
  i to **przed** utworzeniem `technicalLog`, więc blok `catch`/`finally` z linii 159-169 nawet
  się nie wykona.
- `ControlSecretReader.cs:34` — łapie tylko `IOException`. `UnauthorizedAccessException`
  przelatuje przez `Launcher.RequestTicketAsync` poza `RunAsync()` i wywala launcher, zamiast
  wywołać `ShowError()`.
- `Launcher.cs:89-96` — `ConfirmInstanceAsync` łapie `HttpRequestException` i
  `TaskCanceledException`, ale **nie** `JsonException` ani `NotSupportedException`. Scenariusz
  jest realny: host padł twardo (SIGKILL), deskryptor został, inny lokalny proces zajął ten port.
  `GetFromJsonAsync` dostaje nie-JSON i rzuca — launcher pada, zamiast uznać instancję za
  niepotwierdzoną i wystartować nową.

Cała maszyneria `launcher-error.html` / `ShowError()` jest bezużyteczna, jeżeli typowe awarie ją
omijają.

**Naprawa:** owinąć `Main` w `try`/`catch` mapujący na kod wyjścia i `ShowError()`.

---

## 4. Architektura Blazor: globalny `InteractiveServer` dla bezstanowej powłoki

Główna rekomendacja przed P2.

**Pliki:** `src/Servanda.App/Components/App.razor:14`, `Components/Routes.razor`

`Routes` montowany jest z `@rendermode="InteractiveServer"` globalnie, podczas gdy dokumentacja
mówi, że V1 jest bezstanową powłoką ze statycznym pulpitem, a edytory zaczynają się w v2.

Koszt tej decyzji widać w całym kodzie:

- obwód SignalR i stan serwera dla każdej karty,
- `ReconnectModal` z pełną obsługą wznawiania,
- specjalne przypadki dla `/_blazor` i `/_blazor/initializers` w `ProcessSessionMiddleware.cs:30-37`,
- `connect-src` z `ws://` w CSP.

Jedyna interaktywność w aplikacji to przełącznik pokazujący formularz potwierdzenia zamknięcia,
a to da się zrobić statycznym SSR (`<details>`, druga strona, albo progresywne wzbogacenie
JavaScriptem, który i tak już istnieje).

Przejście na statyczny SSR jako domyślny — z ewentualnym `@rendermode="InteractiveServer"` per
komponent, gdy v2 tego zażąda — usuwa SignalR, obwody, modal reconnectu, dwa wyjątki w middleware
i połowę CSP. Mniejsza powierzchnia ataku i szybszy start, czyli dokładnie to, co deklaruje
`docs/product/current-scope.md`.

Decyzja tej wagi powinna trafić do ADR.

---

## 5. Przepływ zamykania jest kruchy

**Pliki:** `src/Servanda.App/wwwroot/shutdown.js`, `Hosting/ShutdownEndpoint.cs:21`,
`Components/Layout/ShutdownControl.razor:17`

`shutdown.js` przechwytuje globalny `submit` na `document`, dopasowuje po `form.id`, a po
sukcesie robi `document.body.replaceChildren(main)` (`shutdown.js:30`). Obwód Blazora **nadal
żyje** i trzyma referencje do usuniętych węzłów. Jeżeli serwer nie zdąży się zamknąć przed
kolejnym batchem renderowania — a `OnCompleted` w `ShutdownEndpoint.cs:21` odpala
`StopApplication()` dopiero po wysłaniu odpowiedzi — następny render rzuci błąd w konsoli
i pokaże `#blazor-error-ui`. Dodatkowo `history.replaceState(null, "", "/shutdown")`
(`shutdown.js:32`) zostawia adres, który po odświeżeniu daje 401.

W modelu z punktu 4 problem znika: albo zwykły POST z pełnym przeładowaniem strony (bez JS),
albo `@onclick` → `IHostApplicationLifetime.StopApplication()` przez obwód (antiforgery zbędne,
bo obwód jest już uwierzytelniony ciasteczkiem i Originem).

**Dostępność:** `role="alert"` na `<p id="shutdown-warning">` siedzi w DOM od początku i jest
tylko chowany przez `hidden`. Ogłaszanie zmiany widoczności regionu `alert` jest niekonsekwentne
między czytnikami ekranu. Formularz należy renderować warunkowo (`@if`) — wstawienie do DOM
ogłosi się niezawodnie. Ten sam element jest też reużywany przez `shutdown.js:36` do komunikatu
błędu, czyli jeden `alert` pełni dwie różne role.

---

## 6. `ProcessSessionStore` bez wygasania i ewikcji

**Plik:** `src/Servanda.App/Security/ProcessSessionStore.cs`

Sesja żyje tak długo, jak proces. Słownik rośnie o wpis na każde uruchomienie launchera.
Nie ma TTL, limitu rozmiaru ani unieważniania. Dla jednego użytkownika na desktopie to niski
priorytet, ale jest to magazyn sesji bez polityki czasu życia.

**Naprawa:** TTL z odświeżaniem i twardy limit liczby sesji. `BootstrapTicketStore` ma to
zrobione poprawnie — warto ujednolicić oba magazyny.

---

## 7. Pozostałości szablonu (do posprzątania przed design systemem P2)

- **`wwwroot/app.css`** — cały plik to nietknięty szablon: reguły dla Bootstrapa
  (`.form-floating`, `.darker-border-checkbox`, `var(--bs-secondary-color)`), którego w projekcie
  nie ma, plus base64 SVG `blazor-error-boundary`. Martwy kod w pliku, który za chwilę stanie się
  fundamentem systemu interfejsu.
- **Teksty po angielsku w polskim produkcie** — `Error.razor`, `NotFound.razor`,
  `ReconnectModal.razor`, `#blazor-error-ui` w `MainLayout.razor:7-11`. `Error.razor` dodatkowo
  instruuje użytkownika o `ASPNETCORE_ENVIRONMENT=Development`.
- **`Error.razor:29`** — `[CascadingParameter] HttpContext` nigdy nie zostanie dostarczony,
  bo komponent renderuje się interaktywnie (kaskada `HttpContext` istnieje wyłącznie w statycznym
  SSR). `RequestId` zawsze spadnie na `Activity.Current?.Id`.
- **`appsettings.Development.json`** jest co do znaku identyczny z `appsettings.json` — do
  usunięcia. `"AllowedHosts": "*"` jest martwe (nie ma `UseHostFiltering`), a przy tym mylące,
  bo sugeruje coś przeciwnego niż `LocalHostSecurityMiddleware`.
- **`MainLayout`** nie ma landmarku `<main>`. `FocusOnNavigate Selector="h1"` działa, ale
  nawigacja po landmarkach nie.

---

## 8. Drobne, ale warte poprawy

- **`ResolveContentRoot()` (`Program.cs:188`)** — zakodowane trzy poziomy w górę. Przy
  `dotnet run -r linux-x64` albo `build -o` ścieżka się nie zgadza i funkcja **cicho** wraca do
  `AppContext.BaseDirectory` bez `wwwroot`: aplikacja wstanie bez CSS i JS, bez żadnego błędu.
  Lepiej: pętla w górę do pierwszego katalogu z `Servanda.App.csproj`, z limitem głębokości.
- **Stałe 500 ms na każdym zimnym starcie** — `Launcher.cs:25` woła
  `WaitForConfirmedInstanceAsync(500 ms)`, a pętla `do/while` (`Launcher.cs:61-71`) czeka pełne
  5 × 100 ms także wtedy, gdy pliku deskryptora po prostu nie ma. Brak pliku jest jednoznaczny —
  należy wyjść od razu.
- **`HttpClient` tworzony w każdej iteracji pollingu** — `Launcher.cs:82` powołuje nowy
  `HttpClient` i `SocketsHttpHandler` przy każdym obrocie pętli, do ~100 instancji w oknie 10 s.
  Przy tej skali nie wyczerpie portów, ale to podręcznikowy antywzorzec. Wystarczy jeden klient
  na `RunAsync`.
- **Trzecia deklaracja `geteuid`** — `LinuxLauncherPlatform.cs:120` ma własny
  `[DllImport("libc")] geteuid()` obok `LinuxIdentity.geteuid` (bo tamten jest `internal`
  w innym projekcie). Używa przy tym starego `DllImport` zamiast `LibraryImport`, którym
  posługuje się reszta kodu — niespójność i przeszkoda dla pełnego AOT. Należy wystawić
  `LinuxIdentity` publicznie.
- **UID pobierany przy każdej operacji** — `TechnicalLogWriter.RotateIfRequired` woła
  `GetEffectiveUserId()` do czterech razy na rotację, `InstanceDescriptorReader` przy każdym
  odczycie. UID nie zmienia się w cyklu życia procesu; `static readonly Lazy<uint>` załatwia
  sprawę.
- **Martwy kod** — `PrivateFileSystem.cs:11-14`:
  `if (!OperatingSystem.IsLinux()) { LinuxIdentity.EnsureLinux(); return; }`. `EnsureLinux()`
  zawsze rzuca, więc `return` jest nieosiągalny. Wystarczy `LinuxIdentity.EnsureLinux();` na
  górze metody, jak w `VerifyPrivateFile`.
- **Duplikat `SecretSize = 32`** w `ControlSecret.cs:7` i `ControlSecretReader.cs:5` — dwie
  prywatne stałe, które muszą się zgadzać, bez żadnego wiązania.
- **`AtomicInstanceDescriptorStore.GetEffectiveUserId()` (`:69-72`)** — prywatny wrapper bez
  wartości dodanej na `LinuxIdentity.GetEffectiveUserId()`.
- **TOCTOU w `PrivateFileSystem`** — `RejectSymbolicLink` (lstat) i `GetOwnershipAndMode` (stat,
  podąża za dowiązaniem) to dwie osobne operacje na ścieżce. Praktyczne ryzyko jest zerowe, bo
  katalog nadrzędny ma 0700, ale warto zapisać to w komentarzu, żeby nikt później nie użył tej
  klasy na ścieżce spoza prywatnego katalogu. Czystszy wariant: `open` i `fstat` na uchwycie.
- **Podwójny `Dispose`** — `technicalLog` i `controlSecret` są jednocześnie w `using`
  w `RunHostAsync` i zarejestrowane w DI. Zweryfikowane empirycznie: `HOST_STOPPED` zapisuje się
  poprawnie, więc kontener tych instancji nie sprząta. Ale `TechnicalLogWriter.Dispose()`
  — w odróżnieniu od `ControlSecret` — nie ma strażnika `_disposed`. Zmiana rejestracji na
  `AddSingleton<TechnicalLogWriter>()` dałaby `ObjectDisposedException` z bloku `finally` przy
  każdym normalnym zamknięciu. Należy dodać strażnik.
- **`Servanda.Domain.Tests` i `Servanda.Application.Tests` nie mają ani jednego testu** — to
  świadome rusztowanie pod v2, ale `dotnet test` raportuje je jako „żaden test nie jest zgodny
  z filtrem", co w CI łatwo pomylić z awarią odkrywania testów.
- **Brak `.editorconfig`** mimo reguły 10 w `AGENTS.md` wymagającej
  `dotnet format --verify-no-changes` — weryfikacja opiera się dziś wyłącznie na domyślnych
  regułach.
- **`.idea/` nie jest ignorowane** — potwierdzone przez `git check-ignore`. Katalog istnieje jako
  untracked i grozi przypadkowym commitem.

---

## Co jest zrobione dobrze

- Łańcuch bootstrapu (sekret 0600 → jednorazowy bilet → fragment URL zamiast query → wymiana na
  ciasteczko → `history.replaceState` czyszczące fragment) jest poprawny i przemyślany. Bilet
  nigdy nie trafia do logów serwera ani do historii przeglądarki.
- Przechowywanie biletów i sesji jako fingerprintów SHA-256 zamiast wartości surowych,
  `FixedTimeEquals` w `ControlSecret.Authenticate`, `ZeroMemory` w blokach `finally` —
  konsekwentna higiena kryptograficzna.
- Atomowa publikacja deskryptora przez `rename`, z weryfikacją uprawnień na pliku tymczasowym
  **przed** przeniesieniem (`AtomicInstanceDescriptorStore.cs:56-57`) — właściwa kolejność.
- `InstanceRuntimeState.MarkReady` z `Interlocked.CompareExchange` i walidacją loopback/HTTP,
  plus twarde 503 dopóki origin nie jest ustalony — brak okna, w którym host odpowiada bez
  kanonicznego originu.
- Odrzucanie dowiązań symbolicznych i wymuszanie 0700/0600 na wszystkich ścieżkach runtime,
  z izolowanymi katalogami XDG w trybie deweloperskim.
- Walidacja deskryptora po stronie czytelnika (`InstanceDescriptorReader.IsValidReadyDescriptor`)
  sprawdza nie tylko schemat, ale i to, że origin jest loopbackiem bez query i fragmentu.
  Traktowanie własnego pliku runtime jako niezaufanego wejścia to dobry odruch.

---

## Sugerowana kolejność

1. Punkty 1 i 3 — jedyne, które psują działanie u użytkownika końcowego.
2. Punkt 4 — decyzja o trybie renderowania, do podjęcia **przed** budową pulpitu P2, z ADR.
3. Punkty 2, 5, 6 — hartowanie i poprawki jakości.
4. Punkty 7 i 8 — sprzątanie, naturalnie łączy się z pracami nad systemem interfejsu w P2.
