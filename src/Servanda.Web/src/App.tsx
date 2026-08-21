import { useState, useEffect, type FormEvent } from 'react';
import { fetchHealth, fetchNotes, createNote, type HealthStatus, type Note } from './api';

export function App() {
  const [health, setHealth] = useState<HealthStatus | null>(null);
  const [healthError, setHealthError] = useState<string | null>(null);
  const [notes, setNotes] = useState<Note[]>([]);
  const [notesLoading, setNotesLoading] = useState(true);
  const [notesError, setNotesError] = useState<string | null>(null);

  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  async function loadData() {
    try {
      setHealthError(null);
      const h = await fetchHealth();
      setHealth(h);
    } catch (err: any) {
      setHealthError(err.message || 'Brak połączenia z API');
    }

    try {
      setNotesError(null);
      setNotesLoading(true);
      const n = await fetchNotes();
      setNotes(n);
    } catch (err: any) {
      setNotesError(err.message || 'Błąd pobierania notatek');
    } finally {
      setNotesLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, []);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!title.trim()) return;

    setSubmitting(true);
    setSubmitError(null);

    try {
      const created = await createNote({
        title: title.trim(),
        content: content.trim(),
      });
      setNotes((prev) => [created, ...prev]);
      setTitle('');
      setContent('');

      // Refresh health check to update note count in DB
      fetchHealth().then(setHealth).catch(() => {});
    } catch (err: any) {
      setSubmitError(err.message || 'Nie udało się zapisać notatki.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="app-container">
      <header>
        <h1>Servanda</h1>
        <p className="subtitle">Weryfikacja komunikacji: Przeglądarka ➔ Backend ➔ Baza SQLite</p>
      </header>

      <section className="status-grid" aria-label="Status systemu">
        <div className="status-card">
          <h3>Przeglądarka / Frontend</h3>
          <div className="status-badge">
            <span className="dot"></span>
            <span>React + Vite (5173)</span>
          </div>
        </div>

        <div className="status-card">
          <h3>Backend API</h3>
          <div className="status-badge">
            <span className={`dot ${healthError ? 'error' : ''}`}></span>
            <span>{healthError ? 'Rozłączony' : health?.status === 'healthy' ? '.NET 10 (5180)' : 'Sprawdzanie...'}</span>
          </div>
        </div>

        <div className="status-card">
          <h3>Baza danych</h3>
          <div className="status-badge">
            <span className={`dot ${healthError || health?.database !== 'connected' ? 'error' : ''}`}></span>
            <span>
              {health?.database === 'connected'
                ? `SQLite (data/servanda.db, ${health.noteCount} notatek)`
                : healthError
                ? 'Brak danych'
                : 'Łączenie...'}
            </span>
          </div>
        </div>
      </section>

      {healthError && (
        <div className="alert-error" role="alert">
          <strong>Błąd komunikacji z backendem:</strong> {healthError}
        </div>
      )}

      <section className="section-card">
        <h2>Dodaj notatkę testową (Zapis do SQLite)</h2>
        {submitError && <div className="alert-error">{submitError}</div>}
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="note-title">Tytuł notatki</label>
            <input
              id="note-title"
              className="form-input"
              type="text"
              placeholder="np. Moja pierwsza notatka"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              disabled={submitting}
              required
            />
          </div>
          <div className="form-group">
            <label htmlFor="note-content">Treść notatki</label>
            <textarea
              id="note-content"
              className="form-textarea"
              rows={3}
              placeholder="Wpisz treść notatki..."
              value={content}
              onChange={(e) => setContent(e.target.value)}
              disabled={submitting}
            />
          </div>
          <button type="submit" className="btn-primary" disabled={submitting || !title.trim()}>
            {submitting ? 'Zapisywanie w SQLite...' : 'Zapisz notatkę w bazie'}
          </button>
        </form>
      </section>

      <section className="section-card">
        <h2>Notatki zapisane w bazie ({notes.length})</h2>
        {notesLoading ? (
          <p className="empty-state">Ładowanie notatek z bazy SQLite...</p>
        ) : notesError ? (
          <div className="alert-error">{notesError}</div>
        ) : notes.length === 0 ? (
          <p className="empty-state">Brak notatek w bazie. Użyj powyższego formularza, aby dodać pierwszy wpis.</p>
        ) : (
          <div className="notes-list">
            {notes.map((note) => (
              <article key={note.id} className="note-item">
                <h4>{note.title}</h4>
                {note.content && <p>{note.content}</p>}
                <div className="note-meta">
                  <span>ID: {note.id}</span> • <span>Utworzono: {new Date(note.createdAt).toLocaleString()}</span>
                </div>
              </article>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

export default App;
