/* eslint-env serviceworker */
/* global self */
/* @noflow */
var swGlobal = self;
var LEADING_SLASHES = /^\/+/;
var STATIC_PATH_PATTERN = /\/(?:Content|Scripts)\//i;
var STATIC_EXT_PATTERN = /\.(?:css|js|png|jpg|jpeg|gif|svg|ico|woff|woff2|ttf|eot)$/i;

var CACHE_NAME = "recruitment-shell-v1";
var scopeUrl = new swGlobal.URL(swGlobal.registration.scope);

function appUrl(relativePath) {
    return new swGlobal.URL(relativePath.replace(LEADING_SLASHES, ""), scopeUrl).toString();
}

var OFFLINE_URL = appUrl("offline.html");
var STATIC_ASSETS = [
    OFFLINE_URL,
    appUrl("Content/css/bootstrap.min.css"),
    appUrl("Content/css/font-awesome.min.css"),
    appUrl("Scripts/jquery-3.6.0.min.js"),
    appUrl("Scripts/bootstrap.bundle.min.js"),
    appUrl("Content/images/nanosoft-logo-transparent.png"),
    appUrl("Content/images/nanosoft-logo.jpg")
];

swGlobal.addEventListener("install", (event) => {
    event.waitUntil(
        swGlobal.caches.open(CACHE_NAME).then((cache) => {
            return cache.addAll(STATIC_ASSETS);
        }).then(() => {
            return swGlobal.skipWaiting();
        })
    );
});

swGlobal.addEventListener("activate", (event) => {
    event.waitUntil(
        swGlobal.caches.keys().then((cacheNames) => {
            return Promise.all(
                cacheNames
                    .filter((cacheName) => { return cacheName !== CACHE_NAME; })
                    .map((cacheName) => { return swGlobal.caches.delete(cacheName); })
            );
        }).then(() => {
            return swGlobal.clients.claim();
        })
    );
});

swGlobal.addEventListener("fetch", (event) => {
    if (event.request.method !== "GET") {
        return;
    }

    var requestUrl = new swGlobal.URL(event.request.url);
    if (requestUrl.origin !== swGlobal.location.origin) {
        return;
    }

    if (event.request.mode === "navigate") {
        event.respondWith(
            swGlobal.fetch(event.request)
                .then((response) => {
                    var responseCopy = response.clone();
                    swGlobal.caches.open(CACHE_NAME).then((cache) => {
                        cache.put(event.request, responseCopy);
                    });
                    return response;
                })
                .catch(() => {
                    return swGlobal.caches.match(event.request).then((cachedPage) => {
                        return cachedPage || swGlobal.caches.match(OFFLINE_URL);
                    });
                })
        );
        return;
    }

    var isStaticAsset = STATIC_PATH_PATTERN.test(requestUrl.pathname) ||
        STATIC_EXT_PATTERN.test(requestUrl.pathname);

    if (isStaticAsset) {
        event.respondWith(
            swGlobal.caches.match(event.request).then((cached) => {
                if (cached) {
                    return cached;
                }

                return swGlobal.fetch(event.request).then((response) => {
                    var responseCopy = response.clone();
                    swGlobal.caches.open(CACHE_NAME).then((cache) => {
                        cache.put(event.request, responseCopy);
                    });
                    return response;
                });
            })
        );
    }
});
