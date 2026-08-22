import { render, screen, waitFor, fireEvent, within } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import App from './App';
import * as api from './api';

vi.mock('./api', () => ({
  fetchHealth: vi.fn(),
  fetchNotes: vi.fn(),
  createNote: vi.fn(),
  reorderNotes: vi.fn(),
  moveNote: vi.fn(),
  fetchCategories: vi.fn(),
  reorderCategories: vi.fn(),
  updateCategory: vi.fn(),
}));

describe('App Component', () => {
  const mockCategories = [
    { id: 'cat-1', name: 'Prompty', color: null, sortOrder: 0 },
    { id: 'cat-2', name: 'Notatki', color: null, sortOrder: 1 },
    { id: 'cat-3', name: 'Rodzina', color: null, sortOrder: 2 },
    { id: 'cat-4', name: 'Narzędzia', color: null, sortOrder: 3 },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.fetchHealth).mockResolvedValue({
      status: 'healthy',
      database: 'connected',
      noteCount: 1,
      timestampUtc: new Date().toISOString(),
    });

    vi.mocked(api.fetchNotes).mockResolvedValue([
      {
        id: '123e4567-e89b-12d3-a456-426614174000',
        categoryId: null,
        title: 'Notatka testowa',
        content: 'Treść testowa',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        sortOrder: 0,
        isPinned: false,
        isArchived: false,
      },
    ]);

    vi.mocked(api.fetchCategories).mockResolvedValue(mockCategories);
  });

  it('renders header, brand, and sidebar categories', async () => {
    render(<App />);

    expect(screen.getByRole('heading', { level: 1, name: 'Prywatne notatki' })).toBeInTheDocument();
    expect(screen.getByText('Servanda')).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText(/Notatka testowa/)).toBeInTheDocument();
    });

    // Check categories sidebar
    const sidebar = screen.getByRole('complementary', { name: /Kategorie/i });
    expect(sidebar).toBeInTheDocument();
    expect(within(sidebar).getByTestId('sidebar-all-categories-button')).toBeInTheDocument();
    expect(within(sidebar).getByText('Wyświetl wszystkie kategorie')).toBeInTheDocument();
    expect(within(sidebar).getByText('Prompty')).toBeInTheDocument();
    expect(within(sidebar).getByText('Notatki')).toBeInTheDocument();
    expect(within(sidebar).getByText('Rodzina')).toBeInTheDocument();
    expect(within(sidebar).getByText('Narzędzia')).toBeInTheDocument();
  });

  it('allows clicking "Wyświetl wszystkie kategorie" to clear category filter', async () => {
    render(<App />);

    await waitFor(() => {
      expect(screen.getByTestId('category-card-cat-1')).toBeInTheDocument();
    });

    const allCatBtn = screen.getByTestId('sidebar-all-categories-button');
    expect(allCatBtn).toHaveAttribute('aria-pressed', 'true');
    expect(allCatBtn.classList.contains('active')).toBe(true);

    // Select a category
    const catPromptyBtn = within(screen.getByTestId('category-card-cat-1')).getByRole('button', { name: /Prompty/i });
    fireEvent.click(catPromptyBtn);

    // Now all-categories button is inactive
    expect(allCatBtn).toHaveAttribute('aria-pressed', 'false');
    expect(allCatBtn.classList.contains('active')).toBe(false);

    // Filter indicator should be shown
    expect(screen.getByText(/Wyświetlanie:/i)).toBeInTheDocument();

    // Click "Wyświetl wszystkie kategorie"
    fireEvent.click(allCatBtn);

    // Now all-categories button is active again and filter is cleared
    expect(allCatBtn).toHaveAttribute('aria-pressed', 'true');
    expect(allCatBtn.classList.contains('active')).toBe(true);
    expect(screen.queryByText(/Wyświetlanie:/i)).not.toBeInTheDocument();
  });

  it('highlights subcategories on sidebar when parent category is selected', async () => {
    const categoriesWithSub = [
      { id: 'cat-parent', name: 'Projekty', color: null, sortOrder: 0 },
      { id: 'cat-sub-1', name: 'Web', color: null, sortOrder: 0, parentCategoryId: 'cat-parent' },
      { id: 'cat-sub-2', name: 'Api', color: null, sortOrder: 1, parentCategoryId: 'cat-parent' },
      { id: 'cat-other', name: 'Inne', color: null, sortOrder: 1 },
    ];
    vi.mocked(api.fetchCategories).mockResolvedValue(categoriesWithSub);

    render(<App />);

    await waitFor(() => {
      expect(screen.getByTestId('category-card-cat-parent')).toBeInTheDocument();
      expect(screen.getByTestId('category-card-cat-sub-1')).toBeInTheDocument();
      expect(screen.getByTestId('category-card-cat-sub-2')).toBeInTheDocument();
    });

    const parentCard = screen.getByTestId('category-card-cat-parent');
    const subCard1 = screen.getByTestId('category-card-cat-sub-1');
    const subCard2 = screen.getByTestId('category-card-cat-sub-2');
    const otherCard = screen.getByTestId('category-card-cat-other');

    // Initially none are active
    expect(parentCard.classList.contains('active')).toBe(false);
    expect(subCard1.classList.contains('active')).toBe(false);
    expect(subCard2.classList.contains('active')).toBe(false);

    // Click parent category
    fireEvent.click(within(parentCard).getByRole('button', { name: /Projekty/i }));

    // Parent and all its subcategories should be active (highlighted)
    expect(parentCard.classList.contains('active')).toBe(true);
    expect(subCard1.classList.contains('active')).toBe(true);
    expect(subCard2.classList.contains('active')).toBe(true);
    expect(otherCard.classList.contains('active')).toBe(false);
  });

  it('allows reordering categories via drag and drop and calls reorderCategories API', async () => {
    vi.mocked(api.reorderCategories).mockImplementation(async (orderedIds) => {
      return orderedIds.map((id, index) => {
        const cat = mockCategories.find((c) => c.id === id)!;
        return { ...cat, sortOrder: index };
      });
    });

    render(<App />);

    await waitFor(() => {
      expect(screen.getByTestId('category-card-cat-1')).toBeInTheDocument();
    });

    const cardPrompty = screen.getByTestId('category-card-cat-1');
    const cardNotatki = screen.getByTestId('category-card-cat-2');

    // Mock getBoundingClientRect
    cardNotatki.getBoundingClientRect = vi.fn(() => ({
      top: 100,
      bottom: 150,
      left: 0,
      right: 200,
      width: 200,
      height: 50,
      x: 0,
      y: 100,
      toJSON: () => {},
    }));

    const dataTransfer = {
      setData: vi.fn(),
      getData: vi.fn(() => '0'),
      effectAllowed: '',
      dropEffect: '',
    };

    fireEvent.dragStart(cardPrompty, { dataTransfer });
    // clientY = 140 is in the bottom half of top:100, height:50 (ratio = 40/50 = 0.8 >= 0.55)
    fireEvent.dragOver(cardNotatki, { dataTransfer, clientY: 140 });
    fireEvent.drop(cardNotatki, { dataTransfer });
    fireEvent.dragEnd(cardPrompty);

    await waitFor(() => {
      expect(api.reorderCategories).toHaveBeenCalledWith(['cat-2', 'cat-1', 'cat-3', 'cat-4']);
    });
  });

  it('does not display drop line when item stays in its original place', async () => {
    render(<App />);

    await waitFor(() => {
      expect(screen.getByTestId('category-card-cat-1')).toBeInTheDocument();
    });

    const cardPrompty = screen.getByTestId('category-card-cat-1');

    cardPrompty.getBoundingClientRect = vi.fn(() => ({
      top: 100,
      bottom: 150,
      left: 0,
      right: 200,
      width: 200,
      height: 50,
      x: 0,
      y: 100,
      toJSON: () => {},
    }));

    const dataTransfer = {
      setData: vi.fn(),
      getData: vi.fn(() => '0'),
      effectAllowed: '',
      dropEffect: '',
    };

    fireEvent.dragStart(cardPrompty, { dataTransfer });
    // Dragging over itself in lower half (index 0 -> destIndex 0)
    fireEvent.dragOver(cardPrompty, { dataTransfer, clientY: 140 });

    // No drop indicator line should be rendered
    expect(document.querySelector('.drop-indicator-line')).toBeNull();
  });

  it('applies distinct colors to categories', async () => {
    render(<App />);

    await waitFor(() => {
      expect(screen.getByTestId('category-card-cat-1')).toBeInTheDocument();
    });

    const cardPrompty = screen.getByTestId('category-card-cat-1');
    const cardNotatki = screen.getByTestId('category-card-cat-2');
    const cardRodzina = screen.getByTestId('category-card-cat-3');
    const cardNarzedzia = screen.getByTestId('category-card-cat-4');

    expect(cardPrompty.style.getPropertyValue('--cat-color')).toBe('#a855f7');
    expect(cardNotatki.style.getPropertyValue('--cat-color')).toBe('#38bdf8');
    expect(cardRodzina.style.getPropertyValue('--cat-color')).toBe('#f59e0b');
    expect(cardNarzedzia.style.getPropertyValue('--cat-color')).toBe('#10b981');
  });

  it('allows moving note to category by dropping onto sidebar category card', async () => {
    vi.mocked(api.reorderNotes).mockResolvedValue([
      {
        id: '123e4567-e89b-12d3-a456-426614174000',
        categoryId: 'cat-1',
        title: 'Notatka testowa',
        content: 'Treść testowa',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        sortOrder: 0,
        isPinned: false,
        isArchived: false,
      },
    ]);

    render(<App />);

    await waitFor(() => {
      expect(screen.getByTestId('note-tile-123e4567-e89b-12d3-a456-426614174000')).toBeInTheDocument();
    });

    const noteTile = screen.getByTestId('note-tile-123e4567-e89b-12d3-a456-426614174000');
    const cardPrompty = screen.getByTestId('category-card-cat-1');

    const dataTransfer = {
      setData: vi.fn(),
      getData: vi.fn((type) => (type === 'application/x-servanda-note' ? '123e4567-e89b-12d3-a456-426614174000' : '')),
      effectAllowed: '',
      dropEffect: '',
    };

    fireEvent.dragStart(noteTile, { dataTransfer });
    fireEvent.dragOver(cardPrompty, { dataTransfer });
    fireEvent.drop(cardPrompty, { dataTransfer });
    fireEvent.dragEnd(noteTile);

    await waitFor(() => {
      expect(api.reorderNotes).toHaveBeenCalledWith('cat-1', ['123e4567-e89b-12d3-a456-426614174000']);
    });
  });

  it('renders settings button at the bottom of the sidebar and opens settings panel on click', async () => {
    render(<App />);

    await waitFor(() => {
      expect(screen.getByTestId('category-card-cat-1')).toBeInTheDocument();
    });

    const settingsBtn = screen.getByTestId('sidebar-settings-button');
    expect(settingsBtn).toBeInTheDocument();
    expect(within(settingsBtn).getByText('Ustawienia')).toBeInTheDocument();

    // Settings dialog is not initially visible
    expect(screen.queryByTestId('settings-modal-dialog')).not.toBeInTheDocument();

    // Click settings button
    fireEvent.click(settingsBtn);

    // Modal opens with options
    expect(screen.getByTestId('settings-modal-dialog')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: 'Ustawienia' })).toBeInTheDocument();

    // Option 1: Zarządzaj Kategoriami
    expect(screen.getByTestId('settings-option-categories')).toBeInTheDocument();
    expect(screen.getByText('Zarządzaj Kategoriami')).toBeInTheDocument();

    // Option 2: Zarządzaj Kaflem notatki
    expect(screen.getByTestId('settings-option-note-tile')).toBeInTheDocument();
    expect(screen.getByText('Zarządzaj Kaflem notatki')).toBeInTheDocument();

    // Close modal
    fireEvent.click(screen.getByRole('button', { name: 'Gotowe' }));
    expect(screen.queryByTestId('settings-modal-dialog')).not.toBeInTheDocument();
  });

  it('allows editing category name in settings modal and updates category in state and sidebar', async () => {
    vi.mocked(api.updateCategory).mockResolvedValue({
      id: 'cat-1',
      name: 'Prompty AI',
      color: null,
      sortOrder: 0,
    });

    render(<App />);

    await waitFor(() => {
      expect(screen.getByTestId('category-card-cat-1')).toBeInTheDocument();
    });

    // Open settings
    fireEvent.click(screen.getByTestId('sidebar-settings-button'));

    // Open category management
    fireEvent.click(screen.getByTestId('settings-option-categories'));

    // Click category name to start inline editing
    const nameBtn = screen.getByTestId('category-name-btn-cat-1');
    fireEvent.click(nameBtn);

    // Edit input
    const input = screen.getByTestId('category-edit-input-cat-1');
    fireEvent.change(input, { target: { value: 'Prompty AI' } });

    // Save
    fireEvent.click(screen.getByTestId('category-save-btn-cat-1'));

    await waitFor(() => {
      expect(api.updateCategory).toHaveBeenCalledWith('cat-1', { name: 'Prompty AI' });
    });

    // Close settings modal
    fireEvent.click(screen.getByRole('button', { name: 'Gotowe' }));

    // Sidebar should reflect updated category name
    await waitFor(() => {
      const sidebarCard = screen.getByTestId('category-card-cat-1');
      expect(within(sidebarCard).getByText('Prompty AI')).toBeInTheDocument();
    });
  });
});






