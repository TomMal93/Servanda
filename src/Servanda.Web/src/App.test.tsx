import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import App from './App';
import * as api from './api';

vi.mock('./api', () => ({
  fetchHealth: vi.fn(),
  fetchNotes: vi.fn(),
  createNote: vi.fn(),
  fetchCategories: vi.fn(),
  reorderCategories: vi.fn(),
}));

describe('App Component', () => {
  const mockCategories = [
    { id: 'cat-1', name: 'Prompty', color: null, sortOrder: 0 },
    { id: 'cat-2', name: 'Notatki', color: null, sortOrder: 1 },
    { id: 'cat-3', name: 'Rodzina', color: null, sortOrder: 2 },
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
        isPinned: false,
        isArchived: false,
      },
    ]);

    vi.mocked(api.fetchCategories).mockResolvedValue(mockCategories);
  });

  it('renders header, status cards, and sidebar categories', async () => {
    render(<App />);

    expect(screen.getByRole('heading', { level: 1, name: 'Servanda' })).toBeInTheDocument();
    expect(screen.getByText('Przeglądarka / Frontend')).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText(/Notatka testowa/)).toBeInTheDocument();
      expect(screen.getByText(/SQLite \(data\/servanda\.db, 1 notatek\)/)).toBeInTheDocument();
    });

    // Check categories sidebar
    expect(screen.getByRole('complementary', { name: /Kategorie/i })).toBeInTheDocument();
    expect(screen.getByText('Prompty')).toBeInTheDocument();
    expect(screen.getByText('Notatki')).toBeInTheDocument();
    expect(screen.getByText('Rodzina')).toBeInTheDocument();
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
      expect(screen.getByText('Prompty')).toBeInTheDocument();
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
      expect(api.reorderCategories).toHaveBeenCalledWith(['cat-2', 'cat-1', 'cat-3']);
    });
  });

  it('does not display drop line when item stays in its original place', async () => {
    render(<App />);

    await waitFor(() => {
      expect(screen.getByText('Prompty')).toBeInTheDocument();
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
      expect(screen.getByText('Prompty')).toBeInTheDocument();
    });

    const cardPrompty = screen.getByTestId('category-card-cat-1');
    const cardNotatki = screen.getByTestId('category-card-cat-2');
    const cardRodzina = screen.getByTestId('category-card-cat-3');

    expect(cardPrompty.style.getPropertyValue('--cat-color')).toBe('#a855f7');
    expect(cardNotatki.style.getPropertyValue('--cat-color')).toBe('#38bdf8');
    expect(cardRodzina.style.getPropertyValue('--cat-color')).toBe('#f59e0b');
  });
});




