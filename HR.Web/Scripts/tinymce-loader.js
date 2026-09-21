(function (window) {
    'use strict';

    var TINYMCE_VERSION = '6.8.4';
    var TINYMCE_CDN_BASE = 'https://cdn.jsdelivr.net/npm/tinymce@' + TINYMCE_VERSION;
    var TINYMCE_SCRIPT_URL = TINYMCE_CDN_BASE + '/tinymce.min.js';
    var scriptPromise = null;

    function loadScript() {
        if (typeof window.tinymce !== 'undefined') {
            return Promise.resolve(window.tinymce);
        }
        if (scriptPromise) {
            return scriptPromise;
        }

        scriptPromise = new Promise(function (resolve, reject) {
            var script = document.createElement('script');
            script.src = TINYMCE_SCRIPT_URL;
            script.referrerPolicy = 'origin';
            script.async = true;
            script.onload = function () {
                if (typeof window.tinymce !== 'undefined') {
                    resolve(window.tinymce);
                } else {
                    reject(new Error('TinyMCE unavailable after load'));
                }
            };
            script.onerror = function () {
                reject(new Error('TinyMCE script failed to load'));
            };
            document.head.appendChild(script);
        });

        return scriptPromise;
    }

    function whenReady(callback) {
        if (!callback) {
            return;
        }

        if (typeof window.tinymce !== 'undefined') {
            callback(window.tinymce, null);
            return;
        }

        loadScript()
            .then(function (tinymce) {
                callback(tinymce, null);
            })
            .catch(function (error) {
                callback(null, error);
            });
    }

    function mergeInitOptions(options) {
        var merged = {
            menubar: false,
            branding: false,
            license_key: 'gpl',
            base_url: TINYMCE_CDN_BASE,
            suffix: '.min',
            convert_urls: false,
            entity_encoding: 'raw'
        };

        if (!options) {
            return merged;
        }

        for (var key in options) {
            if (Object.prototype.hasOwnProperty.call(options, key)) {
                merged[key] = options[key];
            }
        }

        return merged;
    }

    function init(options) {
        return loadScript().then(function (tinymce) {
            return tinymce.init(mergeInitOptions(options));
        });
    }

    function getEditor(textareaId) {
        if (typeof window.tinymce === 'undefined' || !textareaId) {
            return null;
        }

        var ed = window.tinymce.get(textareaId);
        if (ed) {
            return ed;
        }

        var list = window.tinymce.editors;
        if (!list || !list.length) {
            return null;
        }

        for (var i = 0; i < list.length; i++) {
            var candidate = list[i];
            if (candidate && candidate.id === textareaId) {
                return candidate;
            }
        }

        for (var j = 0; j < list.length; j++) {
            var editor = list[j];
            try {
                var el = editor.getElement && editor.getElement();
                if (el && el.id === textareaId) {
                    return editor;
                }
            } catch (ex) { /* ignore */ }
        }

        return null;
    }

    function removeEditor(textareaId) {
        var editor = getEditor(textareaId);
        if (editor) {
            window.tinymce.remove(editor);
        }
    }

    function removeExcept(keepTextareaId) {
        if (typeof window.tinymce === 'undefined') {
            return;
        }

        var list = window.tinymce.editors ? window.tinymce.editors.slice() : [];
        list.forEach(function (editor) {
            if (!editor) {
                return;
            }
            if (keepTextareaId && editor.id === keepTextareaId) {
                return;
            }
            try {
                var el = editor.getElement && editor.getElement();
                if (keepTextareaId && el && el.id === keepTextareaId) {
                    return;
                }
            } catch (ex) { /* ignore */ }
            window.tinymce.remove(editor);
        });
    }

    window.HrTinyMceLoader = {
        version: TINYMCE_VERSION,
        baseUrl: TINYMCE_CDN_BASE,
        scriptUrl: TINYMCE_SCRIPT_URL,
        load: loadScript,
        whenReady: whenReady,
        init: init,
        get: getEditor,
        remove: removeEditor,
        removeExcept: removeExcept,
        mergeInitOptions: mergeInitOptions
    };
})(window);
