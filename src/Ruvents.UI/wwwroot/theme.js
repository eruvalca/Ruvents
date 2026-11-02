// Colour theme preference: auto (system), light, or dark. This is the only script
// needed for theming; everything visual lives in theme.css.
const storageKey = 'ruvents-theme';
const modes = ['auto', 'light', 'dark'];
const root = document.documentElement;

// The active mode is kept in memory so the toggle still cycles when storage is
// unavailable; localStorage is only the persistence layer.
let current = 'auto';

const readStoredMode = () => {
    try {
        const stored = localStorage.getItem(storageKey);
        return modes.includes(stored) ? stored : 'auto';
    } catch {
        return current;
    }
};

const label = mode => (mode === 'auto' ? 'Auto' : mode === 'light' ? 'Light' : 'Dark');

const apply = mode => {
    current = modes.includes(mode) ? mode : 'auto';

    if (current === 'auto') {
        delete root.dataset.theme;
    } else {
        root.dataset.theme = current;
    }

    try {
        if (current === 'auto') localStorage.removeItem(storageKey);
        else localStorage.setItem(storageKey, current);
    } catch {
        // Storage can be unavailable in private browsing; the choice still applies for this page.
    }

    for (const element of document.querySelectorAll('[data-theme-toggle-label]')) {
        element.textContent = label(current);
    }

    for (const element of document.querySelectorAll('[data-theme-toggle]')) {
        element.setAttribute('aria-label', `Colour theme: ${label(current)}. Activate to change.`);
    }
};

export function initializeTheme(blazor) {
    apply(readStoredMode());
    document.addEventListener('click', event => {
        const button = event.target instanceof Element ? event.target.closest('[data-theme-toggle]') : null;
        if (!button) return;
        apply(modes[(modes.indexOf(current) + 1) % modes.length]);
    });
    // Enhanced navigation replaces the shell markup, so re-apply the mode that is
    // already active instead of re-reading storage.
    blazor?.addEventListener('enhancedload', () => apply(current));
}
