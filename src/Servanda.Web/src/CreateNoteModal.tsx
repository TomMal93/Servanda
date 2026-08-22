import React, { useState, useEffect } from 'react';
import type { Category, Note } from './api';

export type NoteType = 'text' | 'checklist' | 'code' | 'links' | 'canvas';

export interface NoteTypeOption {
  id: NoteType;
  title: string;
  badge: string;
  badgeType: 'active' | 'upcoming';
  description: string;
  icon: (props: { className?: string }) => React.JSX.Element;
}

export const NOTE_TYPE_OPTIONS: NoteTypeOption[] = [
  {
    id: 'text',
    title: 'Notatka tekstowa',
    badge: 'Standardowa',
    badgeType: 'active',
    description: 'Klasyczna notatka z formatowaniem Markdown, nagłówkami i listami.',
    icon: ({ className = '' }) => (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className={className} aria-hidden="true">
        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
        <polyline points="14 2 14 8 20 8" />
        <line x1="16" y1="13" x2="8" y2="13" />
        <line x1="16" y1="17" x2="8" y2="17" />
        <polyline points="10 9 9 9 8 9" />
      </svg>
    ),
  },
  {
    id: 'checklist',
    title: 'Lista zadań (Checklista)',
    badge: 'Wkrótce',
    badgeType: 'upcoming',
    description: 'Interaktywna lista kontrolna z polami do odhaczania ukończonych zadań.',
    icon: ({ className = '' }) => (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className={className} aria-hidden="true">
        <polyline points="9 11 12 14 22 4" />
        <path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11" />
      </svg>
    ),
  },
  {
    id: 'code',
    title: 'Fragment kodu (Snippet)',
    badge: 'Wkrótce',
    badgeType: 'upcoming',
    description: 'Notatka techniczna z kolorowaniem składni, numeracją linii i wyborem języka.',
    icon: ({ className = '' }) => (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className={className} aria-hidden="true">
        <polyline points="16 18 22 12 16 6" />
        <polyline points="8 6 2 12 8 18" />
      </svg>
    ),
  },
  {
    id: 'links',
    title: 'Zbiór linków (Zakładki)',
    badge: 'Wkrótce',
    badgeType: 'upcoming',
    description: 'Kolekcja odnośników internetowych z automatycznym podglądem i etykietami.',
    icon: ({ className = '' }) => (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className={className} aria-hidden="true">
        <path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71" />
        <path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71" />
      </svg>
    ),
  },
  {
    id: 'canvas',
    title: 'Tablica pomysłów (Szkic)',
    badge: 'Wkrótce',
    badgeType: 'upcoming',
    description: 'Swobodna tablica na luźne myśli, diagramy i mapy powiązań koncepcji.',
    icon: ({ className = '' }) => (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className={className} aria-hidden="true">
        <circle cx="12" cy="12" r="10" />
        <path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" />
        <line x1="12" y1="17" x2="12.01" y2="17" />
      </svg>
    ),
  },
];

export interface CreateNoteModalProps {
  isOpen: boolean;
  onClose: () => void;
  categories: Category[];
  selectedCategoryId: string | null;
  onCreateNote: (note: { title: string; content: string; categoryId: string | null }) => Promise<Note>;
  categoriesLoading?: boolean;
}

