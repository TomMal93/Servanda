import { render, screen, fireEvent, waitFor } from '@testing-library/react';
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

    // Ensure cat-order-badge is removed
    expect(screen.queryByText(/Kolejność:/i)).not.toBeInTheDocument();

    // Click back button
    const backBtn = screen.getByLabelText('Wróć do listy opcji');
    fireEvent.click(backBtn);

    // Returns to main menu
    expect(screen.getByRole('heading', { level: 2, name: 'Ustawienia' })).toBeInTheDocument();
    expect(screen.getByTestId('settings-option-categories')).toBeInTheDocument();
  });

  it('allows reordering categories via drag and drop in settings modal', () => {
    const handleReorder = vi.fn();
    render(
      <SettingsModal
        isOpen={true}
        onClose={vi.fn()}
        categories={mockCategories}
        onReorderCategories={handleReorder}
      />
    );

    // Go to categories subview
    fireEvent.click(screen.getByTestId('settings-option-categories'));

    const itemPrompty = screen.getByTestId('settings-category-item-cat-1');
    const itemNotatki = screen.getByTestId('settings-category-item-cat-2');

    itemNotatki.getBoundingClientRect = vi.fn(() => ({
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

    fireEvent.dragStart(itemPrompty, { dataTransfer });
    fireEvent.dragOver(itemNotatki, { dataTransfer, clientY: 140 });
    fireEvent.drop(itemNotatki, { dataTransfer });
    fireEvent.dragEnd(itemPrompty);

    expect(handleReorder).toHaveBeenCalledWith(0, 1);
  });

  it('starts category name editing on clicking category name and saves updated name', async () => {
    const handleUpdate = vi.fn().mockResolvedValue(undefined);
    render(
      <SettingsModal
        isOpen={true}
        onClose={vi.fn()}
        categories={mockCategories}
        onUpdateCategory={handleUpdate}
      />
    );

    // Go to categories subview
    fireEvent.click(screen.getByTestId('settings-option-categories'));

    // Click on category name button
    const nameBtn = screen.getByTestId('category-name-btn-cat-1');
    expect(nameBtn).toBeInTheDocument();
    fireEvent.click(nameBtn);

    // Input should be visible
    const input = screen.getByTestId('category-edit-input-cat-1') as HTMLInputElement;
    expect(input).toBeInTheDocument();
    expect(input.value).toBe('Prompty');

    // Change value
    fireEvent.change(input, { target: { value: 'Prompty AI' } });
    expect(input.value).toBe('Prompty AI');

    // Submit form / click save
    const saveBtn = screen.getByTestId('category-save-btn-cat-1');
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(handleUpdate).toHaveBeenCalledWith('cat-1', 'Prompty AI');
    });
  });

  it('cancels category name editing on cancel button click or Escape', () => {
    const handleUpdate = vi.fn();
    render(
      <SettingsModal
        isOpen={true}
        onClose={vi.fn()}
        categories={mockCategories}
        onUpdateCategory={handleUpdate}
      />
    );

    // Go to categories subview
    fireEvent.click(screen.getByTestId('settings-option-categories'));

    // Start editing
    fireEvent.click(screen.getByTestId('category-name-btn-cat-1'));
    expect(screen.getByTestId('category-edit-input-cat-1')).toBeInTheDocument();

    // Click cancel button
    const cancelBtn = screen.getByTestId('category-cancel-btn-cat-1');
    fireEvent.click(cancelBtn);

    // Input is closed, name button restored
    expect(screen.queryByTestId('category-edit-input-cat-1')).not.toBeInTheDocument();
    expect(screen.getByTestId('category-name-btn-cat-1')).toBeInTheDocument();
    expect(handleUpdate).not.toHaveBeenCalled();
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
