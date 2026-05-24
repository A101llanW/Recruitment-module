using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Services;
using HR.Web.ViewModels;

namespace HR.Web.Controllers
{
    public partial class AdminController
    {
        private readonly QuestionnaireTemplateService _questionnaireTemplateService = new QuestionnaireTemplateService();

        public ActionResult QuestionnaireTemplates()
        {
            var templates = _questionnaireTemplateService.GetActiveTemplatesForCurrentTenant();
            return View(templates);
        }

        public ActionResult EditQuestionnaireTemplate(int? id)
        {
            var model = _questionnaireTemplateService.BuildEditViewModel(id);
            if (model == null && id.HasValue && id.Value > 0)
            {
                return HttpNotFound();
            }

            return View(model ?? new QuestionnaireTemplateEditViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditQuestionnaireTemplate(
            QuestionnaireTemplateEditViewModel model,
            int[] selectedQuestions,
            string questionWeightValues,
            string questionStagesPayload)
        {
            if (model == null)
            {
                ModelState.AddModelError(string.Empty, "Template data is required.");
                return View(new QuestionnaireTemplateEditViewModel());
            }

            var questionWeights = ParseTemplateQuestionWeights(questionWeightValues);
            var questionStages = ParseTemplateQuestionStages(questionStagesPayload, selectedQuestions, model.StageCount);
            var assignments = BuildTemplateAssignments(selectedQuestions, questionWeights, questionStages);

            var error = _questionnaireTemplateService.SaveTemplate(
                model.Id > 0 ? (int?)model.Id : null,
                model.Name,
                model.Description,
                model.StageCount,
                assignments);

            if (!string.IsNullOrEmpty(error))
            {
                ModelState.AddModelError(string.Empty, error);
                var reload = _questionnaireTemplateService.BuildEditViewModel(model.Id > 0 ? (int?)model.Id : null)
                             ?? new QuestionnaireTemplateEditViewModel();
                reload.Name = model.Name;
                reload.Description = model.Description;
                reload.StageCount = model.StageCount;
                reload.SelectedQuestionIds = selectedQuestions != null ? selectedQuestions.ToList() : new List<int>();
                reload.SelectedQuestionWeights = questionWeights != null
                    ? new Dictionary<int, decimal>(questionWeights)
                    : new Dictionary<int, decimal>();
                reload.SelectedQuestionStages = questionStages != null
                    ? new Dictionary<int, int>(questionStages)
                    : new Dictionary<int, int>();
                return View(reload);
            }

            TempData["Message"] = model.Id > 0
                ? "Questionnaire template updated successfully."
                : "Questionnaire template created successfully.";
            return RedirectToAction("QuestionnaireTemplates");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteQuestionnaireTemplate(int id)
        {
            var error = _questionnaireTemplateService.SoftDeleteTemplate(id);
            if (!string.IsNullOrEmpty(error))
            {
                TempData["ErrorMessage"] = error;
            }
            else
            {
                TempData["Message"] = "Questionnaire template removed.";
            }

            return RedirectToAction("QuestionnaireTemplates");
        }

        [HttpGet]
        public ActionResult GetQuestionnaireTemplateData(int id)
        {
            var payload = _questionnaireTemplateService.BuildTemplatePayload(id);
            if (payload == null)
            {
                return Json(new { success = false, message = "Template not found or access denied." }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { success = true, template = payload }, JsonRequestBehavior.AllowGet);
        }

        private static IList<QuestionnaireTemplateAssignmentInput> BuildTemplateAssignments(
            int[] selectedQuestions,
            IDictionary<int, decimal> questionWeights,
            IDictionary<int, int> questionStages)
        {
            var assignments = new List<QuestionnaireTemplateAssignmentInput>();
            if (selectedQuestions == null || selectedQuestions.Length == 0)
            {
                return assignments;
            }

            var order = 1;
            foreach (var questionId in selectedQuestions.Distinct())
            {
                decimal weight;
                if (questionWeights == null || !questionWeights.TryGetValue(questionId, out weight))
                {
                    weight = 0m;
                }

                int stageNumber;
                if (questionStages == null || !questionStages.TryGetValue(questionId, out stageNumber))
                {
                    stageNumber = 1;
                }

                assignments.Add(new QuestionnaireTemplateAssignmentInput
                {
                    QuestionId = questionId,
                    Order = order++,
                    Weight = weight,
                    StageNumber = stageNumber
                });
            }

            return assignments;
        }

        private static IDictionary<int, decimal> ParseTemplateQuestionWeights(string payload)
        {
            var weights = new Dictionary<int, decimal>();
            if (string.IsNullOrWhiteSpace(payload))
            {
                return weights;
            }

            foreach (var entry in payload.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split('=');
                if (parts.Length != 2)
                {
                    continue;
                }

                int questionId;
                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out questionId) || questionId <= 0)
                {
                    continue;
                }

                decimal weight;
                if (!decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out weight))
                {
                    continue;
                }

                weights[questionId] = Math.Max(0m, Math.Min(100m, weight));
            }

            return weights;
        }

        private static IDictionary<int, int> ParseTemplateQuestionStages(string payload, int[] selectedQuestions, int stageCount)
        {
            var stages = new Dictionary<int, int>();
            if (selectedQuestions != null)
            {
                foreach (var questionId in selectedQuestions.Distinct())
                {
                    if (questionId > 0)
                    {
                        stages[questionId] = 1;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(payload))
            {
                return stages;
            }

            var max = Math.Max(1, stageCount);
            foreach (var entry in payload.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Split('=');
                if (parts.Length != 2)
                {
                    continue;
                }

                int questionId;
                if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out questionId) || questionId <= 0)
                {
                    continue;
                }

                int stage;
                if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out stage))
                {
                    continue;
                }

                stages[questionId] = Math.Max(1, Math.Min(max, stage));
            }

            return stages;
        }
    }
}
