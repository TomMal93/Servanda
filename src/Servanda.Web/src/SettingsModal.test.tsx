import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SettingsModal } from './SettingsModal';

describe('SettingsModal', () => {
  const mockCategories = [
    { id: 'cat-1', name: 'Prompty', color: '#a855f7', sortOrder: 0 },
    { id: 'cat-2', name: 'Notatki', color: '#38bdf8', sortOrder: 1 },
  ];

  it('does not render when isOpen is false', () => {
    render(<SettingsModal isOpen={false} onClose={vi.fn()} categories={mockCategories} />);
    expect(screen.queryByTestId('settings-modal-dialog')).not.toBeInTheDocument();
  });

  it('renders settings dialog with options menu when isOpen is true', () => {
    render(<SettingsModal isOpen={true} onClose={vi.fn()} categories={mockCategories} />);

    expect(screen.getByTestId('settings-modal-dialog')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: 'Ustawienia' })).toBeInTheDocument();

    // Option 1: Zarządzaj Kategoriami
    expect(screen.getByTestId('settings-option-categories')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 3, name: 'Zarządzaj Kategoriami' })).toBeInTheDocument();

    // Option 2: Zarządzaj Kaflem notatki
    expect(screen.getByTestId('settings-option-note-tile')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 3, name: 'Zarządzaj Kaflem notatki' })).toBeInTheDocument();
  });

  it('navigates to "Zarządzaj Kategoriami" view and back', () => {
    render(<SettingsModal isOpen={true} onClose={vi.fn()} categories={mockCategories} />);

    const categoriesOption = screen.getByTestId('settings-option-categories');
    fireEvent.click(categoriesOption);

    // Header updates to subview
    expect(screen.getByRole('heading', { level: 2, name: 'Zarządzaj Kategoriami' })).toBeInTheDocument();
    expect(screen.getByTestId('settings-subview-categories')).toBeInTheDocument();
    expect(screen.getByText('Prompty')).toBeInTheDocument();
    expect(screen.getByText('Notatki')).toBeInTheDocument();

    // Click back button
    const backBtn = screen.getByLabelText('Wróć do listy opcji');
    fireEvent.click(backBtn);

    // Returns to main menu
    expect(screen.getByRole('heading', { level: 2, name: 'Ustawienia' })).toBeInTheDocument();
    expect(screen.getByTestId('settings-option-categories')).toBeInTheDocument();
  });

  it('navigates to "Zarządzaj Kaflem notatki" view and back', () => {
    render(<SettingsModal isOpen={true} onClose={vi.fn()} categories={mockCategories} />);

    const noteTileOption = screen.getByTestId('settings-option-note-tile');
    fireEvent.click(noteTileOption);

    // Header updates to subview
    expect(screen.getByRole('heading', { level: 2, name: 'Zarządzaj Kaflem notatki' })).toBeInTheDocument();
    expect(screen.getByTestId('settings-subview-note-tile')).toBeInTheDocument();
    expect(screen.getByText('Podgląd kafelka notatki')).toBeInTheDocument();

    // Click footer back button
    const footerBackBtn = screen.getByRole('button', { name: /Powrót do menu opcji/i });
    fireEvent.click(footerBackBtn);

    // Returns to main menu
    expect(screen.getByRole('heading', { level: 2, name: 'Ustawienia' })).toBeInTheDocument();
  });

  it('calls onClose when close button or Gotowe is clicked', () => {
    const handleClose = vi.fn();
    render(<SettingsModal isOpen={true} onClose={handleClose} categories={mockCategories} />);

    const closeBtn = screen.getByLabelText('Zamknij ustawienia');
    fireEvent.click(closeBtn);
    expect(handleClose).toHaveBeenCalledTimes(1);

    const doneBtn = screen.getByRole('button', { name: 'Gotowe' });
    fireEvent.click(doneBtn);
    expect(handleClose).toHaveBeenCalledTimes(2);
  });

  it('closes on Escape key press when on menu view', () => {
    const handleClose = vi.fn();
    render(<SettingsModal isOpen={true} onClose={handleClose} categories={mockCategories} />);

    fireEvent.keyDown(window, { key: 'Escape' });
    expect(handleClose).toHaveBeenCalledTimes(1);
  });
});
