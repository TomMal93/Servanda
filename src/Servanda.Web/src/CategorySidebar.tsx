import React, { useState, useRef, useLayoutEffect } from 'react';
import type { Category } from './api';

interface CategorySidebarProps {
  categories: Category[];
  selectedCategoryId: string | null;
  onSelectCategory: (categoryId: string | null) => void;
  onReorder: (sourceIndex: number, targetIndex: number) => void;
  loading?: boolean;
  error?: string | null;
}

interface DropTarget {
  index: number;
  position: 'before' | 'after';
}

function getCategoryIcon(name: string) {
  const normalized = name.toLowerCase().trim();
  if (normalized.includes('prompt')) {
    return (
      <svg
        className="category-icon"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        aria-hidden="true"
      >
        <polyline points="4 17 10 11 4 5" />
        <line x1="12" y1="19" x2="20" y2="19" />
      </svg>
    );
  }

  if (normalized.includes('notatk') || normalized.includes('note')) {
    return (
      <svg
        className="category-icon"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        aria-hidden="true"
      >
        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
        <polyline points="14 2 14 8 20 8" />
        <line x1="16" y1="13" x2="8" y2="13" />
        <line x1="16" y1="17" x2="8" y2="17" />
        <polyline points="10 9 9 9 8 9" />
      </svg>
    );
  }

  if (normalized.includes('rodzin') || normalized.includes('family')) {
    return (
      <svg
        className="category-icon"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        aria-hidden="true"
      >
        <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
        <circle cx="9" cy="7" r="4" />
        <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
        <path d="M16 3.13a4 4 0 0 1 0 7.75" />
      </svg>
    );
  }

  if (normalized.includes('narzędz') || normalized.includes('narzedz') || normalized.includes('tool')) {
    return (
      <svg
        className="category-icon"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        aria-hidden="true"
      >
        <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z" />
      </svg>
    );
  }

  return (
    <svg
      className="category-icon"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
    </svg>
  );
}

function getCategoryColor(category: Category, index: number): string {
  if (category.color && category.color.trim()) {
    return category.color.trim();
  }
  const normalized = category.name.toLowerCase().trim();
  if (normalized.includes('prompt')) {
    return '#a855f7'; // Purple / Fioletowy
  }
  if (normalized.includes('notatk') || normalized.includes('note')) {
    return '#38bdf8'; // Sky Blue / Niebieski
  }
  if (normalized.includes('rodzin') || normalized.includes('family')) {
    return '#f59e0b'; // Amber / Bursztynowy
  }
  if (normalized.includes('narzędz') || normalized.includes('narzedz') || normalized.includes('tool')) {
    return '#10b981'; // Emerald / Zielony
  }

  const fallbackPalette = ['#10b981', '#ec4899', '#06b6d4', '#8b5cf6', '#f97316', '#14b8a6', '#6366f1'];
  return fallbackPalette[index % fallbackPalette.length];
}

function getDestinationIndex(source: number, target: number, position: 'before' | 'after'): number {
  let dest = position === 'after' ? target + 1 : target;
  if (source < dest) {
    dest -= 1;
  }
  return dest;
}


