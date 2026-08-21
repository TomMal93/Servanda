import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import App from './App';
import * as api from './api';

vi.mock('./api', () => ({
  fetchHealth: vi.fn(),
  fetchNotes: vi.fn(),
  createNote: vi.fn(),
}));

describe('App Component', () => {
  it('renders header and initial cards', async () => {
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

    render(<App />);

    expect(screen.getByText('Servanda')).toBeInTheDocument();
    expect(screen.getByText('Przeglądarka / Frontend')).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText(/Notatka testowa/)).toBeInTheDocument();
      expect(screen.getByText(/SQLite \(data\/servanda\.db, 1 notatek\)/)).toBeInTheDocument();
    });
  });
});
