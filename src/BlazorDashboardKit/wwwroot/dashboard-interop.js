// BlazorDashboardKit dashboard interop (ES module).
//
// Ported from BTCPay Server's classic `window.DashboardInterop` script. The
// behaviour (Gridstack init/serialize/destroy, Chartist rendering, JSON
// download/upload, clipboard) is preserved 1:1; only the delivery mechanism
// changed: the global object's methods are now named ESM exports and its
// `this._*` instance fields are module-scoped state.
//
// `GridStack` (gridstack-all.js) and `Chartist` are classic, non-module global
// libraries referenced defensively via `globalThis`. This module injects the
// kit's stylesheet, the GridStack stylesheet and the GridStack script itself
// (see `ensureAssets`) the first time a dashboard becomes interactive — i.e.
// only when this module is imported by `DashboardJsInterop.InitGridAsync`,
// never globally — so host pages without a dashboard are untouched. The
// consumer is responsible only for a Chartist build, and only if charts are
// used.

let _grid = null;
let _charts = {};
let _dotNetHelper = null;
let _changeBatch = null;
let _float = true;

// --- Lazy asset injection ---
//
// Replaces the old global `BlazorDashboardKit.lib.module.js` Blazor JS
// initializer, which injected these on *every* Blazor page of the host app
// (auto-discovered RCL initializers run app-wide) and so perturbed unrelated
// pages. Injection now happens here, lazily: this module is only imported when
// a dashboard turns interactive, so a host page with no dashboard never loads
// GridStack. Idempotent and memoized — if the host already references an asset
// (pinned version / custom order) or GridStack is already defined, nothing is
// duplicated. `initGrid` awaits this, so GridStack is guaranteed ready before
// use (the old eager inject only worked by startup-timing luck).
const ASSET_BASE = '_content/BlazorDashboardKit/';
let _assetsPromise = null;

function ensureStylesheet(relativePath) {
    const present = [...document.querySelectorAll('link[rel="stylesheet"]')]
        .some(l => (l.getAttribute('href') || '').indexOf(relativePath) !== -1);
    if (present) return;
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = ASSET_BASE + relativePath;
    document.head.appendChild(link);
}

function ensureGridStackScript() {
    return new Promise((resolve, reject) => {
        if (typeof globalThis.GridStack !== 'undefined') { resolve(); return; }
        const rel = 'gridstack/gridstack-all.js';
        const existing = [...document.querySelectorAll('script')]
            .find(s => (s.getAttribute('src') || '').indexOf(rel) !== -1);
        if (existing) {
            existing.addEventListener('load', () => resolve());
            existing.addEventListener('error', () => reject(new Error('Failed to load GridStack')));
            // It may have finished loading before this listener attached.
            Promise.resolve().then(() => {
                if (typeof globalThis.GridStack !== 'undefined') resolve();
            });
            return;
        }
        const script = document.createElement('script');
        script.src = ASSET_BASE + rel;
        script.async = false; // ordered relative to other dynamically inserted scripts
        script.addEventListener('load', () => resolve());
        script.addEventListener('error', () => reject(new Error('Failed to load GridStack')));
        document.head.appendChild(script);
    });
}

// Idempotent + memoized. Resolves once GridStack is available.
export function ensureAssets() {
    if (_assetsPromise) return _assetsPromise;
    ensureStylesheet('gridstack/gridstack.min.css');
    ensureStylesheet('dashboard.css');
    _assetsPromise = ensureGridStackScript();
    return _assetsPromise;
}

// --- Gridstack integration ---

