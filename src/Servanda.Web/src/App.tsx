import React, { useState, useEffect, type FormEvent } from 'react';
import {
  fetchHealth,
  fetchNotes,
  createNote,
  reorderNotes,
  fetchCategories,
  reorderCategories,
  updateCategory,
  type Note,
  type Category,
} from './api';
import { CategorySidebar } from './CategorySidebar';
import { NoteTilesBoard } from './NoteTilesBoard';
import { SettingsModal } from './SettingsModal';
import { TopMenu } from './TopMenu';

export function App() {
  const [healthError, setHealthError] = useState<string | null>(null);
  const [notes, setNotes] = useState<Note[]>([]);
  const [notesLoading, setNotesLoading] = useState(true);
  const [notesError, setNotesError] = useState<string | null>(null);

  const [categories, setCategories] = useState<Category[]>([]);
  const [categoriesLoading, setCategoriesLoading] = useState(true);
  const [categoriesError, setCategoriesError] = useState<string | null>(null);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(null);

  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');

  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [formCategoryId, setFormCategoryId] = useState<string>('');
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  async function loadData() {
    try {
      setHealthError(null);
      await fetchHealth();
    } catch (err: any) {
      setHealthError(err.message || 'Brak połączenia z API');
    }

    try {
      setNotesError(null);
      setNotesLoading(true);
      const n = await fetchNotes();
      setNotes(n);
    } catch (err: any) {
      setNotesError(err.message || 'Błąd pobierania notatek');
    } finally {
      setNotesLoading(false);
    }

    try {
      setCategoriesError(null);
      setCategoriesLoading(true);
      const cats = await fetchCategories();
      setCategories(cats);
    } catch (err: any) {
      setCategoriesError(err.message || 'Błąd pobierania kategorii');
    } finally {
      setCategoriesLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, []);

  async function handleReorder(sourceIndex: number, targetIndex: number) {
    if (
      sourceIndex < 0 ||
      sourceIndex >= categories.length ||
      targetIndex < 0 ||
      targetIndex >= categories.length ||
      sourceIndex === targetIndex
    ) {
      return;
    }

    const reordered = [...categories];
    const [movedItem] = reordered.splice(sourceIndex, 1);
    reordered.splice(targetIndex, 0, movedItem);

    // Optimistic UI update
    setCategories(reordered);

    try {
      const updated = await reorderCategories(reordered.map((c) => c.id));
      setCategories(updated);
    } catch (err: any) {
      setCategoriesError(err.message || 'Błąd zapisu kolejności kategorii');
    }
  }

  async function handleNoteReorder(targetCategoryId: string | null, orderedNoteIds: string[]) {
    // Optimistic UI update
    setNotes((prevNotes) => {
      return prevNotes.map((note) => {
        const idx = orderedNoteIds.indexOf(note.id);
        if (idx !== -1) {
          return {
            ...note,
            categoryId: targetCategoryId,
            sortOrder: idx,
          };
        }
        return note;
      });
    });

    try {
      const updated = await reorderNotes(targetCategoryId, orderedNoteIds);
      setNotes(updated);
    } catch (err: any) {
      setNotesError(err.message || 'Błąd zapisu kolejności notatek');
    }
  }

  async function handleNoteDropOnCategory(noteId: string, categoryId: string) {
    const note = notes.find((n) => n.id === noteId);
    if (!note) return;

    // If note is already in that category, no reorder needed unless target order is specified
    const targetCategoryNotes = notes
      .filter((n) => n.categoryId === categoryId && n.id !== noteId)
      .sort((a, b) => a.sortOrder - b.sortOrder);

    const orderedIds = [...targetCategoryNotes.map((n) => n.id), noteId];
    await handleNoteReorder(categoryId, orderedIds);
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!title.trim()) return;

    setSubmitting(true);
    setSubmitError(null);

    try {
      const created = await createNote({
        title: title.trim(),
        content: content.trim(),
        categoryId: formCategoryId ? formCategoryId : null,
      });
      setNotes((prev) => [created, ...prev]);
      setTitle('');
      setContent('');
      setFormCategoryId('');
      setIsFormOpen(false);
    } catch (err: any) {
      setSubmitError(err.message || 'Nie udało się zapisać notatki.');
    } finally {
      setSubmitting(false);
    }
  }

  async function handleUpdateCategory(id: string, newName: string) {
    const updated = await updateCategory(id, { name: newName });
    setCategories((prev) => prev.map((c) => (c.id === id ? { ...c, name: updated.name } : c)));
  }

  const filteredNotes = notes.filter((n) => {
    if (!searchQuery.trim()) return true;
    const q = searchQuery.toLowerCase();
    return n.title.toLowerCase().includes(q) || n.content.toLowerCase().includes(q);
  });

  return (
    <div className="app-shell-root">
      <TopMenu
        categories={categories}
        selectedCategoryId={selectedCategoryId}
        onSelectCategory={setSelectedCategoryId}
        notesCount={notes.length}
        isFormOpen={isFormOpen}
        onToggleAddNote={() => setIsFormOpen((prev) => !prev)}
        onOpenSettings={() => setIsSettingsOpen(true)}
        healthError={healthError}
      />

      <div className="app-layout-root">
        <CategorySidebar
          categories={categories}
          selectedCategoryId={selectedCategoryId}
          onSelectCategory={setSelectedCategoryId}
          onReorder={handleReorder}
          onNoteDrop={handleNoteDropOnCategory}
          onOpenSettings={() => setIsSettingsOpen(true)}
          loading={categoriesLoading}
          error={categoriesError}
        />

        <div className="app-main-viewport">
          <div className="main-panel-frame">
            {/* Minimal Underline Search Bar */}
            <div className="main-panel-search-bar" role="search">
              <div className="search-bar-underline-container">
                <div className="main-search-input-wrapper">
                  <input
                    type="text"
                    className="main-search-input"
                    placeholder="Szukaj w notatkach..."
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    aria-label="Szukaj w notatkach"
                    data-testid="main-search-input"
                    autoComplete="off"
                  />
                  <div className="search-right-addon">
                    {searchQuery && (
                      <button
                        type="button"
                        className="main-search-clear-btn"
                        onClick={() => setSearchQuery('')}
                        title="Wyczyść wyszukiwanie"
                        aria-label="Wyczyść wyszukiwanie"
                      >
                        ✕
                      </button>
                    )}
                    <div className="search-icon-wrapper" aria-hidden="true">
                      <svg
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="2"
                        className="main-search-icon"
                      >
                        <circle cx="11" cy="11" r="8" />
                        <line x1="21" y1="21" x2="16.65" y2="16.65" />
                      </svg>
                    </div>
                  </div>
                </div>
              </div>

              {searchQuery && (
                <div className="main-search-status">
                  <span className="status-radar-dot" aria-hidden="true" />
                  <span>ZNALEZIONO:</span> <strong>{filteredNotes.length}</strong> {filteredNotes.length === 1 ? 'notatkę' : 'notatek'}
                </div>
              )}
            </div>

            <main className="main-content">
              {healthError && (
                <div className="alert-error" role="alert">
                  <strong>Błąd komunikacji z backendem:</strong> {healthError}
                </div>
              )}

              {isFormOpen && (
                <section className="section-card add-note-section">
                  <h2>Dodaj nową notatkę</h2>
                  {submitError && <div className="alert-error">{submitError}</div>}
                  <form onSubmit={handleSubmit}>
                    <div className="form-row-2col">
                      <div className="form-group">
                        <label htmlFor="note-title">Tytuł notatki</label>
                        <input
                          id="note-title"
                          className="form-input"
                          type="text"
                          placeholder="np. Nowy pomysł na projekt"
                          value={title}
                          onChange={(e) => setTitle(e.target.value)}
                          disabled={submitting}
                          required
                          autoFocus
                        />
                      </div>

                      <div className="form-group">
                        <label htmlFor="note-category">Kategoria / Podkategoria</label>
                        <select
                          id="note-category"
                          className="form-select"
                          value={formCategoryId}
                          onChange={(e) => setFormCategoryId(e.target.value)}
                          disabled={submitting || categoriesLoading}
                        >
                          <option value="">-- Bez kategorii --</option>
                          {categories
                            .filter((c) => !c.parentCategoryId)
                            .sort((a, b) => a.sortOrder - b.sortOrder)
                            .map((parent) => {
                              const subs = categories
                                .filter((c) => c.parentCategoryId === parent.id)
                                .sort((a, b) => a.sortOrder - b.sortOrder);
                              return (
                                <React.Fragment key={parent.id}>
                                  <option value={parent.id}>{parent.name}</option>
                                  {subs.map((sub) => (
                                    <option key={sub.id} value={sub.id}>
                                      &nbsp;&nbsp;↳ {sub.name}
                                    </option>
                                  ))}
                                </React.Fragment>
                              );
                            })}
                        </select>
                      </div>
                    </div>

                    <div className="form-group">
                      <label htmlFor="note-content">Treść notatki</label>
                      <textarea
                        id="note-content"
                        className="form-textarea"
                        rows={3}
                        placeholder="Wpisz treść notatki..."
                        value={content}
                        onChange={(e) => setContent(e.target.value)}
                        disabled={submitting}
                      />
                    </div>

                    <div className="form-actions">
                      <button type="submit" className="btn-primary" disabled={submitting || !title.trim()}>
                        {submitting ? 'Zapisywanie w SQLite...' : 'Zapisz notatkę'}
                      </button>
                      <button
                        type="button"
                        className="btn-secondary"
                        onClick={() => setIsFormOpen(false)}
                        disabled={submitting}
                      >
                        Anuluj
                      </button>
                    </div>
                  </form>
                </section>
              )}

              {/* Główny widok kafli pogrupowanych w kategorie i podkategorie */}
              <NoteTilesBoard
                categories={categories}
                notes={filteredNotes}
                selectedCategoryId={selectedCategoryId}
                onSelectCategory={setSelectedCategoryId}
                onReorderNotes={handleNoteReorder}
                loading={notesLoading || categoriesLoading}
                error={notesError || categoriesError}
              />
            </main>
          </div>
        </div>
      </div>

      <SettingsModal
        isOpen={isSettingsOpen}
        onClose={() => setIsSettingsOpen(false)}
        categories={categories}
        onUpdateCategory={handleUpdateCategory}
        onReorderCategories={handleReorder}
      />
    </div>
  );
}

export default App;
