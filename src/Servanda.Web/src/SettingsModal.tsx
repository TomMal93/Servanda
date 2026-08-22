import React, { useState, useEffect, useRef, useLayoutEffect } from 'react';
import type { Category } from './api';

export type SettingsView = 'menu' | 'categories' | 'note-tile' | 'backup';

interface DropTarget {
  index: number;
  position: 'before' | 'after';
}

interface SettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
  categories?: Category[];
  onUpdateCategory?: (id: string, newName: string) => Promise<void>;
  onReorderCategories?: (sourceIndex: number, targetIndex: number) => void;
}

function getDestinationIndex(source: number, target: number, position: 'before' | 'after'): number {
  let dest = position === 'after' ? target + 1 : target;
  if (source < dest) {
    dest -= 1;
  }
  return dest;
}

export const SettingsModal: React.FC<SettingsModalProps> = ({
  isOpen,
  onClose,
  categories = [],
  onUpdateCategory,
  onReorderCategories,
}) => {
  const [activeView, setActiveView] = useState<SettingsView>('menu');

  // Inline editing state for category name
  const [editingCategoryId, setEditingCategoryId] = useState<string | null>(null);
  const [editingName, setEditingName] = useState('');
  const [isSavingCategory, setIsSavingCategory] = useState(false);
  const [categoryEditError, setCategoryEditError] = useState<string | null>(null);

  // Drag and drop state for category reordering
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);
  const [dropTarget, setDropTarget] = useState<DropTarget | null>(null);

  // Backup configuration state (preview / coming soon)
  const [backupScreenshots, setBackupScreenshots] = useState(true);
  const [screenshotFolderStructure, setScreenshotFolderStructure] = useState<'hierarchical' | 'flat' | 'date_hierarchical'>('hierarchical');
  const [screenshotHighRes, setScreenshotHighRes] = useState(true);
  const [screenshotIncludeMeta, setScreenshotIncludeMeta] = useState(true);
  const [backupMarkdown, setBackupMarkdown] = useState(true);
  const [backupSqlite, setBackupSqlite] = useState(true);
  const [backupZip, setBackupZip] = useState(true);
  const [backupSchedule, setBackupSchedule] = useState<'on_close' | 'daily' | 'manual'>('on_close');

  // References for FLIP animation during reorder
  const itemRefs = useRef<Map<string, HTMLDivElement>>(new Map());
  const prevPositions = useRef<Map<string, number>>(new Map());

  // Flatten categories in hierarchical order: root categories followed by their subcategories
  const hierarchicalCategories: { category: Category; originalIndex: number; isSubcategory: boolean }[] = [];
  const rootCategories = categories
    .map((cat, idx) => ({ category: cat, originalIndex: idx }))
    .filter(({ category }) => !category.parentCategoryId)
    .sort((a, b) => a.category.sortOrder - b.category.sortOrder);

  rootCategories.forEach((root) => {
    hierarchicalCategories.push({ ...root, isSubcategory: false });
    const subcats = categories
      .map((cat, idx) => ({ category: cat, originalIndex: idx }))
      .filter(({ category }) => category.parentCategoryId === root.category.id)
      .sort((a, b) => a.category.sortOrder - b.category.sortOrder);

    subcats.forEach((sub) => {
      hierarchicalCategories.push({ ...sub, isSubcategory: true });
    });
  });

  useLayoutEffect(() => {
    if (activeView !== 'categories') return;
    const prevPosMap = prevPositions.current;
    categories.forEach((cat) => {
      const el = itemRefs.current.get(cat.id);
      if (el) {
        const currentTop = el.getBoundingClientRect().top;
        const oldTop = prevPosMap.get(cat.id);
        if (oldTop !== undefined && oldTop !== currentTop) {
          const deltaY = oldTop - currentTop;
          el.style.transform = `translateY(${deltaY}px)`;
          el.style.transition = 'none';

          requestAnimationFrame(() => {
            el.style.transition = 'transform 0.28s cubic-bezier(0.2, 0, 0, 1)';
            el.style.transform = '';
          });
        }
        prevPosMap.set(cat.id, currentTop);
      }
    });
  }, [categories, activeView]);

  // Reset state when opening/closing
  useEffect(() => {
    if (isOpen) {
      setActiveView('menu');
      setEditingCategoryId(null);
      setEditingName('');
      setCategoryEditError(null);
      setDraggedIndex(null);
      setDropTarget(null);
    }
  }, [isOpen]);

  // Handle ESC key to close, navigate back, or cancel inline edit
  useEffect(() => {
    if (!isOpen) return;

    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        if (editingCategoryId !== null) {
          setEditingCategoryId(null);
          setEditingName('');
          setCategoryEditError(null);
        } else if (activeView !== 'menu') {
          setActiveView('menu');
        } else {
          onClose();
        }
      }
    }

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, activeView, editingCategoryId, onClose]);

  function startEditing(category: Category) {
    setEditingCategoryId(category.id);
    setEditingName(category.name);
    setCategoryEditError(null);
  }

  function cancelEditing() {
    setEditingCategoryId(null);
    setEditingName('');
    setCategoryEditError(null);
  }

  async function handleSaveCategory(id: string) {
    const trimmed = editingName.trim();
    if (!trimmed) return;

    const currentCat = categories.find((c) => c.id === id);
    if (currentCat && currentCat.name === trimmed) {
      setEditingCategoryId(null);
      return;
    }

    setIsSavingCategory(true);
    setCategoryEditError(null);
    try {
      if (onUpdateCategory) {
        await onUpdateCategory(id, trimmed);
      }
      setEditingCategoryId(null);
    } catch (err: any) {
      setCategoryEditError(err.message || 'Nie udało się zapisać nazwy kategorii.');
    } finally {
      setIsSavingCategory(false);
    }
  }

  // Drag & drop handlers for category reordering in Settings
  function handleDragStart(e: React.DragEvent, originalIndex: number) {
    if (editingCategoryId !== null) return;
    setDraggedIndex(originalIndex);
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', originalIndex.toString());
    e.dataTransfer.setData('application/x-servanda-category-settings', originalIndex.toString());
  }

  function handleDragOver(e: React.DragEvent, originalIndex: number) {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';

    if (draggedIndex === null) return;

    const rect = e.currentTarget.getBoundingClientRect();
    const ratio = (e.clientY - rect.top) / rect.height;

    let position: 'before' | 'after' | null = null;

    if (ratio >= 0.55) {
      position = 'after';
    } else if (originalIndex === 0 && ratio <= 0.35) {
      position = 'before';
    }

    if (!position) {
      if (dropTarget !== null) setDropTarget(null);
      return;
    }

    const destIndex = getDestinationIndex(draggedIndex, originalIndex, position);
    if (destIndex === draggedIndex) {
      if (dropTarget !== null) setDropTarget(null);
      return;
    }

    if (dropTarget?.index !== originalIndex || dropTarget?.position !== position) {
      setDropTarget({ index: originalIndex, position });
    }
  }

  function handleDragLeave(e: React.DragEvent) {
    const relatedTarget = e.relatedTarget as Node | null;
    if (e.currentTarget.contains(relatedTarget)) {
      return;
    }
    setDropTarget(null);
  }

  function handleDrop(e: React.DragEvent, targetIndex: number) {
    e.preventDefault();

    if (draggedIndex === null) return;

    let position = dropTarget?.position;
    let target = dropTarget ? dropTarget.index : targetIndex;

    if (!position) {
      const rect = e.currentTarget.getBoundingClientRect();
      const height = rect.height || 40;
      const ratio = (e.clientY - rect.top) / height;
      if (ratio >= 0.55 || rect.height === 0) {
        position = 'after';
      } else if (targetIndex === 0 && ratio <= 0.35) {
        position = 'before';
      } else {
        position = 'after';
      }
    }

    const newIndex = getDestinationIndex(draggedIndex, target, position);
    if (newIndex !== draggedIndex && onReorderCategories) {
      onReorderCategories(draggedIndex, newIndex);
    }

    setDraggedIndex(null);
    setDropTarget(null);
  }

  function handleDragEnd() {
    setDraggedIndex(null);
    setDropTarget(null);
  }

  if (!isOpen) return null;

  return (
    <div
      className="settings-modal-backdrop"
      onClick={(e) => {
        if (e.target === e.currentTarget) {
          onClose();
        }
      }}
      data-testid="settings-modal-backdrop"
    >
      <div
        className="settings-modal-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="settings-dialog-title"
        data-testid="settings-modal-dialog"
      >
        <header className="settings-modal-header">
          <div className="settings-header-left">
            {activeView !== 'menu' && (
              <button
                type="button"
                className="settings-back-btn"
                onClick={() => setActiveView('menu')}
                title="Wróć do listy opcji"
                aria-label="Wróć do listy opcji"
              >
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="back-icon">
                  <polyline points="15 18 9 12 15 6" />
                </svg>
              </button>
            )}
            <div>
              <h2 id="settings-dialog-title" className="settings-modal-title">
                {activeView === 'menu' && 'Ustawienia'}
                {activeView === 'categories' && 'Zarządzaj Kategoriami'}
                {activeView === 'note-tile' && 'Zarządzaj Kaflem notatki'}
                {activeView === 'backup' && 'Kopia zapasowa danych'}
              </h2>
              <p className="settings-modal-subtitle">
                {activeView === 'menu' && 'Dostosuj aplikację Servanda do swoich potrzeb'}
                {activeView === 'categories' && 'Edytuj nazwy i zmieniaj kolejność kategorii przeciągając je'}
                {activeView === 'note-tile' && 'Dostosuj wygląd i zachowanie kafelka notatek'}
                {activeView === 'backup' && 'Wybierz metody tworzenia kopii zapasowej i konfigurację eksportu'}
              </p>
            </div>
          </div>
          <button
            type="button"
            className="settings-close-btn"
            onClick={onClose}
            aria-label="Zamknij ustawienia"
            title="Zamknij"
          >
            ✕
          </button>
        </header>

        <div className="settings-modal-content">
          {activeView === 'menu' && (
            <div className="settings-options-grid" data-testid="settings-options-menu">
              {/* Opcja 1: Zarządzaj Kategoriami */}
              <button
                type="button"
                className="settings-option-card"
                onClick={() => setActiveView('categories')}
                data-testid="settings-option-categories"
              >
                <div className="settings-option-icon-wrapper categories-icon-theme">
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    className="settings-option-icon"
                  >
                    <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
                    <line x1="12" y1="11" x2="12" y2="17" />
                    <line x1="9" y1="14" x2="15" y2="14" />
                  </svg>
                </div>
                <div className="settings-option-info">
                  <h3 className="settings-option-title">Zarządzaj Kategoriami</h3>
                  <p className="settings-option-description">
                    Edytuj nazwy kategorii (kliknij na nazwę) i zmieniaj ich kolejność metodą przeciągnij i upuść.
                  </p>
                  <span className="settings-option-meta">
                    Liczba kategorii: <strong>{categories.length}</strong>
                  </span>
                </div>
                <div className="settings-option-arrow" aria-hidden="true">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <polyline points="9 18 15 12 9 6" />
                  </svg>
                </div>
              </button>

              {/* Opcja 2: Zarządzaj Kaflem notatki */}
              <button
                type="button"
                className="settings-option-card"
                onClick={() => setActiveView('note-tile')}
                data-testid="settings-option-note-tile"
              >
                <div className="settings-option-icon-wrapper tile-icon-theme">
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    className="settings-option-icon"
                  >
                    <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
                    <line x1="7" y1="7" x2="17" y2="7" />
                    <line x1="7" y1="11" x2="14" y2="11" />
                    <line x1="7" y1="15" x2="11" y2="15" />
                  </svg>
                </div>
                <div className="settings-option-info">
                  <h3 className="settings-option-title">Zarządzaj Kaflem notatki</h3>
                  <p className="settings-option-description">
                    Dostosuj widoczność elementów na kafelku, długość podglądu treści oraz format daty.
                  </p>
                  <span className="settings-option-meta">
                    Wygląd i układ kafelków
                  </span>
                </div>
                <div className="settings-option-arrow" aria-hidden="true">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <polyline points="9 18 15 12 9 6" />
                  </svg>
                </div>
              </button>

              {/* Opcja 3: Kopia zapasowa danych (wkrótce dostępna) */}
              <button
                type="button"
                className="settings-option-card"
                onClick={() => setActiveView('backup')}
                data-testid="settings-option-backup"
              >
                <div className="settings-option-icon-wrapper backup-icon-theme">
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    className="settings-option-icon"
                  >
                    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                    <polyline points="7 10 12 15 17 10" />
                    <line x1="12" y1="15" x2="12" y2="3" />
                  </svg>
                </div>
                <div className="settings-option-info">
                  <div className="settings-option-title-row">
                    <h3 className="settings-option-title">Kopia zapasowa danych</h3>
                    <span className="badge-coming-soon" data-testid="backup-coming-soon-badge">Wkrótce</span>
                  </div>
                  <p className="settings-option-description">
                    Tworzenie zrzutów ekranu notatek do uporządkowanych folderów, eksport Markdown oraz kopia bazy SQLite.
                  </p>
                  <span className="settings-option-meta">
                    Screenshots do folderów, Markdown, SQLite
                  </span>
                </div>
                <div className="settings-option-arrow" aria-hidden="true">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <polyline points="9 18 15 12 9 6" />
                  </svg>
                </div>
              </button>
            </div>
          )}

          {activeView === 'categories' && (
            <div className="settings-subview" data-testid="settings-subview-categories">
              <div className="settings-section-card">
                <div className="settings-section-header-row">
                  <div>
                    <h4 className="settings-section-heading">Kategorie i podkategorie</h4>
                    <p className="settings-section-text">
                      Kliknij na nazwę, aby ją edytować, lub przeciągnij kategorię, aby zmienić jej kolejność.
                    </p>
                  </div>
                </div>

                {categoryEditError && (
                  <div className="alert-error" role="alert">
                    {categoryEditError}
                  </div>
                )}

                <div className="settings-categories-list-preview" role="list">
                  {hierarchicalCategories.length === 0 ? (
                    <p className="empty-state">Brak utworzonych kategorii.</p>
                  ) : (
                    hierarchicalCategories.map(({ category: cat, originalIndex, isSubcategory }) => {
                      const isEditing = editingCategoryId === cat.id;
                      const isDragging = draggedIndex === originalIndex;
                      const showLineBefore =
                        dropTarget?.index === originalIndex && dropTarget?.position === 'before' && draggedIndex !== originalIndex;
                      const showLineAfter =
                        dropTarget?.index === originalIndex && dropTarget?.position === 'after' && draggedIndex !== originalIndex;

                      return (
                        <div
                          key={cat.id}
                          ref={(el) => {
                            if (el) {
                              itemRefs.current.set(cat.id, el);
                            } else {
                              itemRefs.current.delete(cat.id);
                            }
                          }}
                          className={`settings-category-item-container ${isSubcategory ? 'subcategory-item' : ''}`}
                        >
                          {showLineBefore && (
                            <div className="drop-indicator-line drop-line-before" aria-hidden="true" />
                          )}

                          <div
                            className={`settings-category-preview-item ${isSubcategory ? 'is-sub' : ''} ${
                              isDragging ? 'dragging' : ''
                            }`}
                            draggable={!isEditing}
                            onDragStart={(e) => handleDragStart(e, originalIndex)}
                            onDragOver={(e) => handleDragOver(e, originalIndex)}
                            onDragLeave={handleDragLeave}
                            onDrop={(e) => handleDrop(e, originalIndex)}
                            onDragEnd={handleDragEnd}
                            data-testid={`settings-category-item-${cat.id}`}
                            title="Przeciągnij, aby zmienić kolejność"
                          >
                            <div
                              className="drag-handle"
                              aria-hidden="true"
                              title="Chwyć i przeciągnij, aby zmienić kolejność"
                              data-testid={`settings-drag-handle-${cat.id}`}
                            >
                              <svg viewBox="0 0 24 24" fill="currentColor" className="drag-icon">
                                <circle cx="9" cy="6" r="1.5" />
                                <circle cx="15" cy="6" r="1.5" />
                                <circle cx="9" cy="12" r="1.5" />
                                <circle cx="15" cy="12" r="1.5" />
                                <circle cx="9" cy="18" r="1.5" />
                                <circle cx="15" cy="18" r="1.5" />
                              </svg>
                            </div>

                            <span
                              className="cat-color-dot"
                              style={{ backgroundColor: cat.color || (isSubcategory ? '#06b6d4' : '#38bdf8') }}
                            />

                            {isEditing ? (
                              <form
                                className="category-inline-edit-form"
                                onSubmit={(e) => {
                                  e.preventDefault();
                                  handleSaveCategory(cat.id);
                                }}
                              >
                                <input
                                  type="text"
                                  className="category-name-edit-input"
                                  value={editingName}
                                  onChange={(e) => setEditingName(e.target.value)}
                                  onKeyDown={(e) => {
                                    if (e.key === 'Escape') {
                                      e.stopPropagation();
                                      cancelEditing();
                                    }
                                  }}
                                  autoFocus
                                  disabled={isSavingCategory}
                                  data-testid={`category-edit-input-${cat.id}`}
                                  aria-label={`Edytuj nazwę kategorii ${cat.name}`}
                                />
                                <div className="category-edit-actions">
                                  <button
                                    type="submit"
                                    className="btn-category-save"
                                    disabled={isSavingCategory || !editingName.trim()}
                                    title="Zapisz nazwę (Enter)"
                                    aria-label="Zapisz"
                                    data-testid={`category-save-btn-${cat.id}`}
                                  >
                                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                                      <polyline points="20 6 9 17 4 12" />
                                    </svg>
                                  </button>
                                  <button
                                    type="button"
                                    className="btn-category-cancel"
                                    onClick={cancelEditing}
                                    disabled={isSavingCategory}
                                    title="Anuluj edycję (Esc)"
                                    aria-label="Anuluj"
                                    data-testid={`category-cancel-btn-${cat.id}`}
                                  >
                                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                                      <line x1="18" y1="6" x2="6" y2="18" />
                                      <line x1="6" y1="6" x2="18" y2="18" />
                                    </svg>
                                  </button>
                                </div>
                              </form>
                            ) : (
                              <button
                                type="button"
                                className="cat-name-interactive-btn"
                                onClick={() => startEditing(cat)}
                                title="Kliknij, aby edytować nazwę"
                                data-testid={`category-name-btn-${cat.id}`}
                              >
                                <span className="cat-name">{cat.name}</span>
                                <span className="cat-edit-hint-icon" aria-hidden="true" title="Edytuj nazwę">
                                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                                    <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                                    <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                                  </svg>
                                </span>
                              </button>
                            )}
                          </div>

                          {showLineAfter && (
                            <div className="drop-indicator-line drop-line-after" aria-hidden="true" />
                          )}
                        </div>
                      );
                    })
                  )}
                </div>
              </div>
            </div>
          )}

          {activeView === 'note-tile' && (
            <div className="settings-subview" data-testid="settings-subview-note-tile">
              <div className="settings-section-card">
                <h4 className="settings-section-heading">Podgląd kafelka notatki</h4>
                <p className="settings-section-text">
                  Kafelki notatek wyświetlają tytuł, podgląd treści, datę utworzenia oraz kolor przypisany do kategorii.
                </p>
                <div className="settings-tile-preview-container">
                  <div className="note-tile-card sample-preview-tile" style={{ '--tile-border-color': '#38bdf8' } as React.CSSProperties}>
                    <div className="note-tile-header">
                      <h4 className="note-tile-title">Przykładowa notatka</h4>
                      <div className="note-drag-handle" title="Uchwyt przeciągania">
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
                    <p className="note-tile-snippet">
                      Oto przykładowy tekst podglądu notatki na kafelku, pokazujący jak prezentuje się treść.
                    </p>
                    <footer className="note-tile-meta">
                      <span>{new Date().toLocaleDateString('pl-PL')}</span>
                    </footer>
                  </div>
                </div>
              </div>
            </div>
          )}

          {activeView === 'backup' && (
            <div className="settings-subview" data-testid="settings-subview-backup">
              {/* Alert informacyjny o stanie funkcji */}
              <div className="backup-status-banner" role="status" data-testid="backup-status-banner">
                <div className="backup-status-icon-wrap" aria-hidden="true">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="status-info-icon">
                    <circle cx="12" cy="12" r="10" />
                    <line x1="12" y1="8" x2="12" y2="12" />
                    <line x1="12" y1="16" x2="12.01" y2="16" />
                  </svg>
                </div>
                <div className="backup-status-text">
                  <div className="backup-status-heading-row">
                    <strong>Funkcjonalność w przygotowaniu (Wkrótce dostępna)</strong>
                    <span className="badge-coming-soon">Podgląd planowanych opcji</span>
                  </div>
                  <p>
                    Poniżej możesz zapoznać się z planowanymi metodami archiwizacji i skonfigurować schemat eksportu danych.
                    Wszystkie mechanizmy kopii zapasowej zostaną aktywowane w nadchodzącej aktualizacji.
                  </p>
                </div>
              </div>

              {/* Sekcja 1: Zrzuty ekranu notatek do folderów */}
              <div className="settings-section-card backup-section-card" data-testid="backup-section-screenshots">
                <div className="backup-card-header">
                  <div className="backup-card-title-group">
                    <div className="backup-mini-icon-wrapper screenshot-icon-theme" aria-hidden="true">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                        <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
                        <circle cx="8.5" cy="8.5" r="1.5" />
                        <polyline points="21 15 16 10 5 21" />
                      </svg>
                    </div>
                    <div>
                      <h4 className="settings-section-heading">Zrzuty ekranu notatek (Screenshots do folderów)</h4>
                      <p className="settings-section-text">
                        Automatyczne generowanie plików graficznych (PNG) każdej notatki w pełnym formacie wizualnym i rozmieszczanie ich w strukturze folderów.
                      </p>
                    </div>
                  </div>
                  <label className="toggle-switch" title="Włącz/wyłącz zrzuty ekranu">
                    <input
                      type="checkbox"
                      checked={backupScreenshots}
                      onChange={(e) => setBackupScreenshots(e.target.checked)}
                      aria-label="Włącz tworzenie zrzutów ekranu notatek"
                    />
                    <span className="toggle-slider" />
                  </label>
                </div>

                {backupScreenshots && (
                  <div className="backup-options-nested">
                    <div className="backup-option-subrow">
                      <label className="backup-subrow-label">Struktura folderów dla zrzutów ekranu:</label>
                      <div className="folder-structure-selector" role="radiogroup" aria-label="Wybór struktury folderów dla zrzutów ekranu">
                        <label className={`folder-radio-card ${screenshotFolderStructure === 'hierarchical' ? 'active' : ''}`}>
                          <input
                            type="radio"
                            name="folder_structure"
                            value="hierarchical"
                            checked={screenshotFolderStructure === 'hierarchical'}
                            onChange={() => setScreenshotFolderStructure('hierarchical')}
                          />
                          <div className="folder-radio-content">
                            <span className="folder-radio-title">Kategoria / Podkategoria / Tytuł.png</span>
                            <span className="folder-radio-desc">Zalecana hierarchiczna struktura odzwierciedlająca Twoje kategorie</span>
                          </div>
                        </label>

                        <label className={`folder-radio-card ${screenshotFolderStructure === 'flat' ? 'active' : ''}`}>
                          <input
                            type="radio"
                            name="folder_structure"
                            value="flat"
                            checked={screenshotFolderStructure === 'flat'}
                            onChange={() => setScreenshotFolderStructure('flat')}
                          />
                          <div className="folder-radio-content">
                            <span className="folder-radio-title">Kategoria / Tytuł_notatki.png</span>
                            <span className="folder-radio-desc">Płaski podział wyłącznie według głównej kategorii</span>
                          </div>
                        </label>

                        <label className={`folder-radio-card ${screenshotFolderStructure === 'date_hierarchical' ? 'active' : ''}`}>
                          <input
                            type="radio"
                            name="folder_structure"
                            value="date_hierarchical"
                            checked={screenshotFolderStructure === 'date_hierarchical'}
                            onChange={() => setScreenshotFolderStructure('date_hierarchical')}
                          />
                          <div className="folder-radio-content">
                            <span className="folder-radio-title">RRRR-MM-DD / Kategoria / Tytuł.png</span>
                            <span className="folder-radio-desc">Podział z datą utworzenia kopii na początku ścieżki</span>
                          </div>
                        </label>
                      </div>
                    </div>

                    {/* Wizualny podgląd struktury katalogów */}
                    <div className="backup-folder-tree-preview" aria-label="Podgląd struktury folderów">
                      <div className="folder-tree-header">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="folder-tree-icon" aria-hidden="true">
                          <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
                        </svg>
                        <span>Podgląd wygenerowanej struktury plików na dysku:</span>
                      </div>
                      <pre className="folder-tree-code">
                        {screenshotFolderStructure === 'hierarchical' && (
`📁 Servanda_Backup/
├── 📁 Screenshots/
│   ├── 📁 Prompty/
│   │   ├── 📁 Asystenci AI/
│   │   │   └── 🖼️ Architektura_systemu.png
│   │   └── 🖼️ Generator_raportow.png
│   └── 📁 Projekty/
│       └── 🖼️ Zadania_na_ten_tydzien.png`
                        )}
                        {screenshotFolderStructure === 'flat' && (
`📁 Servanda_Backup/
├── 📁 Screenshots/
│   ├── 📁 Prompty/
│   │   └── 🖼️ Architektura_systemu.png
│   └── 📁 Projekty/
│       └── 🖼️ Zadania_na_ten_tydzien.png`
                        )}
                        {screenshotFolderStructure === 'date_hierarchical' && (
`📁 Servanda_Backup/
├── 📁 2026-08-22/
│   └── 📁 Screenshots/
│       ├── 📁 Prompty/
│       │   └── 🖼️ Architektura_systemu.png
│       └── 📁 Projekty/
│           └── 🖼️ Zadania_na_ten_tydzien.png`
                        )}
                      </pre>
                    </div>

                    <div className="backup-checkbox-grid">
                      <label className="backup-checkbox-label">
                        <input
                          type="checkbox"
                          checked={screenshotHighRes}
                          onChange={(e) => setScreenshotHighRes(e.target.checked)}
                        />
                        <span>Wysoka rozdzielczość HD (2x Retina dla krystalicznie czystego tekstu)</span>
                      </label>
                      <label className="backup-checkbox-label">
                        <input
                          type="checkbox"
                          checked={screenshotIncludeMeta}
                          onChange={(e) => setScreenshotIncludeMeta(e.target.checked)}
                        />
                        <span>Dołączaj kolor kategorii, tagi i datę edycji na dole grafiki</span>
                      </label>
                    </div>
                  </div>
                )}
              </div>

              {/* Sekcja 2: Eksport Markdown (.md) */}
              <div className="settings-section-card backup-section-card" data-testid="backup-section-markdown">
                <div className="backup-card-header">
                  <div className="backup-card-title-group">
                    <div className="backup-mini-icon-wrapper markdown-icon-theme" aria-hidden="true">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                        <polyline points="14 2 14 8 20 8" />
                        <line x1="16" y1="13" x2="8" y2="13" />
                        <line x1="16" y1="17" x2="8" y2="17" />
                        <polyline points="10 9 9 9 8 9" />
                      </svg>
                    </div>
                    <div>
                      <h4 className="settings-section-heading">Eksport do plików Markdown (.md)</h4>
                      <p className="settings-section-text">
                        Zapis każdej notatki jako niezależny plik `.md` z nagłówkiem YAML frontmatter (tagi, data, powiązania).
                        Idealne do używania z Obsidian, Logseq lub edytorami tekstowymi.
                      </p>
                    </div>
                  </div>
                  <label className="toggle-switch" title="Włącz/wyłącz eksport Markdown">
                    <input
                      type="checkbox"
                      checked={backupMarkdown}
                      onChange={(e) => setBackupMarkdown(e.target.checked)}
                      aria-label="Włącz eksport do plików Markdown"
                    />
                    <span className="toggle-slider" />
                  </label>
                </div>
              </div>

              {/* Sekcja 3: Kopia bazy danych SQLite (.db) */}
              <div className="settings-section-card backup-section-card" data-testid="backup-section-sqlite">
                <div className="backup-card-header">
                  <div className="backup-card-title-group">
                    <div className="backup-mini-icon-wrapper sqlite-icon-theme" aria-hidden="true">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                        <ellipse cx="12" cy="5" rx="9" ry="3" />
                        <path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3" />
                        <path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5" />
                      </svg>
                    </div>
                    <div>
                      <h4 className="settings-section-heading">Kopia bazy danych SQLite (.db)</h4>
                      <p className="settings-section-text">
                        Tworzy bezpieczną kopię zapasową całego pliku bazy SQLite (`data/servanda.db`) ze znacznikiem czasu i sumą kontrolną.
                      </p>
                    </div>
                  </div>
                  <label className="toggle-switch" title="Włącz/wyłącz kopię bazy SQLite">
                    <input
                      type="checkbox"
                      checked={backupSqlite}
                      onChange={(e) => setBackupSqlite(e.target.checked)}
                      aria-label="Włącz kopię bazy SQLite"
                    />
                    <span className="toggle-slider" />
                  </label>
                </div>
              </div>

              {/* Sekcja 4: Paczka ZIP i Harmonogram */}
              <div className="settings-section-card backup-section-card" data-testid="backup-section-archive">
                <div className="backup-card-header">
                  <div className="backup-card-title-group">
                    <div className="backup-mini-icon-wrapper zip-icon-theme" aria-hidden="true">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                        <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
                        <polyline points="3.27 6.96 12 12.01 20.73 6.96" />
                        <line x1="12" y1="22.08" x2="12" y2="12" />
                      </svg>
                    </div>
                    <div>
                      <h4 className="settings-section-heading">Kompresja do archiwum ZIP i harmonogram</h4>
                      <p className="settings-section-text">
                        Pakowanie wszystkich wybranych materiałów do jednego archiwum ZIP oraz automatyzacja tworzenia kopii.
                      </p>
                    </div>
                  </div>
                  <label className="toggle-switch" title="Włącz/wyłącz pakowanie do ZIP">
                    <input
                      type="checkbox"
                      checked={backupZip}
                      onChange={(e) => setBackupZip(e.target.checked)}
                      aria-label="Włącz kompresję do ZIP"
                    />
                    <span className="toggle-slider" />
                  </label>
                </div>

                <div className="backup-schedule-row">
                  <span className="backup-subrow-label">Automatyczne wykonywanie kopii zapasowej:</span>
                  <div className="schedule-pills">
                    <button
                      type="button"
                      className={`schedule-pill ${backupSchedule === 'on_close' ? 'active' : ''}`}
                      onClick={() => setBackupSchedule('on_close')}
                    >
                      Przy zamykaniu aplikacji
                    </button>
                    <button
                      type="button"
                      className={`schedule-pill ${backupSchedule === 'daily' ? 'active' : ''}`}
                      onClick={() => setBackupSchedule('daily')}
                    >
                      Raz dziennie
                    </button>
                    <button
                      type="button"
                      className={`schedule-pill ${backupSchedule === 'manual' ? 'active' : ''}`}
                      onClick={() => setBackupSchedule('manual')}
                    >
                      Tylko ręcznie
                    </button>
                  </div>
                </div>
              </div>

              {/* Dolny pasek akcji z zablokowanym przyciskiem */}
              <div className="backup-action-bar">
                <button
                  type="button"
                  className="btn-primary backup-execute-btn"
                  disabled
                  title="Funkcja jest obecnie w trakcie wdrażania"
                  data-testid="backup-execute-btn"
                >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="btn-icon" aria-hidden="true">
                    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                    <polyline points="7 10 12 15 17 10" />
                    <line x1="12" y1="15" x2="12" y2="3" />
                  </svg>
                  Utwórz kopię zapasową teraz (Wkrótce dostępne)
                </button>
              </div>
            </div>
          )}
        </div>

        <footer className="settings-modal-footer">
          {activeView !== 'menu' ? (
            <button
              type="button"
              className="btn-secondary"
              onClick={() => {
                setEditingCategoryId(null);
                setEditingName('');
                setCategoryEditError(null);
                setActiveView('menu');
              }}
            >
              ← Powrót do menu opcji
            </button>
          ) : (
            <div />
          )}
          <button
            type="button"
            className="btn-primary"
            onClick={onClose}
          >
            Gotowe
          </button>
        </footer>
      </div>
    </div>
  );
};

export default SettingsModal;
