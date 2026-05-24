import pathlib

root = pathlib.Path(__file__).resolve().parents[2] / "HR.Web"
for name in ("Create", "Edit"):
    path = root / f"Views/Positions/{name}.cshtml"
    content = path.read_text(encoding="utf-8")
    start = content.index("    function initQuestionWeightAllocator()")
    end = content.index("    document.addEventListener('DOMContentLoaded'", start)
    replacement = """    function initQuestionWeightAllocator() {
        return initQuestionnaireAssignmentEditor({
            formId: document.getElementById('positionCreateForm') ? 'positionCreateForm' : 'positionEditForm'
        });
    }

    function initQuestionnaireTemplateApply(editor) {
        var select = document.getElementById('questionnaireTemplateSelect');
        var applyBtn = document.getElementById('applyQuestionnaireTemplateBtn');
        var statusEl = document.getElementById('questionnaireTemplateApplyStatus');
        if (!select || !applyBtn || !editor) {
            return;
        }

        applyBtn.addEventListener('click', function () {
            var templateId = select.value;
            if (!templateId) {
                if (statusEl) {
                    statusEl.textContent = 'Choose a template first.';
                }
                return;
            }

            applyBtn.disabled = true;
            if (statusEl) {
                statusEl.textContent = 'Applying template...';
            }

            fetch('/Admin/GetQuestionnaireTemplateData?id=' + encodeURIComponent(templateId), {
                credentials: 'same-origin'
            })
                .then(function (response) { return response.json(); })
                .then(function (data) {
                    if (!data || !data.success || !data.template) {
                        throw new Error((data && data.message) || 'Unable to load template.');
                    }
                    editor.applyTemplateItems(data.template);
                    if (statusEl) {
                        statusEl.textContent = 'Template applied. You can add more questions below.';
                    }
                })
                .catch(function (err) {
                    if (statusEl) {
                        statusEl.textContent = err.message || 'Failed to apply template.';
                    }
                })
                .finally(function () {
                    applyBtn.disabled = false;
                });
        });
    }

"""
    content = content[:start] + replacement + content[end:]
    marker = "<hr />\n                <div class=\"form-group mb-3\">\n                    <label class=\"font-weight-bold\">Questionnaire questions</label>"
    template_panel = """<div class="card border mb-3" id="questionnaireTemplatePanel">
                    <div class="card-header bg-light">
                        <strong>Start from questionnaire template</strong>
                        <small class="text-muted d-block">Apply a saved template, then add role-specific questions below.</small>
                    </div>
                    <div class="card-body">
                        <div class="d-flex flex-wrap align-items-end">
                            <div class="form-group mb-0 mr-2 flex-grow-1" style="min-width: 220px;">
                                <label class="small font-weight-bold" for="questionnaireTemplateSelect">Template</label>
                                <select id="questionnaireTemplateSelect" class="form-control">
                                    <option value="">-- Select template --</option>
                                    @{
                                        var questionnaireTemplates = ViewBag.QuestionnaireTemplates as IEnumerable<HR.Web.ViewModels.QuestionnaireTemplateListItemViewModel>;
                                        if (questionnaireTemplates != null)
                                        {
                                            foreach (var template in questionnaireTemplates)
                                            {
                                                <option value="@template.Id">@template.Name (@template.QuestionCount questions)</option>
                                            }
                                        }
                                    }
                                </select>
                            </div>
                            <button type="button" id="applyQuestionnaireTemplateBtn" class="btn btn-outline-primary mb-0">Apply Template</button>
                        </div>
                        <small id="questionnaireTemplateApplyStatus" class="form-text text-muted mt-2"></small>
                    </div>
                </div>

                <hr />
                <div class="form-group mb-3">
                    <label class="font-weight-bold">Questionnaire questions</label>"""
    if marker in content:
        content = content.replace(marker, template_panel, 1)
    scripts_marker = "@section Scripts {\n<script>"
    scripts_replacement = "@section Scripts {\n<script src=\"@Url.Content(\"~/Scripts/questionnaire-assignment.js\")\"></script>\n<script>"
    if scripts_marker in content and "questionnaire-assignment.js" not in content:
        content = content.replace(scripts_marker, scripts_replacement, 1)
    dom_marker = "        initPassMarkControl();\n        initQuestionWeightAllocator();"
    dom_replacement = "        initPassMarkControl();\n        var questionnaireEditor = initQuestionWeightAllocator();\n        initQuestionnaireTemplateApply(questionnaireEditor);"
    content = content.replace(dom_marker, dom_replacement, 1)
    path.write_text(content, encoding="utf-8")
    print("Updated", path.name)
