import { initializePwa } from './pwa.js';
import { initializeTheme } from './theme.js';

export function afterWebStarted(blazor) {
    initializePwa(blazor);
    initializeTheme(blazor);
}
