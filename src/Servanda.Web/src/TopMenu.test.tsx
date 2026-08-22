import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { TopMenu } from './TopMenu';
import type { Category } from './api';

describe('TopMenu Component', () => {
  const mockCategories: Category[] = [
    { id: 'cat-1', name: 'Prompty', color: '#a855f7', sortOrder: 0, parentCategoryId: null },
    { id: 'sub-1', name: 'AI Kodowanie', color: '#ec4899', sortOrder: 0, parentCategoryId: 'cat-1' },
    { id: 'cat-2', name: 'Notatki', color: '#38bdf8', sortOrder: 1, parentCategoryId: null },
  ];

  it('renders brand identity, main heading, subtitle, notes counter, and default all notes breadcrumb', () => {
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
    expect(screen.getByText('42')).toBeInTheDocument();
    expect(screen.getByText('SQLite: OK')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Dodaj notatkę/i })).toBeInTheDocument();
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
