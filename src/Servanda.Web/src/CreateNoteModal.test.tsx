import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { CreateNoteModal, NOTE_TYPE_OPTIONS } from './CreateNoteModal';
import type { Category } from './api';

describe('CreateNoteModal Component', () => {
  const mockCategories: Category[] = [
    { id: 'cat-1', name: 'Prompty', color: '#a855f7', sortOrder: 0, parentCategoryId: null },
    { id: 'sub-1', name: 'AI Kodowanie', color: '#ec4899', sortOrder: 0, parentCategoryId: 'cat-1' },
    { id: 'cat-2', name: 'Notatki', color: '#38bdf8', sortOrder: 1, parentCategoryId: null },
  ];

  it('does not render when isOpen is false', () => {
    render(
      <CreateNoteModal
        isOpen={false}
        onClose={vi.fn()}
        categories={mockCategories}
        selectedCategoryId={null}
        onCreateNote={vi.fn()}
      />
    );

    expect(screen.queryByTestId('create-note-modal-dialog')).not.toBeInTheDocument();
  });

  it('renders modal header, all note type options, and default text note form when open', () => {
    render(
      <CreateNoteModal
        isOpen={true}
        onClose={vi.fn()}
        categories={mockCategories}
        selectedCategoryId="sub-1"
        onCreateNote={vi.fn()}
      />
    );

    expect(screen.getByTestId('create-note-modal-dialog')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: 'Utwórz nową notatkę' })).toBeInTheDocument();

    // Check all note type options are rendered
    NOTE_TYPE_OPTIONS.forEach((opt) => {
      expect(screen.getByTestId(`note-type-${opt.id}`)).toBeInTheDocument();
      expect(screen.getByText(opt.title)).toBeInTheDocument();
    });

    // Default is text form
    expect(screen.getByTestId('create-note-text-form')).toBeInTheDocument();
    expect(screen.getByTestId('create-note-title-input')).toBeInTheDocument();
    expect(screen.getByTestId('create-note-category-select')).toHaveValue('sub-1');
  });

  it('submits new note successfully and closes modal', async () => {
    const onCreateNote = vi.fn().mockResolvedValue({
      id: 'note-new',
      title: 'Nowy pomysł',
      content: 'Treść testowa Markdown',
      categoryId: 'cat-1',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      sortOrder: 0,
      isPinned: false,
      isArchived: false,
    });
    const onClose = vi.fn();

    render(
      <CreateNoteModal
        isOpen={true}
        onClose={onClose}
        categories={mockCategories}
        selectedCategoryId={null}
        onCreateNote={onCreateNote}
      />
    );

    const titleInput = screen.getByTestId('create-note-title-input');
    const contentInput = screen.getByTestId('create-note-content-input');
    const categorySelect = screen.getByTestId('create-note-category-select');

    fireEvent.change(titleInput, { target: { value: 'Nowy pomysł' } });
    fireEvent.change(contentInput, { target: { value: 'Treść testowa Markdown' } });
    fireEvent.change(categorySelect, { target: { value: 'cat-1' } });

    fireEvent.click(screen.getByTestId('create-note-submit-btn'));

    await waitFor(() => {
      expect(onCreateNote).toHaveBeenCalledWith({
        title: 'Nowy pomysł',
        content: 'Treść testowa Markdown',
        categoryId: 'cat-1',
      });
      expect(onClose).toHaveBeenCalled();
    });
  });

  it('displays error message if creating note fails', async () => {
    const onCreateNote = vi.fn().mockRejectedValue(new Error('Błąd zapisu bazy danych'));

    render(
      <CreateNoteModal
        isOpen={true}
        onClose={vi.fn()}
        categories={mockCategories}
        selectedCategoryId={null}
        onCreateNote={onCreateNote}
      />
    );

    fireEvent.change(screen.getByTestId('create-note-title-input'), {
      target: { value: 'Test' },
    });
    fireEvent.click(screen.getByTestId('create-note-submit-btn'));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Błąd zapisu bazy danych');
    });
  });

  it('switches between note types and displays placeholder panels with mockups', () => {
    render(
      <CreateNoteModal
        isOpen={true}
        onClose={vi.fn()}
        categories={mockCategories}
        selectedCategoryId={null}
        onCreateNote={vi.fn()}
      />
    );

    // Click Checklist option
    fireEvent.click(screen.getByTestId('note-type-checklist'));
    expect(screen.getByTestId('note-type-placeholder-checklist')).toBeInTheDocument();
    expect(screen.getByText('Podgląd planowanego interfejsu (Lista zadań (Checklista))')).toBeInTheDocument();

    // Click Code Snippet option
    fireEvent.click(screen.getByTestId('note-type-code'));
    expect(screen.getByTestId('note-type-placeholder-code')).toBeInTheDocument();
    expect(screen.getByText('Podgląd planowanego interfejsu (Fragment kodu (Snippet))')).toBeInTheDocument();

    // Click Bookmarks / Links option
    fireEvent.click(screen.getByTestId('note-type-links'));
    expect(screen.getByTestId('note-type-placeholder-links')).toBeInTheDocument();
    expect(screen.getByText('Podgląd planowanego interfejsu (Zbiór linków (Zakładki))')).toBeInTheDocument();

    // Click Canvas option
    fireEvent.click(screen.getByTestId('note-type-canvas'));
    expect(screen.getByTestId('note-type-placeholder-canvas')).toBeInTheDocument();
    expect(screen.getByText('Podgląd planowanego interfejsu (Tablica pomysłów (Szkic))')).toBeInTheDocument();

    // Switch back to text note via CTA button
    fireEvent.click(screen.getByTestId('switch-to-text-note-btn'));
    expect(screen.getByTestId('create-note-text-form')).toBeInTheDocument();
  });

  it('closes on Escape key press or close button click', () => {
    const onClose = vi.fn();
    render(
      <CreateNoteModal
        isOpen={true}
        onClose={onClose}
        categories={mockCategories}
        selectedCategoryId={null}
        onCreateNote={vi.fn()}
      />
    );

    fireEvent.click(screen.getByTestId('create-note-close-btn'));
    expect(onClose).toHaveBeenCalledTimes(1);

    fireEvent.keyDown(window, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(2);
  });
});
