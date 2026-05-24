import pathlib

root = pathlib.Path(__file__).resolve().parents[2] / "HR.Web"
content = (root / "Views/Positions/Create.cshtml").read_text(encoding="utf-8")
start = content.index("function initQuestionWeightAllocator()")
end = content.index("document.addEventListener('DOMContentLoaded'", start)
fn = content[start:end]
fn = fn.replace(
    "function initQuestionWeightAllocator()",
    "function initQuestionnaireAssignmentEditor(options) {\n    options = options || {};",
    1,
)
fn = fn.replace(
    "var form = document.getElementById('positionCreateForm') || document.getElementById('positionEditForm');",
    "var form = document.getElementById(options.formId) || document.getElementById('positionCreateForm') || document.getElementById('positionEditForm') || document.getElementById('templateEditForm');",
    1,
)
fn = fn.replace(
    "var stageCountInput = document.getElementById('questionnaireStageCountInput');",
    "var stageCountInput = document.getElementById(options.stageCountInputId || 'questionnaireStageCountInput');",
    1,
)
fn = fn.replace(
    "var hasSecondaryCheckbox = document.getElementById('hasSecondaryStageCheckbox');",
    "var hasSecondaryCheckbox = document.getElementById(options.hasSecondaryStageCheckboxId || 'hasSecondaryStageCheckbox');",
    1,
)
fn = fn.replace(
    "var stageCountSection = document.getElementById('questionnaireStageCountSection');",
    "var stageCountSection = document.getElementById(options.stageCountSectionId || 'questionnaireStageCountSection');",
    1,
)
footer = """
    function applyTemplateItems(template) {
        if (!template || !template.questions || !template.questions.length) {
            return;
        }
        var desiredStageCount = parseInt(template.stageCount, 10);
        if (!isNaN(desiredStageCount) && desiredStageCount > getQuestionnaireStageCount()) {
            if (hasSecondaryCheckbox) {
                hasSecondaryCheckbox.checked = desiredStageCount > 1;
            }
            if (stageCountInput) {
                stageCountInput.value = String(desiredStageCount);
            }
            updateStageCountSectionVisibility();
            clampActiveEditorStage(getQuestionnaireStageCount());
        }
        template.questions.forEach(function (item) {
            var qid = String(item.questionId);
            if (isQuestionSelected[qid] === true) {
                return;
            }
            isQuestionSelected[qid] = true;
            var weight = parseFloat(item.weight);
            questionWeights[qid] = clamp(isNaN(weight) ? 0 : Math.round(weight), 0, 100);
            var stage = parseInt(item.stageNumber, 10);
            questionStages[qid] = isNaN(stage) || stage < 1 ? 1 : stage;
        });
        syncAllCheckboxVisuals();
        syncGroupSelectHeaders();
        renderRows();
    }

    var api = { applyTemplateItems: applyTemplateItems };
    window.questionnaireAssignmentEditor = api;
    return api;
}
"""
out = "// Shared questionnaire assignment editor\n" + fn.rstrip() + footer
out_path = root / "Scripts/questionnaire-assignment.js"
out_path.write_text(out, encoding="utf-8")
print("Wrote", out_path, "bytes", len(out))
