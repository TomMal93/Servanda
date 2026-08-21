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
  isPinned: boolean;
  isArchived: boolean;
}

export interface CreateNotePayload {
  title: string;
  content: string;
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
