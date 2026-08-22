export interface HealthStatus {
  status: string;
  database: string;
  noteCount: number;
  timestampUtc: string;
}

export interface Note {
  id: string;
  categoryId: string | null;
  title: string;
  content: string;
  createdAt: string;
  updatedAt: string;
  sortOrder: number;
  isPinned: boolean;
  isArchived: boolean;
}

export interface CreateNotePayload {
  title: string;
  content: string;
  categoryId?: string | null;
}

export async function fetchHealth(): Promise<HealthStatus> {
  const res = await fetch('/api/health');
  if (!res.ok) {
    throw new Error(`API health check failed with status ${res.status}`);
  }
  return res.json();
}

export async function fetchNotes(): Promise<Note[]> {
  const res = await fetch('/api/notes');
  if (!res.ok) {
    throw new Error(`Failed to fetch notes: ${res.status}`);
  }
  return res.json();
}

export async function createNote(payload: CreateNotePayload): Promise<Note> {
  const res = await fetch('/api/notes', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(payload),
  });

  if (!res.ok) {
    const errorData = await res.json().catch(() => null);
    const message = errorData?.title || `Failed to create note: ${res.status}`;
    throw new Error(message);
  }

  return res.json();
}

export async function reorderNotes(
  targetCategoryId: string | null,
  orderedNoteIds: string[]
): Promise<Note[]> {
  const res = await fetch('/api/notes/reorder', {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ targetCategoryId, orderedNoteIds }),
  });

  if (!res.ok) {
    const errorData = await res.json().catch(() => null);
    const message = errorData?.title || `Failed to reorder notes: ${res.status}`;
    throw new Error(message);
  }

  return res.json();
}

export async function moveNote(
  noteId: string,
  targetCategoryId: string | null,
  newSortOrder?: number
): Promise<Note[]> {
  const res = await fetch(`/api/notes/${noteId}/move`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ targetCategoryId, newSortOrder }),
  });

  if (!res.ok) {
    const errorData = await res.json().catch(() => null);
    const message = errorData?.title || `Failed to move note: ${res.status}`;
    throw new Error(message);
  }

  return res.json();
}

export interface Category {
  id: string;
  name: string;
  color: string | null;
  sortOrder: number;
  parentCategoryId?: string | null;
}

export async function fetchCategories(): Promise<Category[]> {
  const res = await fetch('/api/categories');
  if (!res.ok) {
    throw new Error(`Failed to fetch categories: ${res.status}`);
  }
  return res.json();
}

export async function reorderCategories(orderedIds: string[]): Promise<Category[]> {
  const res = await fetch('/api/categories/reorder', {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ orderedIds }),
  });

  if (!res.ok) {
    const errorData = await res.json().catch(() => null);
    const message = errorData?.title || `Failed to reorder categories: ${res.status}`;
    throw new Error(message);
  }

  return res.json();
}

export async function updateCategory(
  id: string,
  payload: { name: string; color?: string | null }
): Promise<Category> {
  const res = await fetch(`/api/categories/${id}`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(payload),
  });

  if (!res.ok) {
    const errorData = await res.json().catch(() => null);
    const message = errorData?.title || `Failed to update category: ${res.status}`;
    throw new Error(message);
  }

  return res.json();
}

