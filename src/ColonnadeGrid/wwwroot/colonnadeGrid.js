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

// Custom properties the floating menus read for their chrome. They are defined
// on .cg-root and normally inherit down; when a panel is portaled to <body>
// (see positionFloatingPanel) that inheritance is lost, so their resolved
// values are copied onto the panel. Keep in sync with .cg-root in
// ColonnadeGrid.razor.css and the menus' .razor.css files.
const CG_PORTAL_CUSTOM_PROPS = [
    '--cg-popover-bg',
    '--cg-popover-text-color',
    '--cg-popover-muted-text-color',
    '--cg-popover-hover-bg',
    '--cg-popover-border-color',
    '--cg-popover-line-height',
    '--cg-font-family',
    '--cg-font-size',
    '--cg-radius'
];

export function positionFloatingPanel(panelEl) {
    // The panel is rendered as a DOM descendant of its trigger's header cell,
    // which sits inside host containers that may clip (overflow: hidden/auto) or
    // establish a containing block for fixed descendants (a transform/filter/
    // etc. on an ancestor). Neither position:absolute nor a coordinate-adjusted
    // position:fixed can reliably escape *clipping* — an overflow ancestor clips
    // a fixed child whenever that ancestor is also its containing block. The one
    // approach immune to every host layout is to portal the panel to <body>, so
    // it has no clipping or containing-block ancestor at all, then position it
    // with viewport (fixed) coordinates measured from the trigger. The element
    // stays a Blazor-owned node; restoreFloatingPanel puts it back before Blazor
    // disposes it (see the components' DisposeAsync).
    // Resolve the trigger. On the first call the panel is still a DOM descendant
    // of its trigger's anchor span, so read it from there and remember it — after
    // portaling to <body> the panel's parent/siblings no longer point at the
    // button, and this function runs again whenever the menu changes size (e.g.
    // switching to the taller "Filter by values…" view) to reposition it.
    let trigger;
    if (panelEl._cgPortal && panelEl._cgPortal.trigger && panelEl._cgPortal.trigger.isConnected) {
        trigger = panelEl._cgPortal.trigger;
    } else {
        const anchor = panelEl.parentElement;
        if (!anchor) {
            return;
        }
        // The button is rendered immediately before the panel inside the anchor
        // span; fall back to a class query, then to the span itself.
        trigger = panelEl.previousElementSibling
            || anchor.querySelector('.cg-column-menu-button, .cg-columns-button')
            || anchor;
    }
    const anchorRect = trigger.getBoundingClientRect();

    // Portal to <body> (once). Remember where it came from so it can be restored
    // to exactly its original slot — Blazor removes it from that parent on close,
    // and removing a node from a parent it no longer lives under throws.
    if (!panelEl._cgPortal) {
        // The panel's chrome (background, text/border colors, popover
        // line-height) comes from --cg-popover-*/--cg-font-* custom properties
        // defined on .cg-root and inherited down the tree. Once portaled to
        // <body> that inheritance chain is severed, so copy the resolved values
        // onto the panel itself first. Also lift it above page content: its
        // scoped z-index only meant something inside .cg-root's stacking context.
        const cs = getComputedStyle(panelEl);
        for (const prop of CG_PORTAL_CUSTOM_PROPS) {
            const value = cs.getPropertyValue(prop);
            if (value) {
                panelEl.style.setProperty(prop, value.trim());
            }
        }
        panelEl.style.zIndex = '2147483000';

        const placeholder = document.createComment('cg-floating-panel');
        panelEl.before(placeholder);
        panelEl._cgPortal = { placeholder, trigger };

        // The click-away backdrop (.cg-dropdown-backdrop) closes the menu on an
        // outside click. It lives inside .cg-root, whose `isolation: isolate`
        // caps its stacking within the grid's own context — but the menu now
        // sits on <body> above that context, so clicks outside the grid never
        // reach the backdrop. Lift the same backdrop element onto <body> too,
        // just below the menu, so it covers the whole viewport and catches every
        // outside click. Its Blazor @onclick keeps working across the move.
        const root = trigger.closest('.cg-root');
        const backdrop = root && root.querySelector(':scope > .cg-dropdown-backdrop');
        if (backdrop && !backdrop._cgPortal) {
            const backdropPlaceholder = document.createComment('cg-dropdown-backdrop');
            backdrop.before(backdropPlaceholder);
            backdrop._cgPortal = { placeholder: backdropPlaceholder };
            backdrop.style.zIndex = '2147482999';
            document.body.appendChild(backdrop);
        }
        panelEl._cgPortal.backdrop = backdrop || null;

        document.body.appendChild(panelEl);
    }

    const panelRect = panelEl.getBoundingClientRect();

    // Default: right-aligned to the trigger, opening downward — matches the CSS
    // fallback (position:absolute; right:0; top:100%) so there is no visual jump.
    let left = anchorRect.right - panelRect.width;
    let top = anchorRect.bottom + 4;

    if (left < FLOATING_PANEL_MARGIN) {
        // Flip to left-aligned rather than clamping, which would slide the
        // panel out from under its trigger.
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

    // Now on <body>, so viewport (fixed) coordinates apply directly with no
    // clipping or containing-block ancestor to fight.
    panelEl.style.position = 'fixed';
    panelEl.style.left = `${left}px`;
    panelEl.style.top = `${top}px`;
    panelEl.style.right = 'auto';
}

// Moves a portaled panel back to its original DOM slot so Blazor can remove it
// cleanly on close. Safe to call if the panel was never portaled (no-op).
export function restoreFloatingPanel(panelEl) {
    const portal = panelEl && panelEl._cgPortal;
    if (!portal) {
        return;
    }

    // Restore the lifted backdrop first (see positionFloatingPanel). Both menus
    // share one backdrop, so guard against a double-restore via its own _cgPortal.
    const backdrop = portal.backdrop;
    if (backdrop && backdrop._cgPortal) {
        const bp = backdrop._cgPortal.placeholder;
        if (bp && bp.parentNode) {
            bp.replaceWith(backdrop);
        } else if (backdrop.parentNode === document.body) {
            document.body.removeChild(backdrop);
        }
        backdrop.style.zIndex = '';
        delete backdrop._cgPortal;
    }

    const { placeholder } = portal;
    if (placeholder && placeholder.parentNode) {
        placeholder.replaceWith(panelEl);
    } else if (panelEl.parentNode === document.body) {
        document.body.removeChild(panelEl);
    }
    delete panelEl._cgPortal;
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


