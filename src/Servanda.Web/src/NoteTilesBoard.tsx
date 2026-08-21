import React from 'react';
import type { Category, Note } from './api';

interface NoteTilesBoardProps {
  categories: Category[];
  notes: Note[];
  selectedCategoryId: string | null;
  onSelectCategory: (categoryId: string | null) => void;
  loading?: boolean;
  error?: string | null;
}

export function getPreviewSnippet(content: string, maxWords = 15): string {
  if (!content || !content.trim()) {
    return 'Brak treści notatki.';
  }
  const words = content.trim().split(/\s+/);
  if (words.length <= maxWords) {
    return words.join(' ');
  }
  return words.slice(0, maxWords).join(' ') + '...';
}

export function getCategoryColor(category: Category, index = 0): string {
  if (category.color && category.color.trim()) {
    return category.color.trim();
  }
  const normalized = category.name.toLowerCase().trim();
  if (normalized.includes('prompt')) {
    return '#a855f7';
  }
  if (normalized.includes('kod') || normalized.includes('program')) {
    return '#ec4899';
  }
  if (normalized.includes('analiz') || normalized.includes('dane')) {
    return '#8b5cf6';
  }
  if (normalized.includes('notatk') || normalized.includes('note')) {
    return '#38bdf8';
  }
  if (normalized.includes('prac') || normalized.includes('work')) {
    return '#06b6d4';
  }
  if (normalized.includes('osobist') || normalized.includes('personal')) {
    return '#14b8a6';
  }
  if (normalized.includes('rodzin') || normalized.includes('family')) {
    return '#f59e0b';
  }
  if (normalized.includes('narzędzi') || normalized.includes('narzedzi') || normalized.includes('tool')) {
    return '#10b981';
  }

  const fallbackPalette = ['#10b981', '#ec4899', '#06b6d4', '#8b5cf6', '#f97316', '#14b8a6', '#6366f1', '#3b82f6'];
  return fallbackPalette[index % fallbackPalette.length];
}