export const CategorySidebar: React.FC<CategorySidebarProps> = ({
  categories,
  selectedCategoryId,
  onSelectCategory,
  onReorder,
  loading,
  error,
}) => {
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);
  const [dropTarget, setDropTarget] = useState<DropTarget | null>(null);

  // References for FLIP smooth movement animation
  const itemRefs = useRef<Map<string, HTMLLIElement>>(new Map());
  const prevPositions = useRef<Map<string, number>>(new Map());

  useLayoutEffect(() => {
    const prevPosMap = prevPositions.current;
    categories.forEach((cat) => {
      const el = itemRefs.current.get(cat.id);
      if (el) {
        const currentTop = el.getBoundingClientRect().top;
        const oldTop = prevPosMap.get(cat.id);
        if (oldTop !== undefined && oldTop !== currentTop) {
          const deltaY = oldTop - currentTop;
          el.style.transform = `translateY(${deltaY}px)`;
          el.style.transition = 'none';

          requestAnimationFrame(() => {
            el.style.transition = 'transform 0.28s cubic-bezier(0.2, 0, 0, 1)';
            el.style.transform = '';
          });
        }
        prevPosMap.set(cat.id, currentTop);
      }
    });
  }, [categories]);

  function handleDragStart(e: React.DragEvent, index: number) {
    setDraggedIndex(index);
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', index.toString());
  }


  function handleDragOver(e: React.DragEvent, index: number) {
    e.preventDefault();
    if (draggedIndex === null) return;

    e.dataTransfer.dropEffect = 'move';

    const rect = e.currentTarget.getBoundingClientRect();
    const ratio = (e.clientY - rect.top) / rect.height;

    let position: 'before' | 'after' | null = null;

    // Musi znaleźć się zdecydowanie za połową kafla (np. ratio >= 0.55) aby wyświetlić linię za nim
    if (ratio >= 0.55) {
      position = 'after';
    } else if (index === 0 && ratio <= 0.35) {
      // Dla pierwszego elementu umożliwiamy także wrzucenie na sam początek
      position = 'before';
    }

    if (!position) {
      if (dropTarget !== null) setDropTarget(null);
      return;
    }

    const destIndex = getDestinationIndex(draggedIndex, index, position);
    // Jeśli kafel ma zostać na swoim dotychczasowym miejscu, nie wyświetlaj linii
    if (destIndex === draggedIndex) {
      if (dropTarget !== null) setDropTarget(null);
      return;
    }

    if (dropTarget?.index !== index || dropTarget?.position !== position) {
      setDropTarget({ index, position });
    }
  }

  function handleDragLeave(e: React.DragEvent) {
    const relatedTarget = e.relatedTarget as Node | null;
    if (e.currentTarget.contains(relatedTarget)) {
      return;
    }
    setDropTarget(null);
  }

  function handleDrop(e: React.DragEvent, targetIndex: number) {
    e.preventDefault();
    if (draggedIndex === null) {
      setDropTarget(null);
      return;
    }

    let position = dropTarget?.position;
    let target = dropTarget ? dropTarget.index : targetIndex;

    if (!position) {
      const rect = e.currentTarget.getBoundingClientRect();
      const height = rect.height || 40;
      const ratio = (e.clientY - rect.top) / height;
      if (ratio >= 0.55 || rect.height === 0) {
        position = 'after';
      } else if (targetIndex === 0 && ratio <= 0.35) {
        position = 'before';
      } else {
        position = 'after';
      }
    }

    const newIndex = getDestinationIndex(draggedIndex, target, position);
    if (newIndex !== draggedIndex) {
      onReorder(draggedIndex, newIndex);
    }

    setDraggedIndex(null);
    setDropTarget(null);
  }

  function handleDragEnd() {
    setDraggedIndex(null);
    setDropTarget(null);
  }


  return (
    <aside className="category-sidebar-full" aria-label="Kategorie">
      <div className="sidebar-brand">
        <div className="brand-logo">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="brand-icon">
            <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20" />
            <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z" />
          </svg>
        </div>
        <div className="brand-text">
          <h2>Servanda</h2>
          <span className="brand-tagline">Prywatne notatki</span>
        </div>
      </div>

      <div className="sidebar-section-header">
        <span className="section-title">KATEGORIE</span>
        <span className="sidebar-count">{categories.length}</span>
      </div>

      {error && <div className="alert-error sidebar-alert">{error}</div>}

      {loading ? (
        <div className="empty-state">Ładowanie kategorii...</div>
      ) : categories.length === 0 ? (
        <div className="empty-state">Brak kategorii</div>
      ) : (
        <ul className="categories-list" role="list">
          {categories.map((category, index) => {
            const isSelected = selectedCategoryId === category.id;
            const isDragging = draggedIndex === index;
            const showLineBefore =
              dropTarget?.index === index && dropTarget?.position === 'before' && draggedIndex !== index;
            const showLineAfter =
              dropTarget?.index === index && dropTarget?.position === 'after' && draggedIndex !== index;

            const categoryColor = getCategoryColor(category, index);

            return (
              <li
                key={category.id}
                ref={(el) => {
                  if (el) {
                    itemRefs.current.set(category.id, el);
                  } else {
                    itemRefs.current.delete(category.id);
                  }
                }}
                className="category-item-container"
              >
                {showLineBefore && (
                  <div className="drop-indicator-line drop-line-before" aria-hidden="true" />
                )}

                <div
                  className={`category-card ${isSelected ? 'active' : ''} ${isDragging ? 'dragging' : ''}`}
                  style={{ '--cat-color': categoryColor } as React.CSSProperties}
                  draggable
                  onDragStart={(e) => handleDragStart(e, index)}
                  onDragOver={(e) => handleDragOver(e, index)}
                  onDragLeave={handleDragLeave}
                  onDrop={(e) => handleDrop(e, index)}
                  onDragEnd={handleDragEnd}
                  data-testid={`category-card-${category.id}`}
                  title="Przeciągnij i upuść za inny kafel"
                >
                  <div className="category-card-content">
                    <div className="drag-handle" aria-hidden="true" title="Chwyć i przeciągnij">
                      <svg viewBox="0 0 24 24" fill="currentColor" className="drag-icon">
                        <circle cx="9" cy="6" r="1.5" />
                        <circle cx="15" cy="6" r="1.5" />
                        <circle cx="9" cy="12" r="1.5" />
                        <circle cx="15" cy="12" r="1.5" />
                        <circle cx="9" cy="18" r="1.5" />
                        <circle cx="15" cy="18" r="1.5" />
                      </svg>
                    </div>

                    <button
                      type="button"
                      className="category-main-btn"
                      onClick={() => onSelectCategory(isSelected ? null : category.id)}
                      aria-pressed={isSelected}
                      title={`Filtruj wg kategorii: ${category.name}`}
                    >
                      <span className="category-icon-wrapper">
                        {getCategoryIcon(category.name)}
                      </span>
                      <span className="category-name">{category.name}</span>
                    </button>
                  </div>
                </div>

                {showLineAfter && (
                  <div className="drop-indicator-line drop-line-after" aria-hidden="true" />
                )}
              </li>
            );
          })}
        </ul>
      )}
    </aside>
  );
};

export default CategorySidebar;
