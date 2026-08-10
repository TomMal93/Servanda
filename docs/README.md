# Dokumentacja Servandy

Dokumentacja rozdziela cel produktu, zachowanie funkcji, reguły wizualne i decyzje techniczne. Ma pomagać ludziom oraz agentom czytać wyłącznie kontekst potrzebny do zadania.

## Struktura

```text
docs/
├── README.md
├── product/
│   ├── product-vision.md
│   ├── current-scope.md
│   ├── roadmap.md
│   └── features/
│       ├── areas-dashboard.md
│       ├── tool-catalog.md
│       ├── prompt-library.md
│       ├── search.md
│       └── content-editors.md
├── design/
│   └── ui-system.md
└── engineering/
    ├── data-model.md
    ├── linux-deployment.md
    ├── security-model.md
    ├── technical-decisions.md
    ├── quality-requirements.md
    ├── code-conventions.md
    ├── schemas/
    │   └── servanda-export-v1.schema.json
    └── adr/
        ├── 0001-architektura-lokalnej-aplikacji-linux.md
        ├── 0002-import-zastepujacy-kolekcje.md
        ├── 0003-wyszukiwanie-fts5.md
        ├── 0004-agregaty-rewizje-i-kolejnosc.md
        ├── 0005-lokalna-sesja-launchera.md
        ├── 0006-bezstanowy-zakres-v1.md
        └── 0007-dystrybucja-linux-v1.md
```

## Routing zadań

| Zadanie | Przeczytaj |
|---|---|
| zrozumienie celu i odbiorcy | [product-vision.md](product/product-vision.md) |
| ocena, czy funkcja należy do bieżącego etapu | [current-scope.md](product/current-scope.md) |
| wybór kolejnego priorytetu | [roadmap.md](product/roadmap.md) |
| ekran główny, obszary lub nawigacja między nimi | [areas-dashboard.md](product/features/areas-dashboard.md) |
| katalog, kategorie lub wyszukiwanie narzędzi od v2 | [tool-catalog.md](product/features/tool-catalog.md) |
| prompty, zmienne, wersje lub Prompt Studio od v2 | [prompt-library.md](product/features/prompt-library.md) |
| wspólna semantyka wyszukiwania, ranking lub stronicowanie wyników od v2 | [search.md](product/features/search.md) |
| tworzenie, edycja, usuwanie, kolejność lub reset od v2 | [content-editors.md](product/features/content-editors.md) |
| wygląd, komponenty lub responsywność | [ui-system.md](design/ui-system.md) |
| SQLite, encje, relacje, eksport lub integralność danych od v2 | [data-model.md](engineering/data-model.md) |
| uruchomienie, XDG, launcher lub publikacja Linuksa | [linux-deployment.md](engineering/linux-deployment.md) |
| threat model, lokalna sesja, Host, Origin, CSP lub dane przeglądarki | [security-model.md](engineering/security-model.md) |
| runtime, zapis, hosting lub decyzje otwarte | [technical-decisions.md](engineering/technical-decisions.md) |
| testy, dostępność lub wydajność | [quality-requirements.md](engineering/quality-requirements.md) |
| organizacja kodu, C#, Razor, CSS i JavaScript interop | [code-conventions.md](engineering/code-conventions.md) |

## Źródła prawdy

| Obszar | Źródło prawdy |
|---|---|
| cel i zasady produktu | `product/product-vision.md` |
| granice bieżącego etapu | `product/current-scope.md` |
| kolejność rozwoju | `product/roadmap.md` |
| zachowanie funkcji | właściwy plik w `product/features/` |
| wspólne reguły wizualne | `design/ui-system.md` |
| format i integralność danych | `engineering/data-model.md` |
| uruchomienie i publikacja na Linuksie | `engineering/linux-deployment.md` |
| bezpieczeństwo lokalnego hosta i przeglądarki | `engineering/security-model.md` |
| decyzje techniczne i kwestie otwarte | `engineering/technical-decisions.md` |
| zaakceptowana decyzja trwała | właściwy ADR wskazany przez rejestr decyzji |
| wymagania przekrojowe | `engineering/quality-requirements.md` |
| organizacja implementacji | `engineering/code-conventions.md` |
| szczegóły faktycznie zaimplementowane | kod, dane i testy |

Inny dokument może streszczać kontrakt, ale nie może dodawać konkurencyjnych reguł. W razie rozbieżności popraw właściwe źródło prawdy zamiast wybierać wygodniejszą wersję.

## Statusy

- `obowiązujący` — zaakceptowany kontrakt bieżącego etapu;
- `roboczy` — propozycja lub obszar z otwartymi decyzjami;
- `wycofany` — materiał historyczny, którego nie należy wdrażać.

Słowa **MUSI**, **POWINIEN** i **MOŻE** oznaczają odpowiednio wymaganie, mocną rekomendację i dozwolone zachowanie.

## Stan projektu

Dokumentacja opisuje nową aplikację budowaną od zera. W repozytorium nie ma wcześniejszej implementacji ani danych do migracji. Do czasu utworzenia rozwiązania .NET ścieżki i polecenia docelowe są kontraktem bootstrapu, a nie deklaracją istnienia implementacji.

## Utrzymanie

Dokumenty opisują stan i kontrakty, a nie historię drobnych zmian. Przy zmianie funkcji aktualizuj jej specyfikację; przy zmianie formatu danych — model; przy zmianie priorytetów — roadmapę. Decyzję trudną do odwrócenia po uzgodnieniu przenieś do ADR.
