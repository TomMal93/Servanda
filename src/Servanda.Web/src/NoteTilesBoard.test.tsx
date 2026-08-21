import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { NoteTilesBoard, getPreviewSnippet } from './NoteTilesBoard';
import type { Category, Note } from './api';

describe('NoteTilesBoard Component', () => {
  const mockCategories: Category[] = [
    { id: 'cat-prompty', name: 'Prompty', color: '#a855f7', sortOrder: 0, parentCategoryId: null },
    { id: 'sub-kod', name: 'Generowanie kodu', color: '#ec4899', sortOrder: 0, parentCategoryId: 'cat-prompty' },
    { id: 'cat-notatki', name: 'Notatki', color: '#38bdf8', sortOrder: 1, parentCategoryId: null },
    { id: 'sub-praca', name: 'Praca', color: '#06b6d4', sortOrder: 0, parentCategoryId: 'cat-notatki' },
  ];

  const mockNotes: Note[] = [
    {
      id: 'note-1',
      categoryId: 'cat-prompty',
      title: 'Prompt główny',
      content: 'Jeden dwa trzy cztery pięć sześć siedem osiem dziewięć dziesięć jedenaście dwanaście trzynaście czternaście piętnaście szesnaście siedemnaście',
      createdAt: '2026-08-22T00:00:00.000Z',
      updatedAt: '2026-08-22T00:00:00.000Z',
      isPinned: false,
      isArchived: false,
    },
    {
      id: 'note-2',
      categoryId: 'sub-kod',
      title: 'Prompt do Reacta',
      content: 'Napisz komponent funkcyjny z hookami.',
      createdAt: '2026-08-22T00:00:00.000Z',
      updatedAt: '2026-08-22T00:00:00.000Z',
      isPinned: false,
      isArchived: false,
    },
    {
      id: 'note-3',
      categoryId: 'sub-praca',
      title: 'Zadania na poniedziałek',
      content: 'Przygotować raport i wysłać maile.',
      createdAt: '2026-08-22T00:00:00.000Z',
      updatedAt: '2026-08-22T00:00:00.000Z',
      isPinned: false,
      isArchived: false,
    },
    {
      id: 'note-4',
      categoryId: null,
      title: 'Notatka bez kategorii',
      content: 'Luźna myśl.',
      createdAt: '2026-08-22T00:00:00.000Z',
      updatedAt: '2026-08-22T00:00:00.000Z',
      isPinned: false,
      isArchived: false,
    },
  ];

  it('renders category and subcategory headings and note tiles', () => {
    render(
      <NoteTilesBoard
        categories={mockCategories}
        notes={mockNotes}
        selectedCategoryId={null}
        onSelectCategory={vi.fn()}
      />
    );

    // Root headings
    expect(screen.getByRole('heading', { level: 2, name: 'Prompty' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: 'Notatki' })).toBeInTheDocument();

    // Subcategory headings
    expect(screen.getByRole('heading', { level: 3, name: 'Generowanie kodu' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 3, name: 'Praca' })).toBeInTheDocument();

    // Uncategorized section
    expect(screen.getByRole('heading', { level: 2, name: 'Bez kategorii' })).toBeInTheDocument();

    // Note tiles
    expect(screen.getByText('Prompt główny')).toBeInTheDocument();
    expect(screen.getByText('Prompt do Reacta')).toBeInTheDocument();
    expect(screen.getByText('Zadania na poniedziałek')).toBeInTheDocument();
    expect(screen.getByText('Notatka bez kategorii')).toBeInTheDocument();
  });

  it('applies correct category and subcategory colors to sections and tiles', () => {
    render(
      <NoteTilesBoard
        categories={mockCategories}
        notes={mockNotes}
        selectedCategoryId={null}
        onSelectCategory={vi.fn()}
      />
    );

    const promptySection = screen.getByTestId('category-section-cat-prompty');
    expect(promptySection.style.getPropertyValue('--cat-color')).toBe('#a855f7');

    const subKodSection = screen.getByTestId('subcategory-section-sub-kod');
    expect(subKodSection.style.getPropertyValue('--subcat-color')).toBe('#ec4899');

    const tilePromptGłówny = screen.getByTestId('note-tile-note-1');
    expect(tilePromptGłówny.style.getPropertyValue('--tile-border-color')).toBe('#a855f7');

    const tileSubKod = screen.getByTestId('note-tile-note-2');
    expect(tileSubKod.style.getPropertyValue('--tile-border-color')).toBe('#ec4899');

    const tileSubPraca = screen.getByTestId('note-tile-note-3');
    expect(tileSubPraca.style.getPropertyValue('--tile-border-color')).toBe('#06b6d4');
  });

  it('truncates content in snippet correctly', () => {
    const snippet = getPreviewSnippet(mockNotes[0].content, 15);
    expect(snippet.endsWith('...')).toBe(true);
    expect(snippet.split(' ').length).toBe(15);

    const shortSnippet = getPreviewSnippet('Krótki tekst.', 15);
    expect(shortSnippet).toBe('Krótki tekst.');
  });

  it('allows clearing filter when category is selected', () => {
    const onSelectCategory = vi.fn();
    render(
      <NoteTilesBoard
        categories={mockCategories}
        notes={mockNotes}
        selectedCategoryId="cat-prompty"
        onSelectCategory={onSelectCategory}
      />
    );

    // Only Prompty should be shown
    expect(screen.getByRole('heading', { level: 2, name: 'Prompty' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 2, name: 'Notatki' })).not.toBeInTheDocument();

    // Click clear filter button
    const clearBtn = screen.getByRole('button', { name: /Pokaż wszystkie kategorie/i });
    fireEvent.click(clearBtn);

    expect(onSelectCategory).toHaveBeenCalledWith(null);
  });
});
