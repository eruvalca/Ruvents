import { initializePwa } from './pwa.js';

export function afterWebStarted(blazor) {
    initializePwa(blazor);
}