// BTCPay passed a DOM ElementReference; the host-agnostic kit passes the
// container's element id (DashboardHost._gridContainerId), so the element is
// resolved here. Everything else mirrors the original initGrid.
export async function initGrid(containerId, dotNetHelper, editMode, options) {
    // Best-effort: a failed asset load must NOT throw into the host (a kit
    // must never destabilize its consumer). The GridStack guard below then
    // degrades gracefully — exactly the original contract.
    try { await ensureAssets(); } catch { /* handled by the guard below */ }
    var containerElement = typeof containerId === 'string'
        ? document.getElementById(containerId)
        : containerId;
    if (!containerElement || typeof globalThis.GridStack === 'undefined') return;
    destroyGrid();
    _dotNetHelper = dotNetHelper;

    // options come from DashboardHost.GridOptions (camelCase via Blazor JSON).
    // Fall back to the kit defaults so older callers / null still work.
    var o = options || {};
    var columns = o.columns || 12;
    _float = (o.float == null ? true : o.float);
    var grid = globalThis.GridStack.init({
        column: columns,
        cellHeight: o.cellHeight || 146,
        margin: (o.margin == null ? 8 : o.margin),
        float: _float,
        // Off for the first layout; switched on below once it has been painted.
        animate: false,
        draggable: { handle: '.widget-drag-handle' },
        resizable: { handles: 'e,se,s,sw,w' },
        staticGrid: !editMode,
        columnOpts: {
            breakpoints: [{ w: (o.mobileBreakpointWidth || 992), c: (o.mobileColumns || 1) }],
            breakpointForWindow: true,
            columnMax: columns
        }
    }, containerElement);

    // The container's two changing classes live here, not in Blazor's markup: a Blazor
    // re-render that changed the class attribute would wipe the classes GridStack just added
    // (grid-stack-animate among them, which is what animates moves).
    containerElement.classList.remove('bdk-grid-static');
    containerElement.classList.toggle('dashboard-edit', !!editMode);

    // Animation (moves, compaction) goes on only after GridStack's first layout has been
    // painted. On from the start, every widget visibly shrank from the full-width static
    // fallback to its grid size on each page load and rebuild.
    void containerElement.offsetWidth;
    requestAnimationFrame(function () {
        requestAnimationFrame(function () {
            if (_grid === grid) grid.setAnimation(true);
        });
    });

    // Batch change events with a short debounce to avoid multiple rapid calls
    grid.on('change', function (event, items) {
        if (!items || !_dotNetHelper) return;
        // Debounce: collect changes, send once after 100ms of no activity
        if (_changeBatch) clearTimeout(_changeBatch);
        _changeBatch = setTimeout(function () {
            _changeBatch = null;
            _dotNetHelper.invokeMethodAsync('OnGridChanged', JSON.stringify(collectLayout(grid)));
        }, 100);
    });

    // Destroy charts on resize start to prevent SVG artifacts
    grid.on('resizestart', function (event, el) {
        var chartElements = el.querySelectorAll('.ct-chart');
        chartElements.forEach(function (chartEl) {
            if (chartEl.id && _charts[chartEl.id]) {
                _charts[chartEl.id].detach();
                delete _charts[chartEl.id];
                chartEl.innerHTML = '';
            }
        });
    });

    // Compact once a drag or resize has landed (see compactSoon).
    grid.on('dragstop resizestop', function () {
        if (_grid === grid) compactSoon();
    });

    _grid = grid;
    if (editMode) compactSoon();
}

// Edit mode compacts the grid after every drag, resize, add and remove: turning float off
// makes GridStack slide each widget straight up into any free space above it (same column,
// no overlaps; locked widgets stay put), animated by its own transition, and float goes back
// on so the next move places freely. Float stays on during the move itself: with gravity on
// live, GridStack parks the placeholder at the compacted spot and the dropped widget snaps
// there with no animation. Two frames let the drop paint where it landed before the slide.
// The change event the compaction raises saves the new layout.
function compactSoon() {
    var grid = _grid;
    requestAnimationFrame(function () {
        requestAnimationFrame(function () {
            if (_grid === grid && !grid.opts.staticGrid) compact(grid);
        });
    });
}

function compact(grid) {
    if (!_float) return; // gravity already always on
    grid.float(false);
    grid.float(true);
}

// Every widget's position, in the shape OnGridChanged reads.
function collectLayout(grid) {
    var changes = [];
    grid.getGridItems().forEach(function (el) {
        var node = el.gridstackNode;
        if (!node) return;
        changes.push({
            id: node.id || '',
            x: node.x || 0,
            y: node.y || 0,
            w: node.w || 1,
            h: node.h || 1
        });
    });
    return changes;
}

// Entering edit mode compacts the grid (see compactSoon). Leaving it compacts once more, in
// case a last move's compaction hasn't run yet, and returns the layout as JSON so the kit
// saves it in one go; a change event still pending is dropped, since it would only save the
// same layout again.
export function setEditMode(editMode) {
    if (!_grid) return null;
    var layout = null;
    if (editMode) {
        compactSoon();
    } else {
        compact(_grid);
        if (_changeBatch) {
            clearTimeout(_changeBatch);
            _changeBatch = null;
        }
        layout = JSON.stringify(collectLayout(_grid));
    }
    _grid.setStatic(!editMode);
    _grid.el.classList.toggle('dashboard-edit', !!editMode);
    return layout;
}

