/* Caches the app shell so the capture form still opens with no signal.
   Only static files and the capture page are cached; donor and financial pages are never stored. */
const CACHE = 'pp-shell-v1';
const SHELL = ['/offline.html', '/css/site.css', '/js/site.js', '/icons/icon-192.png'];
const CAPTURE = '/Donation/Create';

self.addEventListener('install', e => {
  e.waitUntil(caches.open(CACHE).then(c => c.addAll(SHELL)).then(() => self.skipWaiting()));
});
self.addEventListener('activate', e => {
  e.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k)))).then(() => self.clients.claim()));
});
self.addEventListener('fetch', e => {
  const req = e.request;
  if (req.method !== 'GET') return;
  const url = new URL(req.url);
  if (url.origin !== location.origin) return;

  if (req.mode === 'navigate') {
    e.respondWith(
      fetch(req).then(res => {
        if (res.ok && url.pathname.toLowerCase() === CAPTURE.toLowerCase()) {
          const copy = res.clone(); caches.open(CACHE).then(c => c.put(CAPTURE, copy));
        }
        return res;
      }).catch(() =>
        url.pathname.toLowerCase() === CAPTURE.toLowerCase()
          ? caches.match(CAPTURE).then(r => r || caches.match('/offline.html'))
          : caches.match('/offline.html'))
    );
    return;
  }
  if (/\.(css|js|png|webmanifest)$/.test(url.pathname)) {
    e.respondWith(caches.match(req).then(hit => hit || fetch(req).then(res => {
      const copy = res.clone(); caches.open(CACHE).then(c => c.put(req, copy)); return res;
    })));
  }
});
