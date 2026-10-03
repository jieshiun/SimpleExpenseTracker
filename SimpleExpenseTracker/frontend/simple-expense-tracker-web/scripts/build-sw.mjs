import { readdir, readFile, writeFile } from "node:fs/promises";
import { createHash } from "node:crypto";
const dist = new URL("../dist/", import.meta.url);
const files = [];
async function walk(directory, prefix = "") {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    if (entry.isDirectory())
      await walk(
        new URL(entry.name + "/", directory),
        prefix + entry.name + "/",
      );
    else if (entry.name !== "sw.js") files.push(prefix + entry.name);
  }
}
await walk(dist);
const hash = createHash("sha256");
for (const path of files.sort())
  hash.update(await readFile(new URL(path, dist)));
const version = "daily-expense-" + hash.digest("hex").slice(0, 12);
// Cache the static app only. Financial data is always fetched from the backend.
await writeFile(
  new URL("sw.js", dist),
  `
const CACHE = ${JSON.stringify(version)};
const ASSETS = ${JSON.stringify(files.map((p) => "/" + p))};
self.addEventListener('install', event => { event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(ASSETS))); });
self.addEventListener('activate', event => { event.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(key => key.startsWith('daily-expense-') && key !== CACHE).map(key => caches.delete(key)))).then(() => self.clients.claim())); });
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);
  if (event.request.method !== 'GET' || url.origin !== self.location.origin || url.pathname.startsWith('/api/')) return;
  if (event.request.mode === 'navigate') {
    event.respondWith(fetch(event.request).catch(() => caches.match('/index.html')));
  } else if (ASSETS.includes(url.pathname)) {
    event.respondWith(caches.match(event.request).then(cached => cached || fetch(event.request)));
  }
});
`,
);
console.log("Generated service worker:", version);
