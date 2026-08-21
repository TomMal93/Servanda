import React, { useState } from 'react';
import type { Category, Note } from './api';

interface NoteTilesBoardProps {
  categories: Category[];
  notes: Note[];
  selectedCategoryId: string | null;
  onSelectCategory: (categoryId: string | null) => void;
  onReorderNotes?: (targetCategoryId: string | null, orderedNoteIds: string[]) => void;
  onNoteDragStart?: (noteId: string) => void;
  onNoteDragEnd?: () => void;
  loading?: boolean;
  error?: string | null;
}

interface NoteDropTarget {
  noteId: string;
  position: 'before' | 'after';
  categoryId: string | null;
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
  onReorderNotes,
  onNoteDragStart,
  onNoteDragEnd,
  loading,
  error,
}) => {
  const [draggedNoteId, setDraggedNoteId] = useState<string | null>(null);
  const [noteDropTarget, setNoteDropTarget] = useState<NoteDropTarget | null>(null);
  const [activeCategoryDropTarget, setActiveCategoryDropTarget] = useState<string | null | 'uncategorized'>(null);

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
  const uncategorizedNotes = notes
    .filter((n) => !n.categoryId || !allCategoryIds.has(n.categoryId))
    .sort((a, b) => a.sortOrder - b.sortOrder);

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

  function handleNoteDragStart(e: React.DragEvent, noteId: string) {
    setDraggedNoteId(noteId);
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', noteId);
    e.dataTransfer.setData('application/x-servanda-note', noteId);
    onNoteDragStart?.(noteId);
  }

  function handleNoteDragOver(e: React.DragEvent, targetNote: Note, targetCategoryId: string | null) {
    e.preventDefault();
    e.stopPropagation();

    if (!draggedNoteId || draggedNoteId === targetNote.id) {
      if (noteDropTarget !== null) setNoteDropTarget(null);
      return;
    }

    e.dataTransfer.dropEffect = 'move';

    const rect = e.currentTarget.getBoundingClientRect();
    const ratioX = (e.clientX - rect.left) / rect.width;
    const position: 'before' | 'after' = ratioX < 0.5 ? 'before' : 'after';

    if (
      noteDropTarget?.noteId !== targetNote.id ||
      noteDropTarget?.position !== position ||
      noteDropTarget?.categoryId !== targetCategoryId
    ) {
      setNoteDropTarget({
        noteId: targetNote.id,
        position,
        categoryId: targetCategoryId,
      });
    }
  }

  function handleNoteDrop(e: React.DragEvent, targetNote: Note, targetCategoryId: string | null) {
    e.preventDefault();
    e.stopPropagation();

    const noteIdToMove = draggedNoteId || e.dataTransfer.getData('application/x-servanda-note') || e.dataTransfer.getData('text/plain');
    if (!noteIdToMove || !onReorderNotes) {
      setDraggedNoteId(null);
      setNoteDropTarget(null);
      setActiveCategoryDropTarget(null);
      return;
    }

    // Get current notes in the target category
    const targetCategoryNotes = notes
      .filter((n) => {
        if (targetCategoryId === null) {
          return !n.categoryId || !allCategoryIds.has(n.categoryId);
        }
        return n.categoryId === targetCategoryId;
      })
      .sort((a, b) => a.sortOrder - b.sortOrder);

    // List of note IDs in target category without the dragged note
    const cleanList = targetCategoryNotes.filter((n) => n.id !== noteIdToMove).map((n) => n.id);
    const targetIdx = cleanList.indexOf(targetNote.id);

    const position = noteDropTarget?.position || 'after';
    const insertIdx = targetIdx === -1 ? cleanList.length : position === 'after' ? targetIdx + 1 : targetIdx;

    cleanList.splice(insertIdx, 0, noteIdToMove);

    onReorderNotes(targetCategoryId, cleanList);

    setDraggedNoteId(null);
    setNoteDropTarget(null);
    setActiveCategoryDropTarget(null);
    onNoteDragEnd?.();
  }

  function handleCategorySectionDragOver(e: React.DragEvent, categoryId: string | null) {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
    const catKey = categoryId === null ? 'uncategorized' : categoryId;
    if (activeCategoryDropTarget !== catKey) {
      setActiveCategoryDropTarget(catKey);
    }
  }

  function handleCategorySectionDragLeave(e: React.DragEvent) {
    const related = e.relatedTarget as Node | null;
    if (e.currentTarget.contains(related)) return;
    setActiveCategoryDropTarget(null);
  }

  function handleCategorySectionDrop(e: React.DragEvent, categoryId: string | null) {
    e.preventDefault();
    const noteIdToMove = draggedNoteId || e.dataTransfer.getData('application/x-servanda-note') || e.dataTransfer.getData('text/plain');
    if (!noteIdToMove || !onReorderNotes) {
      setDraggedNoteId(null);
      setNoteDropTarget(null);
      setActiveCategoryDropTarget(null);
      return;
    }

    const targetCategoryNotes = notes
      .filter((n) => {
        if (categoryId === null) {
          return !n.categoryId || !allCategoryIds.has(n.categoryId);
        }
        return n.categoryId === categoryId;
      })
      .sort((a, b) => a.sortOrder - b.sortOrder);

    const cleanList = targetCategoryNotes.filter((n) => n.id !== noteIdToMove).map((n) => n.id);
    cleanList.push(noteIdToMove);

    onReorderNotes(categoryId, cleanList);

    setDraggedNoteId(null);
    setNoteDropTarget(null);
    setActiveCategoryDropTarget(null);
    onNoteDragEnd?.();
  }

  function handleDragEnd() {
    setDraggedNoteId(null);
    setNoteDropTarget(null);
    setActiveCategoryDropTarget(null);
    onNoteDragEnd?.();
  }

  function renderNoteCard(note: Note, categoryColor: string, categoryId: string | null) {
    const isDragging = draggedNoteId === note.id;
    const isTarget = noteDropTarget?.noteId === note.id;
    const showBefore = isTarget && noteDropTarget?.position === 'before';
    const showAfter = isTarget && noteDropTarget?.position === 'after';

    return (
      <div key={note.id} className="note-tile-wrapper">
        {showBefore && (
          <div className="note-drop-indicator-vertical note-indicator-left" aria-hidden="true" />
        )}
        <article
          className={`note-tile-card ${isDragging ? 'dragging' : ''} ${isTarget ? 'drop-target-active' : ''}`}
          style={{ '--tile-border-color': categoryColor } as React.CSSProperties}
          draggable
          onDragStart={(e) => handleNoteDragStart(e, note.id)}
          onDragOver={(e) => handleNoteDragOver(e, note, categoryId)}
          onDrop={(e) => handleNoteDrop(e, note, categoryId)}
          onDragEnd={handleDragEnd}
          data-testid={`note-tile-${note.id}`}
        >
          <div className="note-tile-header">
            <h4 className="note-tile-title">{note.title}</h4>
            <div className="note-drag-handle" title="Przeciągnij notatkę" aria-hidden="true">
              <svg viewBox="0 0 24 24" fill="currentColor" className="drag-icon">
                <circle cx="9" cy="6" r="1.5" />
                <circle cx="15" cy="6" r="1.5" />
                <circle cx="9" cy="12" r="1.5" />
                <circle cx="15" cy="12" r="1.5" />
                <circle cx="9" cy="18" r="1.5" />
                <circle cx="15" cy="18" r="1.5" />
              </svg>
            </div>
          </div>
          <p className="note-tile-snippet">{getPreviewSnippet(note.content)}</p>
          <footer className="note-tile-meta">
            <span>{new Date(note.createdAt).toLocaleDateString('pl-PL')}</span>
          </footer>
        </article>
        {showAfter && (
          <div className="note-drop-indicator-vertical note-indicator-right" aria-hidden="true" />
        )}
      </div>
    );
  }

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
        const rootNotes = notes
          .filter((n) => n.categoryId === rootCat.id)
          .sort((a, b) => a.sortOrder - b.sortOrder);
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
        const isCatActiveDrop = activeCategoryDropTarget === rootCat.id;

        return (
          <section
            key={rootCat.id}
            className={`category-section ${isCatActiveDrop ? 'category-drop-active' : ''}`}
            style={{ '--cat-color': rootColor } as React.CSSProperties}
            data-testid={`category-section-${rootCat.id}`}
            onDragOver={(e) => handleCategorySectionDragOver(e, rootCat.id)}
            onDragLeave={handleCategorySectionDragLeave}
            onDrop={(e) => handleCategorySectionDrop(e, rootCat.id)}
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
            {showRootDirectNotes && (
              <>
                {rootNotes.length > 0 ? (
                  <div className="note-tiles-grid" data-testid={`notes-grid-${rootCat.id}`}>
                    {rootNotes.map((note) => renderNoteCard(note, rootColor, rootCat.id))}
                  </div>
                ) : (
                  <div
                    className="category-empty-dropzone"
                    data-testid={`empty-dropzone-${rootCat.id}`}
                    onDragOver={(e) => handleCategorySectionDragOver(e, rootCat.id)}
                    onDrop={(e) => handleCategorySectionDrop(e, rootCat.id)}
                  >
                    <p className="category-empty-notice">
                      Brak notatek w kategorii {rootCat.name}. Przeciągnij tutaj notatkę, aby ją przypisać.
                    </p>
                  </div>
                )}
              </>
            )}

            {/* Subcategories */}
            {visibleSubcategories.length > 0 && (
              <div className="subcategories-container">
                {visibleSubcategories.map((subCat, subIdx) => {
                  const subColor = getCategoryColor(subCat, subIdx + 5);
                  const subNotes = notes
                    .filter((n) => n.categoryId === subCat.id)
                    .sort((a, b) => a.sortOrder - b.sortOrder);
                  const isSubActiveDrop = activeCategoryDropTarget === subCat.id;

                  return (
                    <div
                      key={subCat.id}
                      className={`subcategory-section ${isSubActiveDrop ? 'subcategory-drop-active' : ''}`}
                      style={{ '--subcat-color': subColor } as React.CSSProperties}
                      data-testid={`subcategory-section-${subCat.id}`}
                      onDragOver={(e) => handleCategorySectionDragOver(e, subCat.id)}
                      onDragLeave={handleCategorySectionDragLeave}
                      onDrop={(e) => handleCategorySectionDrop(e, subCat.id)}
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

                      {/* Subcategory notes grid */}
                      {subNotes.length > 0 ? (
                        <div className="note-tiles-grid" data-testid={`notes-grid-${subCat.id}`}>
                          {subNotes.map((note) => renderNoteCard(note, subColor, subCat.id))}
                        </div>
                      ) : (
                        <div
                          className="subcategory-empty-dropzone"
                          data-testid={`empty-dropzone-${subCat.id}`}
                          onDragOver={(e) => handleCategorySectionDragOver(e, subCat.id)}
                          onDrop={(e) => handleCategorySectionDrop(e, subCat.id)}
                        >
                          <p className="subcategory-empty-notice">
                            Brak notatek w podkategorii {subCat.name}. Przeciągnij tutaj notatkę, aby ją przypisać.
                          </p>
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </section>
        );
      })}

      {/* Uncategorized notes section */}
      {!selectedCategoryId && (
        <section
          className={`category-section uncategorized-section ${
            activeCategoryDropTarget === 'uncategorized' ? 'category-drop-active' : ''
          }`}
          style={{ '--cat-color': '#64748b' } as React.CSSProperties}
          data-testid="category-section-uncategorized"
          onDragOver={(e) => handleCategorySectionDragOver(e, null)}
          onDragLeave={handleCategorySectionDragLeave}
          onDrop={(e) => handleCategorySectionDrop(e, null)}
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

          {uncategorizedNotes.length > 0 ? (
            <div className="note-tiles-grid" data-testid="notes-grid-uncategorized">
              {uncategorizedNotes.map((note) => renderNoteCard(note, '#64748b', null))}
            </div>
          ) : (
            <div
              className="category-empty-dropzone"
              data-testid="empty-dropzone-uncategorized"
              onDragOver={(e) => handleCategorySectionDragOver(e, null)}
              onDrop={(e) => handleCategorySectionDrop(e, null)}
            >
              <p className="category-empty-notice">
                Brak notatek bez kategorii. Przeciągnij tutaj notatkę, aby usunąć jej przypisanie do kategorii.
              </p>
            </div>
          )}
        </section>
      )}
    </div>
  );
};

export default NoteTilesBoard;
