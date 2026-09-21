'use strict';

var fs = require('fs');
var path = require('path');
var JSDOM;
try {
    JSDOM = require('jsdom').JSDOM;
} catch (err) {
    console.log('SKIP email template editor jsdom test (jsdom not installed).');
    process.exit(0);
}

var repoRoot = path.resolve(__dirname, '../..');
var scriptPath = path.join(repoRoot, 'HR.Web/Scripts/email-template-editor.js');
var editorJs = fs.readFileSync(scriptPath, 'utf8');

var html = '<!DOCTYPE html><html><body>' +
    '<form id="emailTemplatesForm">' +
    '<div class="email-template-compose"' +
    ' data-subject-compose-id="subjectCompose_0"' +
    ' data-subject-storage-id="defaultSubjectTemplate_0"' +
    ' data-body-compose-id="bodyCompose_0"' +
    ' data-body-storage-id="defaultBodyTemplate_0"' +
    ' data-body-id="defaultBodyTemplate_0">' +
    '<textarea id="defaultSubjectTemplate_0" class="email-subject-storage email-template-storage">Hello candidate</textarea>' +
    '<div id="subjectCompose_0" class="email-subject-compose" contenteditable="true"></div>' +
    '<button type="button" class="email-template-token-subj" data-token-name="CandidateName">Insert subject</button>' +
    '<template class="hr-email-chip-source" data-token-name="CandidateName"><span class="hr-email-token" data-hr-token="CandidateName">Candidate name</span></template>' +
    '<textarea id="defaultBodyTemplate_0" class="email-template-body-html email-template-storage"><p>Stored body</p></textarea>' +
    '<div id="bodyCompose_0" class="email-body-compose" contenteditable="true"></div>' +
    '<button type="submit">Save</button>' +
    '</div></form>' +
    '<script>' + editorJs + '</script>' +
    '</body></html>';

var dom = new JSDOM(html, {
    runScripts: 'dangerously',
    url: 'http://127.0.0.1/email-templates'
});

if (dom.window.document.readyState === 'loading') {
    dom.window.document.dispatchEvent(new dom.window.Event('DOMContentLoaded', { bubbles: true }));
}

runChecks();

function runChecks() {
var document = dom.window.document;
var failures = [];

function assert(condition, message) {
    if (!condition) {
        failures.push(message);
    }
}

var subject = document.getElementById('subjectCompose_0');
var body = document.getElementById('bodyCompose_0');
var subjectTa = document.getElementById('defaultSubjectTemplate_0');
var bodyTa = document.getElementById('defaultBodyTemplate_0');

assert(typeof dom.window.tinymce === 'undefined', 'TinyMCE should not be defined');
assert(subject && subject.textContent.indexOf('Hello candidate') !== -1, 'Subject editor hydrates from storage');
assert(body && body.innerHTML.indexOf('Stored body') !== -1, 'Body editor hydrates from storage');

body.innerHTML = '<p>Updated body copy</p>';
body.dispatchEvent(new dom.window.Event('input', { bubbles: true }));
document.getElementById('emailTemplatesForm').dispatchEvent(new dom.window.Event('submit', { bubbles: true, cancelable: true }));
assert(bodyTa.value.indexOf('Updated body copy') !== -1, 'Body storage syncs on input/submit');

subject.innerHTML = 'Changed subject';
subject.dispatchEvent(new dom.window.Event('input', { bubbles: true }));
document.getElementById('emailTemplatesForm').dispatchEvent(new dom.window.Event('submit', { bubbles: true, cancelable: true }));
assert(subjectTa.value.indexOf('Changed subject') !== -1, 'Subject storage syncs on input/submit');

if (failures.length) {
    console.error('Email template editor behavior checks failed:');
    failures.forEach(function (item) {
        console.error(' - ' + item);
    });
    process.exit(1);
}

console.log('PASS Email template editor hydrates and syncs subject/body without TinyMCE.');
}
