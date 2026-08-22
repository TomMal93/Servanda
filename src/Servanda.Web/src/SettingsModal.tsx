import React, { useState, useEffect } from 'react';
import type { Category } from './api';

export type SettingsView = 'menu' | 'categories' | 'note-tile';

interface SettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
  categories?: Category[];
}

export const SettingsModal: React.FC<SettingsModalProps> = ({
  isOpen,
  onClose,
  categories = [],
}) => {
  const [activeView, setActiveView] = useState<SettingsView>('menu');

  // Reset to menu when opening
  useEffect(() => {
    if (isOpen) {
      setActiveView('menu');
    }
  }, [isOpen]);

  // Handle ESC key to close or navigate back
  useEffect(() => {
    if (!isOpen) return;

    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        if (activeView !== 'menu') {
          setActiveView('menu');
        } else {
          onClose();
        }
      }
    }

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, activeView, onClose]);

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
              </h2>
              <p className="settings-modal-subtitle">
                {activeView === 'menu' && 'Dostosuj aplikację Servanda do swoich potrzeb'}
                {activeView === 'categories' && 'Twórz, edytuj i porządkuj kategorie oraz podkategorie'}
                {activeView === 'note-tile' && 'Dostosuj wygląd i zachowanie kafelka notatek'}
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
                    Twórz nowe kategorie, ustalaj hierarchię podkategorii, wybieraj kolory i ikony.
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
            </div>
          )}

          {activeView === 'categories' && (
            <div className="settings-subview" data-testid="settings-subview-categories">
              <div className="settings-section-card">
                <h4 className="settings-section-heading">Kategorie i podkategorie</h4>
                <p className="settings-section-text">
                  Kategorie pozwalają grupować Twoje notatki. Możesz również przeciągać kategorie na pasku bocznym, aby zmieniać ich kolejność.
                </p>
                <div className="settings-categories-list-preview">
                  {categories.length === 0 ? (
                    <p className="empty-state">Brak utworzonych kategorii.</p>
                  ) : (
                    categories
                      .filter((c) => !c.parentCategoryId)
                      .sort((a, b) => a.sortOrder - b.sortOrder)
                      .map((cat) => {
                        const subs = categories
                          .filter((s) => s.parentCategoryId === cat.id)
                          .sort((a, b) => a.sortOrder - b.sortOrder);

                        return (
                          <div key={cat.id} className="settings-category-preview-group">
                            <div className="settings-category-preview-item">
                              <span className="cat-color-dot" style={{ backgroundColor: cat.color || '#38bdf8' }} />
                              <span className="cat-name">{cat.name}</span>
                              <span className="cat-order-badge">Kolejność: {cat.sortOrder}</span>
                            </div>
                            {subs.length > 0 && (
                              <div className="settings-subcategories-preview-list">
                                {subs.map((sub) => (
                                  <div key={sub.id} className="settings-category-preview-item is-sub">
                                    <span className="cat-color-dot" style={{ backgroundColor: sub.color || '#06b6d4' }} />
                                    <span className="cat-name">{sub.name}</span>
                                    <span className="cat-order-badge">Kolejność: {sub.sortOrder}</span>
                                  </div>
                                ))}
                              </div>
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
        </div>

        <footer className="settings-modal-footer">
          {activeView !== 'menu' ? (
            <button
              type="button"
              className="btn-secondary"
              onClick={() => setActiveView('menu')}
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
