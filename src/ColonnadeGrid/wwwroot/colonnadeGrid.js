// Minimal JS interop for ColonnadeGrid: only what Blazor and CSS can't do on
// their own. See docs/architecture.md for the reasoning behind each.
//
// - Column resize: drag tracking and the live width preview happen here, so
//   drag frames never round-trip into Blazor. Only the final width goes to
//   .NET (ColonnadeGrid.OnColumnResizedAsync), whose re-render replaces the
//   inline styles written here.
// - Select-all: a checkbox's `indeterminate` property has no HTML attribute.
// - Dropdown positioning: position:fixed at measured coordinates, so a menu
//   isn't clipped by a host container's overflow.
// - Sticky-header shadow: CSS can't select "currently stuck", so an
//   IntersectionObserver watches a sentinel just above the header.

// Match GridState.DefaultMinColumnWidth/DefaultMaxColumnWidth, which clamp the
// committed width, so the column doesn't jump when the drag ends.
const MIN_COLUMN_WIDTH = 40;
const MAX_COLUMN_WIDTH = 2000;

// Splits a grid-template-columns value on top-level spaces only, keeping
// "minmax(140px, 1fr)" as one track.
function splitTracks(value) {
    const tracks = [];
    let depth = 0;
    let current = '';
    for (const ch of value.trim()) {
        if (ch === '(') {
            depth++;
        } else if (ch === ')') {
            depth--;
        }

        if (ch === ' ' && depth === 0) {
            if (current) {
                tracks.push(current);
                current = '';
            }
        } else {
            current += ch;
        }
    }
    if (current) {
        tracks.push(current);
    }
    return tracks;
}

export function initResize(container, dotNetRef) {
    let active = null;

    function onPointerDown(e) {
        const handle = e.target.closest('[data-resize-handle]');
        if (!handle) {
            return;
        }

        const headerCell = handle.closest('[data-column-id]');
        if (!headerCell) {
            return;
        }

        // Every row shares the header's track list, so the cell's position
        // among its siblings is also its track's index.
        const headerRow = headerCell.parentElement;
        const template = headerRow.style.gridTemplateColumns;

        active = {
            columnId: headerCell.getAttribute('data-column-id'),
            startX: e.clientX,
            startWidth: headerCell.getBoundingClientRect().width,
            currentWidth: null,
            trackIndex: Array.from(headerRow.children).indexOf(headerCell),
            template,
            tracks: splitTracks(template),
            rows: Array.from(container.querySelectorAll('.cg-row'))
        };

        // Keep receiving this pointer's events even once it leaves the handle.
        handle.setPointerCapture?.(e.pointerId);
        e.preventDefault();
    }

    function onPointerMove(e) {
        if (!active || (active.currentWidth === null && e.clientX === active.startX)) {
            return;
        }

        const width = Math.min(MAX_COLUMN_WIDTH, Math.max(MIN_COLUMN_WIDTH, active.startWidth + e.clientX - active.startX));
        active.currentWidth = width;

        if (active.trackIndex < 0) {
            return;
        }

        const liveTracks = active.tracks.slice();
        liveTracks[active.trackIndex] = `${width}px`;
        setRowTemplates(active.rows, liveTracks.join(' '));
    }

    function onPointerUp() {
        if (!active) {
            return;
        }

        const { columnId, currentWidth } = active;
        active = null;

        // A click without a drag leaves the column alone, rather than pinning a
        // flexible column to the width it happens to have.
        if (currentWidth !== null) {
            dotNetRef.invokeMethodAsync('OnColumnResizedAsync', columnId, currentWidth);
        }
    }

    // The browser took the pointer over (e.g. a touch became a scroll): undo the preview.
    function onPointerCancel() {
        if (!active) {
            return;
        }

        setRowTemplates(active.rows, active.template);
        active = null;
    }

    container.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('pointermove', onPointerMove);
    document.addEventListener('pointerup', onPointerUp);
    document.addEventListener('pointercancel', onPointerCancel);

    return {
        dispose() {
            container.removeEventListener('pointerdown', onPointerDown);
            document.removeEventListener('pointermove', onPointerMove);
            document.removeEventListener('pointerup', onPointerUp);
            document.removeEventListener('pointercancel', onPointerCancel);
        }
    };
}

function setRowTemplates(rows, value) {
    for (const row of rows) {
        row.style.gridTemplateColumns = value;
    }
}

const FLOATING_PANEL_MARGIN = 8;

export function positionFloatingPanel(panelEl) {
    const anchor = panelEl.parentElement;
    if (!anchor) {
        return;
    }

    const anchorRect = anchor.getBoundingClientRect();
    const panelRect = panelEl.getBoundingClientRect();

    // Default: right-aligned to the anchor, opening downward — the same as the
    // CSS fallback (position:absolute; right:0; top:100%), so nothing jumps.
    let left = anchorRect.right - panelRect.width;
    let top = anchorRect.bottom + 4;

    if (left < FLOATING_PANEL_MARGIN) {
        // Flip to left-aligned rather than clamping, which would slide the
        // panel out from under its anchor.
        left = anchorRect.left;
    }

    const maxLeft = window.innerWidth - panelRect.width - FLOATING_PANEL_MARGIN;
    if (left > maxLeft) {
        left = Math.max(FLOATING_PANEL_MARGIN, maxLeft);
    }

    const wouldOverflowBottom = top + panelRect.height > window.innerHeight - FLOATING_PANEL_MARGIN;
    if (wouldOverflowBottom) {
        const openUpTop = anchorRect.top - panelRect.height - 4;
        if (openUpTop >= FLOATING_PANEL_MARGIN) {
            top = openUpTop;
        } else {
            // Neither direction fits: open downward, clamped so the top stays reachable.
            top = Math.max(FLOATING_PANEL_MARGIN, window.innerHeight - panelRect.height - FLOATING_PANEL_MARGIN);
        }
    }

    panelEl.style.position = 'fixed';
    panelEl.style.left = `${left}px`;
    panelEl.style.top = `${top}px`;
    panelEl.style.right = 'auto';
}

export function syncIndeterminate(container) {
    const checkbox = container.querySelector('[data-select-all]');
    if (!checkbox) {
        return;
    }

    checkbox.indeterminate = checkbox.getAttribute('data-indeterminate') === 'true';
}

// The nearest ancestor that actually scrolls vertically: what a sticky header
// sticks within, and so the observer's root. Checks scrollHeight as well as
// overflow-y, because overflow-x:auto alone gives a box a computed overflow-y
// of auto even when it never scrolls (see docs/architecture.md).
function findScrollParent(el) {
    let node = el.parentElement;
    while (node && node !== document.body) {
        const overflowY = getComputedStyle(node).overflowY;
        const canScrollY = (overflowY === 'auto' || overflowY === 'scroll') && node.scrollHeight > node.clientHeight;
        if (canScrollY) {
            return node;
        }
        node = node.parentElement;
    }
    return null;
}

export function initStickyHeaderShadow(sentinelEl, headerEl) {
    const observer = new IntersectionObserver(
        ([entry]) => {
            headerEl.classList.toggle('cg-header-row-stuck', !entry.isIntersecting);
        },
        { root: findScrollParent(sentinelEl), threshold: 0 }
    );
    observer.observe(sentinelEl);

    return {
        dispose() {
            observer.disconnect();
        }
    };
}
