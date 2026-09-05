self.importScripts('./service-worker-assets.js');

self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'mln-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const dataCacheName = `${cacheNamePrefix}data`;
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/];
const offlineAssetsExclude = [/^service-worker\.js$/, /^data\//];

const base = new URL(self.registration.scope).pathname;

async function onInstall() {
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
    self.skipWaiting();
}

async function onActivate() {
    const keys = await caches.keys();
    await Promise.all(keys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName && key !== dataCacheName)
        .map(key => caches.delete(key)));

    await self.clients.claim();
}

async function onFetch(event) {
    if (event.request.method !== 'GET') {
        return fetch(event.request);
    }

    const url = new URL(event.request.url);
    const sameOrigin = url.origin === self.location.origin;

    if (sameOrigin && url.pathname.includes('/data/') && url.pathname.endsWith('.json')) {
        return staleWhileRevalidate(event.request);
    }

    if (!sameOrigin) {
        return fetch(event.request);
    }

    const shouldServeIndexHtml = event.request.mode === 'navigate';
    const request = shouldServeIndexHtml ? new Request(`${base}index.html`) : event.request;

    const cached = await caches.match(request, { cacheName });
    if (cached) {
        return cached;
    }

    try {
        return await fetch(event.request);
    } catch (error) {
        const fallback = await caches.match(`${base}index.html`, { cacheName });
        if (fallback) {
            return fallback;
        }
        throw error;
    }
}

async function staleWhileRevalidate(request) {
    const cache = await caches.open(dataCacheName);
    const cached = await cache.match(request);

    const network = fetch(request)
        .then(response => {
            if (response.ok) {
                cache.put(request, response.clone());
            }
            return response;
        })
        .catch(() => cached);

    return cached || network;
}