export const NoteTilesBoard: React.FC<NoteTilesBoardProps> = ({
  categories,
  notes,
  selectedCategoryId,
  onSelectCategory,
  loading,
  error,
}) => {
  if (loading) {
    return (
      <div className="board-loading-container" aria-live="polite">
        <p className="empty-state">Ładowanie widoku głównego...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="alert-error" role="alert">
        <strong>Błąd:</strong> {error}
      </div>
    );
  }

  // Top-level categories (no parentCategoryId)
  const rootCategories = categories
    .filter((c) => !c.parentCategoryId)
    .sort((a, b) => a.sortOrder - b.sortOrder);

  // Map of subcategories grouped by parentCategoryId
  const subcategoriesMap = new Map<string, Category[]>();
  categories.forEach((cat) => {
    if (cat.parentCategoryId) {
      const list = subcategoriesMap.get(cat.parentCategoryId) || [];
      list.push(cat);
      subcategoriesMap.set(cat.parentCategoryId, list);
    }
  });

  // Sort subcategories by sortOrder
  subcategoriesMap.forEach((subs) => {
    subs.sort((a, b) => a.sortOrder - b.sortOrder);
  });

  // Find uncategorized notes (notes where categoryId is null or not in category list)
  const allCategoryIds = new Set(categories.map((c) => c.id));
  const uncategorizedNotes = notes.filter(
    (n) => !n.categoryId || !allCategoryIds.has(n.categoryId)
  );

  // Filter root categories if a category is selected
  let visibleRootCategories = rootCategories;
  let singleSelectedSubcategory: Category | null = null;

  if (selectedCategoryId) {
    const selected = categories.find((c) => c.id === selectedCategoryId);
    if (selected) {
      if (selected.parentCategoryId) {
        // A subcategory is selected
        singleSelectedSubcategory = selected;
        visibleRootCategories = rootCategories.filter((r) => r.id === selected.parentCategoryId);
      } else {
        // A root category is selected
        visibleRootCategories = rootCategories.filter((r) => r.id === selected.id);
      }
    }
  }

  const selectedCategoryObj = categories.find((c) => c.id === selectedCategoryId);

  return (
    <div className="note-tiles-board" data-testid="note-tiles-board">
      {selectedCategoryObj && (
        <div className="board-active-filter-bar">
          <span>
            Wyświetlanie: <strong>{selectedCategoryObj.name}</strong>
          </span>
          <button
            type="button"
            className="btn-clear-filter-badge"
            onClick={() => onSelectCategory(null)}
            title="Pokaż wszystkie kategorie"
          >
            Pokaż wszystkie kategorie ✕
          </button>
        </div>
      )}

      {visibleRootCategories.length === 0 && uncategorizedNotes.length === 0 && (
        <p className="empty-state">Brak kategorii i notatek do wyświetlenia.</p>
      )}

      {visibleRootCategories.map((rootCat, rootIdx) => {
        const rootColor = getCategoryColor(rootCat, rootIdx);
        const rootNotes = notes.filter((n) => n.categoryId === rootCat.id);
        const subcategories = subcategoriesMap.get(rootCat.id) || [];
        const visibleSubcategories = singleSelectedSubcategory
          ? subcategories.filter((s) => s.id === singleSelectedSubcategory?.id)
          : subcategories;

        const totalNotesInHierarchy =
          rootNotes.length +
          subcategories.reduce(
            (acc, sub) => acc + notes.filter((n) => n.categoryId === sub.id).length,
            0
          );

        // If a subcategory is selected, we only show that subcategory's notes
        const showRootDirectNotes = !singleSelectedSubcategory;

        return (
          <section
            key={rootCat.id}
            className="category-section"
            style={{ '--cat-color': rootColor } as React.CSSProperties}
            data-testid={`category-section-${rootCat.id}`}
          >
            {/* Category Header with colored line underneath */}
            <header className="category-header-group">
              <div className="category-header-title-row">
                <h2 className="category-heading">{rootCat.name}</h2>
                <span className="category-count-badge">
                  {totalNotesInHierarchy} {totalNotesInHierarchy === 1 ? 'notatka' : 'notatek'}
                </span>
              </div>
              <div className="category-colored-line" aria-hidden="true" />
            </header>

            {/* Direct notes belonging to this root category */}
            {showRootDirectNotes && rootNotes.length > 0 && (
              <div className="note-tiles-grid" data-testid={`notes-grid-${rootCat.id}`}>
                {rootNotes.map((note) => (
                  <article
                    key={note.id}
                    className="note-tile-card"
                    style={{ '--tile-border-color': rootColor } as React.CSSProperties}
                    data-testid={`note-tile-${note.id}`}
                  >
                    <h4 className="note-tile-title">{note.title}</h4>
                    <p className="note-tile-snippet">{getPreviewSnippet(note.content)}</p>
                    <footer className="note-tile-meta">
                      <span>{new Date(note.createdAt).toLocaleDateString('pl-PL')}</span>
                    </footer>
                  </article>
                ))}
              </div>
            )}

            {/* Subcategories */}
            {visibleSubcategories.length > 0 && (
              <div className="subcategories-container">
                {visibleSubcategories.map((subCat, subIdx) => {
                  const subColor = getCategoryColor(subCat, subIdx + 5);
                  const subNotes = notes.filter((n) => n.categoryId === subCat.id);

                  return (
                    <div
                      key={subCat.id}
                      className="subcategory-section"
                      style={{ '--subcat-color': subColor } as React.CSSProperties}
                      data-testid={`subcategory-section-${subCat.id}`}
                    >
                      {/* Subcategory Header with smaller font and colored line underneath */}
                      <header className="subcategory-header-group">
                        <div className="subcategory-title-row">
                          <h3 className="subcategory-heading">{subCat.name}</h3>
                          <span className="subcategory-count-badge">
                            {subNotes.length} {subNotes.length === 1 ? 'notatka' : 'notatek'}
                          </span>
                        </div>
                        <div className="subcategory-colored-line" aria-hidden="true" />
                      </header>

                      {/* Subcategory notes grid with frames matching the subcategory's color */}
                      {subNotes.length > 0 ? (
                        <div className="note-tiles-grid" data-testid={`notes-grid-${subCat.id}`}>
                          {subNotes.map((note) => (
                            <article
                              key={note.id}
                              className="note-tile-card"
                              style={{ '--tile-border-color': subColor } as React.CSSProperties}
                              data-testid={`note-tile-${note.id}`}
                            >
                              <h4 className="note-tile-title">{note.title}</h4>
                              <p className="note-tile-snippet">{getPreviewSnippet(note.content)}</p>
                              <footer className="note-tile-meta">
                                <span>{new Date(note.createdAt).toLocaleDateString('pl-PL')}</span>
                              </footer>
                            </article>
                          ))}
                        </div>
                      ) : (
                        <p className="subcategory-empty-notice">Brak notatek w podkategorii {subCat.name}.</p>
                      )}
                    </div>
                  );
                })}
              </div>
            )}

            {/* If whole category and subcategories have 0 notes */}
            {totalNotesInHierarchy === 0 && (
              <p className="category-empty-notice">Brak notatek w kategorii {rootCat.name}.</p>
            )}
          </section>
        );
      })}

      {/* Uncategorized notes section */}
      {!selectedCategoryId && uncategorizedNotes.length > 0 && (
        <section
          className="category-section uncategorized-section"
          style={{ '--cat-color': '#64748b' } as React.CSSProperties}
          data-testid="category-section-uncategorized"
        >
          <header className="category-header-group">
            <div className="category-header-title-row">
              <h2 className="category-heading">Bez kategorii</h2>
              <span className="category-count-badge">
                {uncategorizedNotes.length} {uncategorizedNotes.length === 1 ? 'notatka' : 'notatek'}
              </span>
            </div>
            <div className="category-colored-line" aria-hidden="true" />
          </header>

          <div className="note-tiles-grid" data-testid="notes-grid-uncategorized">
            {uncategorizedNotes.map((note) => (
              <article
                key={note.id}
                className="note-tile-card"
                style={{ '--tile-border-color': '#64748b' } as React.CSSProperties}
                data-testid={`note-tile-${note.id}`}
              >
                <h4 className="note-tile-title">{note.title}</h4>
                <p className="note-tile-snippet">{getPreviewSnippet(note.content)}</p>
                <footer className="note-tile-meta">
                  <span>{new Date(note.createdAt).toLocaleDateString('pl-PL')}</span>
                </footer>
              </article>
            ))}
          </div>
        </section>
      )}
    </div>
  );
};

export default NoteTilesBoard;