export const CreateNoteModal: React.FC<CreateNoteModalProps> = ({
  isOpen,
  onClose,
  categories,
  selectedCategoryId,
  onCreateNote,
  categoriesLoading = false,
}) => {
  const [selectedType, setSelectedType] = useState<NoteType>('text');
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [categoryId, setCategoryId] = useState<string>('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Reset or initialize state when opening
  useEffect(() => {
    if (isOpen) {
      setSelectedType('text');
      setTitle('');
      setContent('');
      setCategoryId(selectedCategoryId || '');
      setError(null);
      setSubmitting(false);
    }
  }, [isOpen, selectedCategoryId]);

  // Handle ESC key to close
  useEffect(() => {
    if (!isOpen) return;
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape' && !submitting) {
        onClose();
      }
    }
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, submitting, onClose]);

  if (!isOpen) return null;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!title.trim()) return;

    setSubmitting(true);
    setError(null);

    try {
      await onCreateNote({
        title: title.trim(),
        content: content.trim(),
        categoryId: categoryId ? categoryId : null,
      });
      onClose();
    } catch (err: any) {
      setError(err.message || 'Nie udało się zapisać notatki.');
    } finally {
      setSubmitting(false);
    }
  }

  const currentTypeOption = NOTE_TYPE_OPTIONS.find((t) => t.id === selectedType) || NOTE_TYPE_OPTIONS[0];

  return (
    <div
      className="create-note-modal-backdrop"
      onClick={(e) => {
        if (e.target === e.currentTarget && !submitting) {
          onClose();
        }
      }}
      data-testid="create-note-modal-backdrop"
    >
      <div
        className="create-note-modal-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="create-note-dialog-title"
        data-testid="create-note-modal-dialog"
      >
        <header className="create-note-modal-header">
          <div className="create-note-header-left">
            <div className="create-note-icon-badge">
              <currentTypeOption.icon className="create-note-header-type-icon" />
            </div>
            <div>
              <h2 id="create-note-dialog-title" className="create-note-modal-title">
                Utwórz nową notatkę
              </h2>
              <p className="create-note-modal-subtitle">
                Wybierz rodzaj notatki i uzupełnij zawartość
              </p>
            </div>
          </div>
          <button
            type="button"
            className="create-note-close-btn"
            onClick={onClose}
            aria-label="Zamknij formularz tworzenia notatki"
            title="Zamknij (Esc)"
            disabled={submitting}
            data-testid="create-note-close-btn"
          >
            ✕
          </button>
        </header>

        <div className="create-note-modal-content">
          {/* Note Type Selector Bar / Cards */}
          <div className="note-types-section" aria-label="Wybór rodzaju notatki">
            <div className="note-types-label-row">
              <span className="note-types-section-title">Wybierz rodzaj notatki</span>
              <span className="note-types-counter">{NOTE_TYPE_OPTIONS.length} rodzaje</span>
            </div>
            <div className="note-types-grid" data-testid="note-types-grid" role="tablist">
              {NOTE_TYPE_OPTIONS.map((typeOption) => {
                const isSelected = selectedType === typeOption.id;
                const IconComponent = typeOption.icon;
                return (
                  <button
                    key={typeOption.id}
                    type="button"
                    role="tab"
                    aria-selected={isSelected}
                    className={`note-type-card ${isSelected ? 'selected' : ''} type-${typeOption.id}`}
                    onClick={() => setSelectedType(typeOption.id)}
                    data-testid={`note-type-${typeOption.id}`}
                    title={typeOption.description}
                  >
                    <div className="note-type-card-top">
                      <div className="note-type-icon-wrapper">
                        <IconComponent className="note-type-icon" />
                      </div>
                      <span className={`note-type-badge badge-${typeOption.badgeType}`}>
                        {typeOption.badge}
                      </span>
                    </div>
                    <div className="note-type-card-info">
                      <strong className="note-type-name">{typeOption.title}</strong>
                      <p className="note-type-desc">{typeOption.description}</p>
                    </div>
                  </button>
                );
              })}
            </div>
          </div>

          {/* Form / Placeholder Section */}
          {selectedType === 'text' ? (
            <form onSubmit={handleSubmit} className="create-note-form" data-testid="create-note-text-form">
              {error && (
                <div className="alert-error" role="alert">
                  {error}
                </div>
              )}

              <div className="form-row-2col">
                <div className="form-group">
                  <label htmlFor="modal-note-title">
                    Tytuł notatki <span className="field-required">*</span>
                  </label>
                  <input
                    id="modal-note-title"
                    className="form-input"
                    type="text"
                    placeholder="np. Notatki ze spotkania projektowego"
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                    disabled={submitting}
                    required
                    autoFocus
                    data-testid="create-note-title-input"
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="modal-note-category">Kategoria / Podkategoria</label>
                  <select
                    id="modal-note-category"
                    className="form-select"
                    value={categoryId}
                    onChange={(e) => setCategoryId(e.target.value)}
                    disabled={submitting || categoriesLoading}
                    data-testid="create-note-category-select"
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
                <div className="form-label-with-hint">
                  <label htmlFor="modal-note-content">Treść notatki</label>
                  <span className="markdown-hint">Obsługuje Markdown</span>
                </div>
                <textarea
                  id="modal-note-content"
                  className="form-textarea create-note-textarea"
                  rows={6}
                  placeholder="Wpisz treść notatki... Możesz używać nagłówków, list, pogrubień oraz linków."
                  value={content}
                  onChange={(e) => setContent(e.target.value)}
                  disabled={submitting}
                  data-testid="create-note-content-input"
                />
              </div>

              <div className="create-note-modal-actions">
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={onClose}
                  disabled={submitting}
                  data-testid="create-note-cancel-btn"
                >
                  Anuluj
                </button>
                <button
                  type="submit"
                  className="btn-primary btn-save-note"
                  disabled={submitting || !title.trim()}
                  data-testid="create-note-submit-btn"
                >
                  {submitting ? (
                    <>
                      <span className="spinner-dots" aria-hidden="true">⏳</span>
                      Zapisywanie w SQLite...
                    </>
                  ) : (
                    <>
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" className="btn-action-icon" aria-hidden="true">
                        <polyline points="20 6 9 17 4 12" />
                      </svg>
                      Zapisz notatkę
                    </>
                  )}
                </button>
              </div>
            </form>
          ) : (
            /* Placeholder Panel for Upcoming Note Types */
            <div className="note-type-placeholder-panel" data-testid={`note-type-placeholder-${selectedType}`}>
              <div className="placeholder-hero-card">
                <div className="placeholder-hero-icon-container">
                  <currentTypeOption.icon className="placeholder-hero-icon" />
                </div>
                <div className="placeholder-hero-content">
                  <div className="placeholder-badge-row">
                    <span className="placeholder-status-pill">Planowana funkcja</span>
                    <span className="placeholder-type-name">{currentTypeOption.title}</span>
                  </div>
                  <h3 className="placeholder-hero-title">
                    Ten rodzaj notatki jest w trakcie projektowania
                  </h3>
                  <p className="placeholder-hero-description">
                    {selectedType === 'checklist' &&
                      'Moduł list zadań pozwoli na tworzenie interaktywnych list kontrolnych z możliwością odhaczania elementów, ustalania priorytetów i śledzenia postępów zadań.'}
                    {selectedType === 'code' &&
                      'Moduł fragmentów kodu zapewni dedykowany edytor z kolorowaniem składni dla TypeScript, C#, Python, SQL i innych języków wraz z numeracją linii i szybkim kopiowaniem.'}
                    {selectedType === 'links' &&
                      'Moduł zakładek umożliwi wygodne katalogowanie linków, automatyczne pobieranie tytułów stron i dodawanie własnych opisów do materiałów źródłowych.'}
                    {selectedType === 'canvas' &&
                      'Tablica pomysłów pozwoli na swobodne rozmieszczanie myśli, tworzenie map pojęciowych i szybkie szkicowanie koncepcji architektonicznych.'}
                  </p>
                </div>
              </div>

              {/* Interactive Mockup Preview */}
              <div className="placeholder-mockup-section">
                <div className="placeholder-mockup-header">
                  <span className="mockup-dot red" />
                  <span className="mockup-dot yellow" />
                  <span className="mockup-dot green" />
                  <span className="mockup-title">Podgląd planowanego interfejsu ({currentTypeOption.title})</span>
                </div>
                <div className="placeholder-mockup-body">
                  {selectedType === 'checklist' && (
                    <div className="mockup-checklist-preview">
                      <div className="mockup-todo-item checked">
                        <span className="mockup-checkbox">✓</span>
                        <span className="mockup-text strike">Zaprojektować architekturę bazy danych SQLite</span>
                      </div>
                      <div className="mockup-todo-item checked">
                        <span className="mockup-checkbox">✓</span>
                        <span className="mockup-text strike">Dodać obsługę hierarchicznych kategorii</span>
                      </div>
                      <div className="mockup-todo-item">
                        <span className="mockup-checkbox" />
                        <span className="mockup-text">Zaimplementować dedykowany edytor checklisty</span>
                      </div>
                      <div className="mockup-todo-item">
                        <span className="mockup-checkbox" />
                        <span className="mockup-text">Dodać wskaźnik procentowy ukończenia zadań</span>
                      </div>
                    </div>
                  )}

                  {selectedType === 'code' && (
                    <div className="mockup-code-preview">
                      <div className="mockup-code-line"><span className="code-ln">1</span><span className="code-kw">public record</span> <span className="code-fn">CreateNoteCommand</span>(</div>
                      <div className="mockup-code-line"><span className="code-ln">2</span>  <span className="code-type">string</span> Title,</div>
                      <div className="mockup-code-line"><span className="code-ln">3</span>  <span className="code-type">string?</span> Content,</div>
                      <div className="mockup-code-line"><span className="code-ln">4</span>  <span className="code-type">Guid?</span> CategoryId</div>
                      <div className="mockup-code-line"><span className="code-ln">5</span>);</div>
                    </div>
                  )}

                  {selectedType === 'links' && (
                    <div className="mockup-links-preview">
                      <div className="mockup-link-card">
                        <span className="mockup-link-icon">🔗</span>
                        <div>
                          <strong>Dokumentacja React & Vite</strong>
                          <span className="mockup-link-url">https://vite.dev/guide/</span>
                        </div>
                      </div>
                      <div className="mockup-link-card">
                        <span className="mockup-link-icon">📘</span>
                        <div>
                          <strong>Microsoft .NET 10 Minimal APIs</strong>
                          <span className="mockup-link-url">https://learn.microsoft.com/dotnet/</span>
                        </div>
                      </div>
                    </div>
                  )}

                  {selectedType === 'canvas' && (
                    <div className="mockup-canvas-preview">
                      <div className="mockup-canvas-bubble bubble-1">💡 Pomysł na aplikację</div>
                      <div className="mockup-canvas-arrow">➜</div>
                      <div className="mockup-canvas-bubble bubble-2">📂 Kategoria domenowa</div>
                      <div className="mockup-canvas-arrow">➜</div>
                      <div className="mockup-canvas-bubble bubble-3">🚀 Realizacja w SQLite</div>
                    </div>
                  )}
                </div>
              </div>

              <div className="placeholder-footer-actions">
                <button
                  type="button"
                  className="btn-primary"
                  onClick={() => setSelectedType('text')}
                  data-testid="switch-to-text-note-btn"
                >
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="btn-action-icon" aria-hidden="true">
                    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                    <polyline points="14 2 14 8 20 8" />
                  </svg>
                  Utwórz standardową notatkę tekstową
                </button>
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={onClose}
                >
                  Zamknij
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default CreateNoteModal;
