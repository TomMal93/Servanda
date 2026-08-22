import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { TopMenu, getPluralForm } from './TopMenu';
import type { Category, Note } from './api';

describe('TopMenu Component', () => {
  const mockCategories: Category[] = [
    { id: 'cat-1', name: 'Prompty', color: '#a855f7', sortOrder: 0, parentCategoryId: null },
    { id: 'sub-1', name: 'AI Kodowanie', color: '#ec4899', sortOrder: 0, parentCategoryId: 'cat-1' },
    { id: 'cat-2', name: 'Notatki', color: '#38bdf8', sortOrder: 1, parentCategoryId: null },
  ];

  const mockNotes: Note[] = [
    {
      id: 'n-1',
      title: 'Prompt testowy',
      content: 'Witaj świecie w notatniku',
      categoryId: 'sub-1',
      createdAt: '2026-08-22T10:00:00Z',
      updatedAt: '2026-08-22T10:00:00Z',
      sortOrder: 0,
      isPinned: true,
      isArchived: false,
    },
    {
      id: 'n-2',
      title: 'Druga notatka',
      content: 'Jakaś krótka treść',
      categoryId: 'cat-2',
      createdAt: '2026-08-22T10:00:00Z',
      updatedAt: '2026-08-22T10:00:00Z',
      sortOrder: 1,
      isPinned: false,
      isArchived: false,
    },
  ];

  describe('getPluralForm helper', () => {
    it('returns singular form for 1', () => {
      expect(getPluralForm(1, 'notatka', 'notatki', 'notatek')).toBe('notatka');
      expect(getPluralForm(1, 'kategoria', 'kategorie', 'kategorii')).toBe('kategoria');
    });

    it('returns few form for 2, 3, 4, 22, 23, 24', () => {
      expect(getPluralForm(2, 'notatka', 'notatki', 'notatek')).toBe('notatki');
      expect(getPluralForm(4, 'notatka', 'notatki', 'notatek')).toBe('notatki');
      expect(getPluralForm(22, 'notatka', 'notatki', 'notatek')).toBe('notatki');
      expect(getPluralForm(24, 'kategoria', 'kategorie', 'kategorii')).toBe('kategorie');
    });

    it('returns many form for 0, 5, 11, 12, 14, 20, 21, 25', () => {
      expect(getPluralForm(0, 'notatka', 'notatki', 'notatek')).toBe('notatek');
      expect(getPluralForm(5, 'notatka', 'notatki', 'notatek')).toBe('notatek');
      expect(getPluralForm(11, 'notatka', 'notatki', 'notatek')).toBe('notatek');
      expect(getPluralForm(12, 'notatka', 'notatki', 'notatek')).toBe('notatek');
      expect(getPluralForm(14, 'notatka', 'notatki', 'notatek')).toBe('notatek');
      expect(getPluralForm(20, 'notatka', 'notatki', 'notatek')).toBe('notatek');
      expect(getPluralForm(21, 'notatka', 'notatki', 'notatek')).toBe('notatek');
      expect(getPluralForm(25, 'kategoria', 'kategorie', 'kategorii')).toBe('kategorii');
    });
  });

  it('renders brand identity, main heading, subtitle, notes counter, categories counter, and breadcrumb', () => {
    render(
      <TopMenu
        categories={mockCategories}
        selectedCategoryId={null}
        onSelectCategory={vi.fn()}
        notesCount={42}
        isFormOpen={false}
        onToggleAddNote={vi.fn()}
        onOpenSettings={vi.fn()}
        healthError={null}
      />
    );

    expect(screen.getByText('Servanda')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Prywatne notatki' })).toBeInTheDocument();
    expect(screen.getByText('Przeglądaj notatki pogrupowane w kategorie i podkategorie')).toBeInTheDocument();
    expect(screen.getByText('Wszystkie notatki')).toBeInTheDocument();
    expect(screen.getByTestId('top-menu-notes-count')).toHaveTextContent('42 notatki');
    expect(screen.getByTestId('top-menu-categories-count')).toHaveTextContent('3 kategorie');
    expect(screen.getByText('SQLite: OK')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Dodaj notatkę/i })).toBeInTheDocument();
  });

  it('renders rich statistics when notes array is provided (pinned notes, word count, categories breakdown)', () => {
    render(
      <TopMenu
        categories={mockCategories}
        selectedCategoryId={null}
        onSelectCategory={vi.fn()}
        notes={mockNotes}
        isFormOpen={false}
        onToggleAddNote={vi.fn()}
        onOpenSettings={vi.fn()}
        healthError={null}
      />
    );

    // Notes count
    expect(screen.getByTestId('top-menu-notes-count')).toHaveTextContent('2 notatki');

    // Categories count
    expect(screen.getByTestId('top-menu-categories-count')).toHaveTextContent('3 kategorie');

    // Pinned notes pill
    expect(screen.getByTestId('top-menu-pinned-count')).toHaveTextContent('1 przypięta');

    // Words count pill: 4 words in n-1 ("Prompt testowy Witaj świecie w notatniku" -> 6 words) + 5 words in n-2 ("Druga notatka Jakaś krótka treść" -> 5 words) = 11 words
    expect(screen.getByTestId('top-menu-words-count')).toHaveTextContent('11 słów');
  });

  it('renders active category and subcategory in breadcrumb and handles clearing filter', () => {
    const onSelectCategory = vi.fn();
    render(
      <TopMenu
        categories={mockCategories}
        selectedCategoryId="sub-1"
        onSelectCategory={onSelectCategory}
        notesCount={10}
        isFormOpen={false}
        onToggleAddNote={vi.fn()}
        onOpenSettings={vi.fn()}
        healthError={null}
      />
    );

    // Parent in breadcrumb
    expect(screen.getByRole('button', { name: /Przejdź do: Prompty/i })).toBeInTheDocument();
    // Subcategory current
    expect(screen.getByTestId('breadcrumb-active-category')).toHaveTextContent('AI Kodowanie');

    // Click clear category filter button
    const clearBtn = screen.getByRole('button', { name: /Wyczyść filtr kategorii/i });
    fireEvent.click(clearBtn);
    expect(onSelectCategory).toHaveBeenCalledWith(null);
  });

  it('triggers onToggleAddNote and onOpenSettings when respective buttons are clicked', () => {
    const onToggleAddNote = vi.fn();
    const onOpenSettings = vi.fn();

    render(
      <TopMenu
        categories={mockCategories}
        selectedCategoryId={null}
        onSelectCategory={vi.fn()}
        notesCount={3}
        isFormOpen={false}
        onToggleAddNote={onToggleAddNote}
        onOpenSettings={onOpenSettings}
        healthError={null}
      />
    );

    const addBtn = screen.getByTestId('top-menu-add-note-btn');
    fireEvent.click(addBtn);
    expect(onToggleAddNote).toHaveBeenCalledTimes(1);

    const settingsBtn = screen.getByTestId('top-menu-settings-btn');
    fireEvent.click(settingsBtn);
    expect(onOpenSettings).toHaveBeenCalledTimes(1);
  });

  it('displays database error status when healthError is provided', () => {
    render(
      <TopMenu
        categories={mockCategories}
        selectedCategoryId={null}
        onSelectCategory={vi.fn()}
        notesCount={0}
        isFormOpen={false}
        onToggleAddNote={vi.fn()}
        onOpenSettings={vi.fn()}
        healthError="Brak połączenia"
      />
    );

    expect(screen.getByText('API / DB Błąd')).toBeInTheDocument();
  });
});
