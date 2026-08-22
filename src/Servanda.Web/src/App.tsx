import { useState, useEffect } from 'react';
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
import { CreateNoteModal } from './CreateNoteModal';
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
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');

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

  async function handleCreateNote(data: { title: string; content: string; categoryId: string | null }) {
    const created = await createNote(data);
    setNotes((prev) => [created, ...prev]);
    return created;
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
        notes={notes}
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

      <CreateNoteModal
        isOpen={isFormOpen}
        onClose={() => setIsFormOpen(false)}
        categories={categories}
        selectedCategoryId={selectedCategoryId}
        onCreateNote={handleCreateNote}
        categoriesLoading={categoriesLoading}
      />

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
