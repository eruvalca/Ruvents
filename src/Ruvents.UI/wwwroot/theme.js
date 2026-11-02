// Colour theme preference: auto (system), light, or dark. This is the only script
// needed for theming; everything visual lives in theme.css.
const storageKey = 'ruvents-theme';
const modes = ['auto', 'light', 'dark'];
const root = document.documentElement;

const readMode = () => {
    try {
        const stored = localStorage.getItem(storageKey);
        return modes.includes(stored) ? stored : 'auto';
    } catch {
        return 'auto';
    }
};

const label = mode => (mode === 'auto' ? 'Auto' : mode === 'light' ? 'Light' : 'Dark');

const apply = mode => {
    if (mode === 'auto') {
        delete root.dataset.theme;
    } else {
        root.dataset.theme = mode;
    }

    try {
        if (mode === 'auto') localStorage.removeItem(storageKey);
        else localStorage.setItem(storageKey, mode);
    } catch {
        // Storage can be unavailable in private browsing; the choice still applies for this page.
    }

    for (const element of document.querySelectorAll('[data-theme-toggle-label]')) {
        element.textContent = label(mode);
    }

    for (const element of document.querySelectorAll('[data-theme-toggle]')) {
        element.setAttribute('aria-label', `Colour theme: ${label(mode)}. Activate to change.`);
    }
};

export function initializeTheme(blazor) {
    apply(readMode());
    document.addEventListener('click', event => {
        const button = event.target instanceof Element ? event.target.closest('[data-theme-toggle]') : null;
        if (!button) return;
        const current = readMode();
        apply(modes[(modes.indexOf(current) + 1) % modes.length]);
    });
    blazor?.addEventListener('enhancedload', () => apply(readMode()));
}
