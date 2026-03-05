using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using HR.Web.Services;
using HR.Web.Models;
using HR.Web.Filters;

namespace HR.Web.Controllers
{
    [Authorize(Roles = "Admin, HR, SuperAdmin")]
    [RoleBasedAuthorization("Admin", "HR")]
    public class MLController : Controller
    {
        private readonly MLQuestionnaireService _mlService = new MLQuestionnaireService();

        [HttpGet]
        public ActionResult SmartGeneration()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerateSmartQuestions(string jobTitle, string jobDescription, 
            string keyResponsibilities = "", string requiredQualifications = "",
            string experience = "mid", List<string> questionTypes = null, int count = 5)
        {
            try
            {
                var questions = _mlService.GenerateSmartQuestions(
                    jobTitle, jobDescription, keyResponsibilities, requiredQualifications,
                    experience, questionTypes, count);

                return Json(new
                {
                    success = true,
                    questions = questions.Select(q => new
                    {
                        text = q.Text,
                        type = q.Type,
                        category = q.Category,
                        isRequired = q.IsRequired,
                        options = q.Options != null ? q.Options.Select(o => new { text = o.Text, points = o.Points }) : null
                    }),
                    metadata = new
                    {
                        jobTitle,
                        experience,
                        generatedAt = DateTime.UtcNow,
                        algorithm = "ML-Enhanced",
                        features = new[] { "Industry Detection", "Weighted Keywords", "Category Prediction", "ML Scoring" }
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AnalyzeQuestion(string questionText, string questionType = "Text")
        {
            try
            {
                var validation = _mlService.ValidateQuestion(questionText, questionType);
                var score = _mlService.CalculateQuestionScore(questionText, validation.Category ?? "general");
                
                return Json(new
                {
                    success = true,
                    isValid = validation.IsValid,
                    category = validation.Category,
                    score = score,
                    warnings = validation.Warnings,
                    suggestions = validation.Suggestions,
                    biasedTerms = validation.BiasedTerms
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
