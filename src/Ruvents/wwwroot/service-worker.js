// This starter provides a connection-required page, not an offline application shell.
// Increment the version whenever offline.html changes. Keep the prefix in sync with the development worker.
const cachePrefix = 'ruvents-offline-';
const cacheName = `${cachePrefix}v1`;
const offlineUrl = new URL('offline.html', self.registration.scope).href;

self.addEventListener('install', event => {
    event.waitUntil(caches.open(cacheName).then(cache =>
        cache.add(new Request(offlineUrl, { cache: 'reload' }))));
    // Updates wait for existing windows to close; never reload an in-progress form.
});

self.addEventListener('activate', event => {
    event.waitUntil((async () => {
        const keys = await caches.keys();
        await Promise.all(keys
            .filter(key => key.startsWith(cachePrefix) && key !== cacheName)
            .map(key => caches.delete(key)));
        await self.clients.claim();
    })());
});

self.addEventListener('fetch', event => {
    const request = event.request;
    const url = new URL(request.url);
    const scope = new URL(self.registration.scope);
    const enhancedNavigation = request.headers.get('accept')?.includes('text/html; blazor-enhanced-nav=on');
    if (request.method !== 'GET' || url.origin !== scope.origin || !url.pathname.startsWith(scope.pathname)
        || !(request.mode === 'navigate' || enhancedNavigation)) {
        return;
    }

    const path = url.pathname.slice(scope.pathname.length).toLowerCase();
    // Even direct navigation to APIs/framework resources must retain their real response contracts.
    if (path === 'api' || path.startsWith('api/') || path.startsWith('_')) {
        return;
    }

    event.respondWith((async () => {
        try {
            // Do not cache SSR, Identity, API data, or replace HTTP errors with apparent success.
            return await fetch(request);
        } catch (error) {
            if (request.signal.aborted) {
                throw error;
            }

            const cache = await caches.open(cacheName);
            const fallback = await cache.match(offlineUrl);
            if (!fallback) {
                throw error;
            }

            // Omit blazor-enhanced-nav: allow. Blazor retries an enhanced GET as a full navigation,
            // which loads this standalone page without retaining the previous interactive renderer.
            return new Response(fallback.body, {
                headers: { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' }
            });
        }
    })());
});
