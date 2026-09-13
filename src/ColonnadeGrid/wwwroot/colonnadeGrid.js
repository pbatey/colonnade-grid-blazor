// Minimal JS interop for ColonnadeGrid. Deliberately small and narrow, per
// docs/architecture.md: everything else (sorting,
// filtering, grouping, column visibility/order) is pure Blazor/C#.
//
// Column resize: pointer-drag tracking happens entirely here in JS so
// intermediate drag frames never round-trip into Blazor's render loop —
// only the final width, on pointerup, is sent back to .NET (see
// ColonnadeGrid.OnColumnResizedAsync). The live width preview during the
// drag is also done here, by directly rewriting every row's own
// grid-template-columns inline style (header, body, and group-header rows
// all share the identical track list — see ColonnadeGrid.razor.cs's
// BuildGridTemplateColumns comment): the dragged column's track is replaced
// with its live width on every pointermove, while every other track is left
// exactly as authored (so an unset column's own `minmax(140px, 1fr)` token
// keeps reflowing live as the dragged column grows/shrinks, the same as it
// would after a committed resize). This is safe to mutate directly without
// going through Blazor because the eventual OnColumnResizedAsync call
// causes a real re-render with a real new grid-template-columns string,
// which Blazor's diffing will apply over whatever this function last wrote.
//
// Select-all checkbox: the DOM `indeterminate` property has no HTML
// attribute equivalent, so it cannot be set via a plain Blazor bool
// attribute binding — this is the one place a checkbox's tri-state needs a
// JS call.
//
// Dropdown positioning (positionFloatingPanel): the "..."/"+" dropdowns
// (ColumnMenu/ColumnsMenu) are meant to escape whatever hosting container
// the table sits in — including one with overflow-x:auto, per the
// architecture decision that horizontal scrolling is a hosting concern, not
// this component's — so a dropdown near the container's edge doesn't get
// silently clipped by it. That can only be done with real viewport
// measurement: this switches the panel from the CSS fallback
// (position:absolute relative to its anchor) to position:fixed with
// JS-computed viewport coordinates, which — unlike position:absolute —
// isn't clipped by an ancestor's overflow, and can flip/clamp itself to
// stay on-screen.
//
// Sticky-header shadow (initStickyHeaderShadow): the header row's drop
// shadow should only appear once the row is actually in its stuck
// (position:sticky) position, not all the time — but CSS has no selector
// for "is this element currently stuck." The standard, reliable way to
// detect that transition is an IntersectionObserver watching a zero-height
// sentinel placed immediately above the header: the sentinel scrolls out of
// view at exactly the scroll position where the header starts sticking, so
// toggling a class on that (non-)intersection change is what drives the
// CSS transition that fades the shadow in/out (see .cg-header-row-stuck).

// Splits a grid-template-columns value on top-level whitespace only, so a
// function-notation track like "minmax(140px, 1fr)" — which contains its
// own internal space — survives as one token rather than being split apart.
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

        const headerRow = headerCell.parentElement;
        // The dragged column's position among its row's direct children is
        // also its track's index in grid-template-columns — every row uses
        // the identical track order (optional select-cell, then one track
        // per visible column, then the trailing add-column track).
        const trackIndex = Array.from(headerRow.children).indexOf(headerCell);
        const tracks = splitTracks(headerRow.style.gridTemplateColumns);
        const rows = Array.from(container.querySelectorAll('.cg-row'));

        active = {
            columnId: headerCell.getAttribute('data-column-id'),
            startX: e.clientX,
            startWidth: headerCell.getBoundingClientRect().width,
            currentWidth: null,
            trackIndex,
            tracks,
            rows
        };
        e.preventDefault();
    }

    function onPointerMove(e) {
        if (!active) {
            return;
        }

        const delta = e.clientX - active.startX;
        const width = Math.max(20, active.startWidth + delta);
        active.currentWidth = width;

        if (active.trackIndex < 0) {
            return;
        }

        const liveTracks = active.tracks.slice();
        liveTracks[active.trackIndex] = `${width}px`;
        const liveValue = liveTracks.join(' ');
        for (const row of active.rows) {
            row.style.gridTemplateColumns = liveValue;
        }
    }

    function onPointerUp() {
        if (!active) {
            return;
        }

        const { columnId, currentWidth, startWidth } = active;
        active = null;

        const finalWidth = currentWidth ?? startWidth;
        dotNetRef.invokeMethodAsync('OnColumnResizedAsync', columnId, finalWidth);
    }

    container.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('pointermove', onPointerMove);
    document.addEventListener('pointerup', onPointerUp);

    return {
        dispose() {
            container.removeEventListener('pointerdown', onPointerDown);
            document.removeEventListener('pointermove', onPointerMove);
            document.removeEventListener('pointerup', onPointerUp);
        }
    };
}

const FLOATING_PANEL_MARGIN = 8;

export function positionFloatingPanel(panelEl) {
    const anchor = panelEl.parentElement;
    if (!anchor) {
        return;
    }

    const anchorRect = anchor.getBoundingClientRect();
    const panelRect = panelEl.getBoundingClientRect();

    // Default: right-aligned to the anchor's right edge, opening downward —
    // matches the CSS fallback (position:absolute; right:0; top:100%) this
    // replaces, so there's no visible jump when JS interop is unavailable.
    let left = anchorRect.right - panelRect.width;
    let top = anchorRect.bottom + 4;

    if (left < FLOATING_PANEL_MARGIN) {
        // Flip to left-aligned instead of clamping: clamping alone would
        // slide the panel out from under its anchor, which reads as
        // misplaced rather than intentionally repositioned.
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
            // Neither direction fully fits (a very short viewport) — keep
            // opening downward but clamp so at least the top of the panel,
            // with its own scrollbar if needed, stays reachable.
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

// Walks up from an element to find its nearest scrollable ancestor — the
// same element a `position: sticky` descendant actually sticks relative to.
// Mirrors that so the IntersectionObserver below watches the right
// viewport: a host page embedding the table inside its own
// overflow-y:auto/scroll container needs the observer rooted there, not at
// the browser viewport, or "stuck" would never be detected correctly.
function findScrollParent(el) {
    let node = el.parentElement;
    while (node && node !== document.body) {
        const overflowY = getComputedStyle(node).overflowY;
        // A container can compute overflow-y:auto without ever actually
        // scrolling vertically — CSS forces overflow-y's computed value to
        // "auto" whenever overflow-x is set to anything other than
        // "visible" (and vice versa), even if the author only wanted
        // horizontal scrolling and never set overflow-y at all. This
        // component's own demo hits exactly that: .demo-table-container's
        // overflow-x:auto (for wide tables) makes its overflow-y compute as
        // "auto" too, even though it never has vertical overflow of its
        // own. Using that as the IntersectionObserver's root would mean
        // "intersecting" is judged against a box that never scrolls, so the
        // sentinel would never appear to cross it — checking scrollHeight
        // vs. clientHeight confirms the element can *actually* scroll
        // vertically, not just that its computed style permits it.
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
