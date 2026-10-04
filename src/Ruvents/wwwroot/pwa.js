// The server selects the worker; no interactive renderer is needed for installation.
if ('serviceWorker' in navigator) {
    const script = document.getElementById('pwa-registration').dataset.serviceWorker;
    try {
        await navigator.serviceWorker.register(new URL(script, document.baseURI), {
            scope: new URL('.', document.baseURI).pathname,
            updateViaCache: 'none'
        });
    } catch (error) {
        // Registration must not prevent ordinary online use (for example in restricted browsers).
        console.warn('PWA service worker registration failed.', error);
    }
}