export function destroyGrid() {
    if (_changeBatch) {
        clearTimeout(_changeBatch);
        _changeBatch = null;
    }
    if (_grid) {
        // Back to the static fallback layout until the grid is initialized again, without
        // animating into it.
        _grid.el.classList.remove('grid-stack-animate');
        _grid.el.classList.add('bdk-grid-static');
        _grid.destroy(false); // false = don't remove DOM elements (Blazor owns them)
        _grid = null;
    }
    _dotNetHelper = null;
}

// Locks or unlocks one widget in place: no move, no resize, and other widgets can't push it.
export function setWidgetLocked(id, locked) {
    if (!_grid) return;
    var el = _grid.getGridItems().find(function (e) {
        return e.gridstackNode && e.gridstackNode.id === id;
    });
    if (el) _grid.update(el, { locked: locked, noMove: locked, noResize: locked });
}

export function addGridWidget(element) {
    if (!_grid || !element) return;
    _grid.makeWidget(element);
    compactSoon();
}

export function removeGridWidget(element) {
    if (!_grid || !element) return;
    _grid.removeWidget(element, false); // false = don't remove DOM
    compactSoon();
}

// --- Charts ---

export function renderChart(elementId, type, labels, series, options) {
    var element = document.getElementById(elementId);
    if (!element || typeof globalThis.Chartist === 'undefined') return;
    var Chartist = globalThis.Chartist;

    if (_charts[elementId]) {
        _charts[elementId].detach();
    }

    var chartData = { labels: labels, series: series };

    var low = 0;
    if (type === 'line' && series.length > 0) {
        var flatValues = series[0];
        if (flatValues && flatValues.length > 0) {
            var min = Math.min.apply(null, flatValues);
            var max = Math.max.apply(null, flatValues);
            low = Math.max(min - ((max - min) / 5), 0);
        }
    }

    var labelCount = 6;
    var pointCount = labels.length;
    var labelEvery = pointCount / labelCount;
    var dateFormatter = new Intl.DateTimeFormat('default', { month: 'short', day: 'numeric' });

    var tooltip = (typeof Chartist.plugins !== 'undefined' && Chartist.plugins.tooltip2)
        ? Chartist.plugins.tooltip2({
            template: '<div class="chartist-tooltip-value">{{value}}</div><div class="chartist-tooltip-line"></div>',
            offset: { x: 0, y: -16 }
        })
        : null;

    var defaultOptions = {
        low: low,
        showArea: type === 'line',
        fullWidth: true,
        axisX: {
            labelInterpolationFnc: function (date, i) {
                return i % Math.ceil(labelEvery) === 0 ? dateFormatter.format(new Date(date)) : null;
            }
        },
        axisY: {
            showLabel: false,
            offset: 0
        },
        plugins: tooltip ? [tooltip] : []
    };

    var mergedOptions = Object.assign({}, defaultOptions, options || {});

    if (type === 'line') {
        _charts[elementId] = new Chartist.Line('#' + elementId, chartData, mergedOptions);
    } else if (type === 'bar') {
        _charts[elementId] = new Chartist.Bar('#' + elementId, chartData, mergedOptions);
    }
}

export function destroyChart(elementId) {
    if (_charts[elementId]) {
        _charts[elementId].detach();
        delete _charts[elementId];
    }
}

// --- Export / Import ---

export function downloadJson(filename, json) {
    var blob = new Blob([json], { type: 'application/json' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

export function readFileAsText(inputElement) {
    return new Promise(function (resolve) {
        if (!inputElement || !inputElement.files || inputElement.files.length === 0) {
            resolve(null);
            return;
        }
        var reader = new FileReader();
        reader.onload = function () { resolve(reader.result); };
        reader.onerror = function () { resolve(null); };
        reader.readAsText(inputElement.files[0]);
    });
}

// --- Clipboard ---
//
// BTCPay invoked `navigator.clipboard.writeText` directly from .NET rather than
// through a DashboardInterop method. The kit's DashboardJsInterop routes every
// JS call through this module, so the same behaviour is exposed as an export.
export function copyToClipboard(text) {
    return navigator.clipboard.writeText(text);
}
