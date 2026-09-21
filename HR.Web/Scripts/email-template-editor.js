(function () {
    'use strict';

    var selectionRanges = {};

    function closest(el, selector) {
        if (!el) {
            return null;
        }
        if (el.closest) {
            return el.closest(selector);
        }
        return null;
    }

    function markEmailComposeEditing(compose) {
        if (!compose) {
            return;
        }
        document.querySelectorAll('.email-template-compose').forEach(function (card) {
            card.classList.toggle('email-template-editing', card === compose);
        });
    }

    function clearEmailComposeEditing() {
        document.querySelectorAll('.email-template-compose.email-template-editing').forEach(function (card) {
            card.classList.remove('email-template-editing');
        });
    }

    function enableTemplateStorages() {
        document.querySelectorAll('#emailTemplatesForm textarea.email-template-storage').forEach(function (ta) {
            ta.removeAttribute('disabled');
            ta.readOnly = false;
            ta.classList.remove('readonly-module-disabled');
            ta.style.pointerEvents = '';
            ta.style.opacity = '';
        });
    }

    function saveSelection(el) {
        if (!el || !el.id) {
            return;
        }
        var sel = window.getSelection ? window.getSelection() : null;
        if (!sel || sel.rangeCount === 0) {
            return;
        }
        var range = sel.getRangeAt(0);
        if (!range) {
            return;
        }
        var container = range.commonAncestorContainer;
        if (container && container.nodeType === 3) {
            container = container.parentNode;
        }
        if (!container || !el.contains(container)) {
            return;
        }
        selectionRanges[el.id] = range.cloneRange();
    }

    function restoreSelection(el) {
        if (!el || !el.id) {
            return false;
        }
        var range = selectionRanges[el.id];
        var sel = window.getSelection ? window.getSelection() : null;
        if (!range || !sel) {
            return false;
        }
        try {
            sel.removeAllRanges();
            sel.addRange(range);
            return true;
        } catch (err) {
            return false;
        }
    }

    function clearElement(el) {
        while (el && el.firstChild) {
            el.removeChild(el.firstChild);
        }
    }

    function writeComposeHtml(div, html) {
        clearElement(div);
        if (!html) {
            return;
        }
        var parser = new DOMParser();
        var doc = parser.parseFromString('<div>' + html + '</div>', 'text/html');
        var wrapper = doc.body ? doc.body.firstChild : null;
        if (!wrapper) {
            return;
        }
        var imported = div.ownerDocument.importNode(wrapper, true);
        while (imported.firstChild) {
            div.appendChild(imported.firstChild);
        }
    }

    function readComposeHtml(div) {
        return div ? (div.innerHTML || '') : '';
    }

    function syncComposeToStorage(composeEl) {
        if (!composeEl) {
            return;
        }
        var subjectDiv = document.getElementById(composeEl.getAttribute('data-subject-compose-id'));
        var subjectTa = document.getElementById(composeEl.getAttribute('data-subject-storage-id'));
        if (subjectDiv && subjectTa) {
            subjectTa.value = readComposeHtml(subjectDiv);
        }
        var bodyDiv = document.getElementById(composeEl.getAttribute('data-body-compose-id'));
        var bodyTa = document.getElementById(composeEl.getAttribute('data-body-storage-id'));
        if (bodyDiv && bodyTa) {
            bodyTa.value = readComposeHtml(bodyDiv);
        }
    }

    function syncAllStorages() {
        enableTemplateStorages();
        document.querySelectorAll('.email-template-compose').forEach(syncComposeToStorage);
    }

    function cloneChipNodeFromButton(btn) {
        if (!btn) {
            return null;
        }
        var tpl = btn.nextElementSibling;
        if (tpl && tpl.tagName && tpl.tagName.toUpperCase() === 'TEMPLATE' && tpl.content && tpl.content.firstElementChild) {
            return tpl.content.firstElementChild.cloneNode(true);
        }
        var tokenName = btn.getAttribute('data-token-name');
        var compose = closest(btn, '.email-template-compose');
        if (!tokenName || !compose) {
            return null;
        }
        var templates = compose.querySelectorAll('template.hr-email-chip-source');
        for (var i = 0; i < templates.length; i++) {
            if (templates[i].getAttribute('data-token-name') === tokenName &&
                templates[i].content &&
                templates[i].content.firstElementChild) {
                return templates[i].content.firstElementChild.cloneNode(true);
            }
        }
        return null;
    }

    function insertNodeAtSelection(container, node) {
        container.focus();
        restoreSelection(container);
        var sel = window.getSelection ? window.getSelection() : null;
        if (sel && sel.rangeCount > 0) {
            var range = sel.getRangeAt(0);
            if (container.contains(range.commonAncestorContainer) || container === range.commonAncestorContainer) {
                range.deleteContents();
                range.insertNode(node);
                range.setStartAfter(node);
                range.collapse(true);
                sel.removeAllRanges();
                sel.addRange(range);
                return;
            }
        }
        container.appendChild(node);
    }

    function insertChipIntoEditor(compose, btn, targetAttr) {
        var chipNode = cloneChipNodeFromButton(btn);
        if (!chipNode || !compose) {
            return;
        }
        var editor = document.getElementById(compose.getAttribute(targetAttr));
        if (!editor) {
            return;
        }
        markEmailComposeEditing(compose);
        editor.focus();
        restoreSelection(editor);
        var inserted = false;
        try {
            inserted = document.execCommand('insertHTML', false, chipNode.outerHTML);
        } catch (err) {
            inserted = false;
        }
        if (!inserted) {
            insertNodeAtSelection(editor, chipNode.cloneNode(true));
        }
        saveSelection(editor);
        syncComposeToStorage(compose);
    }

    function applyBodyCommand(compose, command, value) {
        var body = document.getElementById(compose.getAttribute('data-body-compose-id'));
        if (!body) {
            return;
        }
        markEmailComposeEditing(compose);
        body.focus();
        restoreSelection(body);
        try {
            document.execCommand(command, false, value || null);
        } catch (err) { /* ignore unsupported command */ }
        saveSelection(body);
        syncComposeToStorage(compose);
    }

    function bindEditorEvents(div, compose) {
        if (!div) {
            return;
        }
        ['input', 'keyup', 'mouseup', 'focus', 'blur'].forEach(function (evt) {
            div.addEventListener(evt, function () {
                saveSelection(div);
                if (evt === 'input' || evt === 'blur' || evt === 'keyup') {
                    syncComposeToStorage(compose);
                }
            });
        });
        div.addEventListener('focus', function () {
            markEmailComposeEditing(compose);
        });
    }

    function hydrateEditors() {
        document.querySelectorAll('.email-template-compose').forEach(function (compose) {
            var subjectDiv = document.getElementById(compose.getAttribute('data-subject-compose-id'));
            var subjectTa = document.getElementById(compose.getAttribute('data-subject-storage-id'));
            if (subjectDiv && subjectTa) {
                writeComposeHtml(subjectDiv, subjectTa.value);
                bindEditorEvents(subjectDiv, compose);
            }
            var bodyDiv = document.getElementById(compose.getAttribute('data-body-compose-id'));
            var bodyTa = document.getElementById(compose.getAttribute('data-body-storage-id'));
            if (bodyDiv && bodyTa) {
                writeComposeHtml(bodyDiv, bodyTa.value);
                if (!bodyDiv.childNodes.length) {
                    bodyDiv.innerHTML = '<p><br></p>';
                }
                bindEditorEvents(bodyDiv, compose);
            }
        });
    }

    document.addEventListener('focusin', function (ev) {
        var compose = closest(ev.target, '.email-template-compose');
        if (compose) {
            markEmailComposeEditing(compose);
        }
    }, true);

    document.addEventListener('mousedown', function (ev) {
        var t = ev.target;
        if (closest(t, '.email-template-compose')) {
            return;
        }
        clearEmailComposeEditing();
    }, true);

    document.addEventListener('mousedown', function (ev) {
        if (closest(ev.target, '.email-template-token-subj, .email-template-token-body, .email-template-format-btn')) {
            ev.preventDefault();
        }
    });

    document.addEventListener('click', function (ev) {
        var subBtn = closest(ev.target, '.email-template-token-subj');
        if (subBtn) {
            ev.preventDefault();
            var composeSub = closest(subBtn, '.email-template-compose');
            insertChipIntoEditor(composeSub, subBtn, 'data-subject-compose-id');
            return;
        }

        var bodyBtn = closest(ev.target, '.email-template-token-body');
        if (bodyBtn) {
            ev.preventDefault();
            var composeBody = closest(bodyBtn, '.email-template-compose');
            insertChipIntoEditor(composeBody, bodyBtn, 'data-body-compose-id');
            return;
        }

        var formatBtn = closest(ev.target, '.email-template-format-btn');
        if (formatBtn) {
            ev.preventDefault();
            var composeFmt = closest(formatBtn, '.email-template-compose');
            if (!composeFmt) {
                return;
            }
            var command = formatBtn.getAttribute('data-command');
            if (command === 'createLink') {
                var url = window.prompt('Link URL', 'https://');
                if (url === null) {
                    return;
                }
                if (!String(url).trim()) {
                    applyBodyCommand(composeFmt, 'unlink');
                    return;
                }
                applyBodyCommand(composeFmt, 'createLink', String(url).trim());
                return;
            }
            applyBodyCommand(composeFmt, command, formatBtn.getAttribute('data-command-value'));
            return;
        }

        var editBtn = closest(ev.target, '.email-template-edit-btn');
        if (editBtn) {
            ev.preventDefault();
            var composeEdit = closest(editBtn, '.email-template-compose');
            if (!composeEdit) {
                return;
            }
            markEmailComposeEditing(composeEdit);
            var body = document.getElementById(composeEdit.getAttribute('data-body-compose-id'));
            if (body) {
                body.focus();
                if (body.scrollIntoView) {
                    body.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                }
            }
        }
    });

    function init() {
        enableTemplateStorages();
        hydrateEditors();

        var form = document.getElementById('emailTemplatesForm');
        if (form) {
            form.addEventListener('submit', function () {
                syncAllStorages();
            }, true);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
