// Replace a previous production worker without caching development responses.
// This prefix must match service-worker.js; do not clear another application's caches.
const cachePrefix = 'ruvents-offline-';

self.addEventListener('install', event => event.waitUntil(self.skipWaiting()));
self.addEventListener('activate', event => {
    event.waitUntil((async () => {
        const keys = await caches.keys();
        await Promise.all(keys.filter(key => key.startsWith(cachePrefix)).map(key => caches.delete(key)));
        await self.clients.claim();
    })());
});
// No fetch handler: all requests use the network.
