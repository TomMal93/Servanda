# Servanda

Prywatna, lokalna pamięć zewnętrzna dla ważnych spraw codzienności. Projekt zakończył etap kompletowania specyfikacji D0 i rozpoczął etap P1, obejmujący budowę fundamentu hosta v1.

## Aktualny stan

- Dokumentacja w `docs/` jest źródłem wymagań nowego projektu, a etap D0 specyfikacji jest ukończony.
- Aktywnym etapem jest P1: fundament bezpiecznego, bezstanowego hosta v1 i uruchomienia na Linuksie.
- Docelowy stos to ASP.NET Core, Blazor Web App Interactive Server, EF Core i SQLite.
- Linux jest podstawową platformą.
- Pierwsze wydanie jest przeznaczone wyłącznie na komputery osobiste i laptopy; telefony i tablety są poza zakresem.
- Wydanie użytkowe będzie uruchamiane z menu aplikacji przez launcher i skrót `.desktop`.
- V1 jest bezstanową powłoką ze statycznym pulpitem planowanych obszarów. Moduły, SQLite, migracje, kopie, recovery, import i eksport rozpoczynają się w v2.

Kolejność prac definiuje [roadmapa](./docs/product/roadmap.md), a granice pierwszego wydania — [bieżący zakres](./docs/product/current-scope.md).

## Dokumentacja

Punkt wejścia, routing i źródła prawdy: [docs/README.md](./docs/README.md).

Najważniejsze dokumenty:

- [wizja produktu](./docs/product/product-vision.md),
- [obszary i ekran główny](./docs/product/features/areas-dashboard.md),
- [model danych](./docs/engineering/data-model.md),
- [architektura](./docs/engineering/technical-decisions.md),
- [uruchomienie na Linuksie](./docs/engineering/linux-deployment.md),
- [wymagania jakościowe](./docs/engineering/quality-requirements.md).

## Przyszłe uruchomienie deweloperskie

Po utworzeniu rozwiązania .NET podstawowy przepływ będzie miał postać:

```bash
dotnet restore
dotnet run --project src/Servanda.App
```

Polecenia są kontraktem planowanego bootstrapu. Do czasu powstania wskazanego projektu nie są jeszcze wykonywalne. README MUSI zostać zaktualizowane przy utworzeniu rozwiązania.

## Ochrona danych

Przyszłe bazy, kopie i eksporty są danymi użytkownika. Ich usunięcie wymaga osobnego, jawnego zadania i mechanizmu ochronnego zgodnego z dokumentacją.

Instrukcje dla agentów: [AGENTS.md](./AGENTS.md).
