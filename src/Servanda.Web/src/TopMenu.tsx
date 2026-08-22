import React from 'react';
import type { Category } from './api';

export interface TopMenuProps {
  categories: Category[];
  selectedCategoryId: string | null;
  onSelectCategory: (categoryId: string | null) => void;
  notesCount: number;
  isFormOpen: boolean;
  onToggleAddNote: () => void;
  onOpenSettings: () => void;
  healthError: string | null;
}

export const TopMenu: React.FC<TopMenuProps> = ({
  categories,
  selectedCategoryId,
  onSelectCategory,
  notesCount,
  isFormOpen,
  onToggleAddNote,
  onOpenSettings,
  healthError,
}) => {
  const selectedCategory = categories.find((c) => c.id === selectedCategoryId);
  let parentCategory: Category | undefined;
  if (selectedCategory?.parentCategoryId) {
    parentCategory = categories.find((c) => c.id === selectedCategory.parentCategoryId);
  }

  return (
    <header className="app-top-menu" data-testid="app-top-menu">
      {/* Left Brand block - seamlessly aligned with the 270px sidebar beneath */}
      <div className="top-menu-brand-section">
        <button
          type="button"
          className="top-menu-brand-link"
          onClick={() => onSelectCategory(null)}
          title="Przejdź do widoku głównego (Wszystkie notatki)"
        >
          <div className="brand-logo">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="brand-icon" aria-hidden="true">
              <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20" />
              <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z" />
            </svg>
          </div>
          <div className="brand-text">
            <h2>Servanda</h2>
            <span className="brand-tagline">Lokalny schowek</span>
          </div>
        </button>
      </div>

      {/* Main Top Bar Area spanning over the viewport */}
      <div className="top-menu-main-section">
        <div className="top-menu-left-group">
          {/* Active Navigation Context / Breadcrumb */}
          <nav className="top-menu-breadcrumb" aria-label="Nawigacja okruszkowa">
            <button
              type="button"
              className={`breadcrumb-root-btn ${selectedCategoryId === null ? 'active' : ''}`}
              onClick={() => onSelectCategory(null)}
              title="Wszystkie notatki"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="breadcrumb-icon" aria-hidden="true">
                <rect x="3" y="3" width="7" height="7" />
                <rect x="14" y="3" width="7" height="7" />
                <rect x="14" y="14" width="7" height="7" />
                <rect x="3" y="14" width="7" height="7" />
              </svg>
              <span>Wszystkie notatki</span>
            </button>

            {selectedCategory && (
              <div className="breadcrumb-path">
                <span className="breadcrumb-separator" aria-hidden="true">/</span>
                {parentCategory && (
                  <>
                    <button
                      type="button"
                      className="breadcrumb-item-btn"
                      onClick={() => onSelectCategory(parentCategory!.id)}
                      title={`Przejdź do: ${parentCategory.name}`}
                      aria-label={`Przejdź do: ${parentCategory.name}`}
                    >
                      {parentCategory.name}
                    </button>
                    <span className="breadcrumb-separator" aria-hidden="true">/</span>
                  </>
                )}
                <span className="breadcrumb-current" data-testid="breadcrumb-active-category">
                  {selectedCategory.name}
                </span>
                <button
                  type="button"
                  className="breadcrumb-clear-btn"
                  onClick={() => onSelectCategory(null)}
                  title="Wyczyść filtr kategorii"
                  aria-label="Wyczyść filtr kategorii"
                >
                  ✕
                </button>
              </div>
            )}
          </nav>
        </div>

        {/* Centered Main Title and Subtitle */}
        <div className="top-menu-center-group">
          <div className="top-menu-title-block">
            <h1 className="top-menu-heading">Prywatne notatki</h1>
            <p className="top-menu-subtitle">Przeglądaj notatki pogrupowane w kategorie i podkategorie</p>
          </div>
        </div>

        <div className="top-menu-right-group">
          {/* Health & DB Status */}
          <div
            className={`top-menu-status-badge ${healthError ? 'status-err' : 'status-ok'}`}
            title={healthError ? `Błąd połączenia: ${healthError}` : 'Baza SQLite podłączona poprawnie'}
            data-testid="top-menu-db-status"
          >
            <span className="status-dot" aria-hidden="true" />
            <span className="status-label">{healthError ? 'API / DB Błąd' : 'SQLite: OK'}</span>
          </div>

          {/* Stats Badge */}
          <div className="top-menu-stats" title="Liczba notatek i kategorii">
            <span className="stats-pill" data-testid="top-menu-notes-count">
              <span className="stats-icon" aria-hidden="true">📝</span>
              <strong>{notesCount}</strong> {notesCount === 1 ? 'notatka' : 'notatek'}
            </span>
          </div>

          {/* Add Note Button transferred to Top Menu */}
          <button
            type="button"
            className={`btn-top-add-note ${isFormOpen ? 'btn-active' : ''}`}
            onClick={onToggleAddNote}
            data-testid="top-menu-add-note-btn"
            title={isFormOpen ? 'Zamknij formularz dodawania notatki' : 'Dodaj nową notatkę'}
          >
            <span className="btn-icon" aria-hidden="true">{isFormOpen ? '✕' : '+'}</span>
            <span>{isFormOpen ? 'Zamknij' : 'Dodaj notatkę'}</span>
          </button>

          {/* Settings button in top menu */}
          <button
            type="button"
            className="btn-top-settings"
            onClick={onOpenSettings}
            data-testid="top-menu-settings-btn"
            title="Otwórz ustawienia aplikacji"
            aria-label="Ustawienia"
          >
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="top-settings-icon"
              aria-hidden="true"
            >
              <circle cx="12" cy="12" r="3" />
              <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z" />
            </svg>
          </button>
        </div>
      </div>
    </header>
  );
};

export default TopMenu;
