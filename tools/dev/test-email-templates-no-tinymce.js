'use strict';

var fs = require('fs');
var path = require('path');

var repoRoot = path.resolve(__dirname, '../..');
var viewPath = path.join(repoRoot, 'HR.Web/Views/Admin/EmailTemplates.cshtml');
var scriptPath = path.join(repoRoot, 'HR.Web/Scripts/email-template-editor.js');
var viewModelPath = path.join(repoRoot, 'HR.Web/ViewModels/EmailTemplateManagementViewModel.cs');
var controllerPath = path.join(repoRoot, 'HR.Web/Controllers/AdminController.EmailTemplates.cs');

var failures = [];

function read(relPath) {
    var full = path.isAbsolute(relPath) ? relPath : path.join(repoRoot, relPath);
    if (!fs.existsSync(full)) {
        failures.push('Missing file: ' + path.relative(repoRoot, full));
        return '';
    }
    return fs.readFileSync(full, 'utf8');
}

function assert(condition, message) {
    if (!condition) {
        failures.push(message);
    }
}

function assertNoTinyMce(filePath, contents) {
    var matches = contents.match(/tinymce/gi) || [];
    assert(matches.length === 0, filePath + ' still references TinyMCE (' + matches.length + ' hits)');
    assert(!/cdn\.jsdelivr\.net\/npm\/tinymce/i.test(contents), filePath + ' still loads TinyMCE from a CDN');
    assert(!/tinymce\.init/i.test(contents), filePath + ' still calls tinymce.init');
}

var view = read(viewPath);
var script = read(scriptPath);
var viewModel = read(viewModelPath);
var controller = read(controllerPath);

assert(view.length > 0, 'EmailTemplates.cshtml is empty');
assert(script.length > 0, 'email-template-editor.js is empty or missing');
assertNoTinyMce('HR.Web/Views/Admin/EmailTemplates.cshtml', view);
assertNoTinyMce('HR.Web/Scripts/email-template-editor.js', script);

assert(/SaveEmailTemplates/.test(view), 'View must post to SaveEmailTemplates');
assert(/AntiForgeryToken/.test(view), 'View must include an anti-forgery token');
assert(/email-subject-compose/.test(view) && /contenteditable/.test(view), 'Subject editor must remain contenteditable');
assert(/email-body-compose/.test(view), 'Body editor must use a contenteditable compose surface (email-body-compose)');
assert(/email-template-editor\.js/.test(view), 'View must include the lightweight editor script');
assert(/DefaultBodyTemplate/.test(view) && /DefaultSubjectTemplate/.test(view), 'View must bind subject and body template fields');
assert(/email-template-format-btn/.test(view), 'View must include the lightweight formatting toolbar');
assert(!/tox-tinymce/.test(view), 'View must not include TinyMCE chrome CSS');
assert(/DefaultSubjectTemplate/.test(viewModel) && /AllowHtml/.test(viewModel), 'Subject template must keep AllowHtml');
assert(/DefaultBodyTemplate/.test(viewModel) && (viewModel.match(/AllowHtml/g) || []).length >= 2, 'Body template must keep AllowHtml');
assert(/Authorize\(Roles = "Admin, SuperAdmin"\)/.test(controller), 'Controller must keep Admin/SuperAdmin auth');
assert(/ValidateAntiForgeryToken/.test(controller), 'Save must keep anti-forgery validation');
assert(/EditorHtmlToStorage/.test(controller) && /EditorSubjectHtmlToPlainStorage/.test(controller), 'Save must keep token chip serialization');
assert(/RedirectToAction\("EmailTemplates"/.test(controller), 'Save/reset must keep tenant-aware EmailTemplates redirect');

if (failures.length) {
    console.error('Email Templates TinyMCE regression checks failed:');
    failures.forEach(function (item) {
        console.error(' - ' + item);
    });
    process.exit(1);
}

console.log('PASS Email Templates pages have no TinyMCE and keep save/auth/token wiring.');
