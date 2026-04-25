using System;
using System.Web.Mvc;
using HR.Web.Services;

namespace HR.Web.Controllers
{
    public class CaptchaController : Controller
    {
        private readonly RealisticCaptchaService _captchaService = new RealisticCaptchaService();

        private sealed class CaptchaSessionState
        {
            public string CaptchaText { get; set; }
            public DateTime? Expiry { get; set; }
            public string CaptchaId { get; set; }
        }

        [AllowAnonymous]
        // Test endpoint to verify controller is working
        public ActionResult Test()
        {
            return Json(new { success = true, message = "CaptchaController is working" }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        [AllowAnonymous]
        public JsonResult Generate()
        {
            try
            {
                var captcha = _captchaService.GenerateCaptcha();
                
                // Store captcha text in session for validation
                Session["CaptchaText"] = captcha.CaptchaText;
                Session["CaptchaExpiry"] = captcha.ExpiresAt;
                Session["CaptchaId"] = captcha.CaptchaId;
                
                return Json(new { 
                    success = true, 
                    captchaId = captcha.CaptchaId,
                    captchaImage = captcha.CaptchaBase64,
                    expiresAt = captcha.ExpiresAt
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public JsonResult Validate(string captchaId, string userInput)
        {
            try
            {
                var state = GetCaptchaSessionState();
                var validationErrorResult = ValidateCaptchaSessionState(state, captchaId);
                if (validationErrorResult != null)
                {
                    return validationErrorResult;
                }

                var isValid = _captchaService.ValidateCaptcha(captchaId, userInput);
                if (isValid && string.Equals(state.CaptchaText, userInput, StringComparison.OrdinalIgnoreCase))
                {
                    ClearCaptchaSession();
                    return Json(new { success = true });
                }

                return Json(new { success = false, message = "Invalid CAPTCHA" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private CaptchaSessionState GetCaptchaSessionState()
        {
            return new CaptchaSessionState
            {
                CaptchaText = Session["CaptchaText"] as string,
                Expiry = Session["CaptchaExpiry"] as DateTime?,
                CaptchaId = Session["CaptchaId"] as string
            };
        }

        private JsonResult ValidateCaptchaSessionState(CaptchaSessionState state, string captchaId)
        {
            if (string.IsNullOrEmpty(state.CaptchaText) || !state.Expiry.HasValue || string.IsNullOrEmpty(state.CaptchaId))
            {
                return Json(new { success = false, message = "CAPTCHA session expired" });
            }

            if (DateTime.UtcNow > state.Expiry.Value)
            {
                ClearCaptchaSession();
                return Json(new { success = false, message = "CAPTCHA expired" });
            }

            if (state.CaptchaId != captchaId)
            {
                return Json(new { success = false, message = "Invalid CAPTCHA ID" });
            }

            return null;
        }

        [HttpGet]
        [AllowAnonymous]
        public JsonResult Refresh()
        {
            try
            {
                // Clear existing captcha
                ClearCaptchaSession();
                
                // Generate new captcha
                var captcha = _captchaService.GenerateCaptcha();
                
                // Store new captcha in session
                Session["CaptchaText"] = captcha.CaptchaText;
                Session["CaptchaExpiry"] = captcha.ExpiresAt;
                Session["CaptchaId"] = captcha.CaptchaId;
                
                return Json(new { 
                    success = true, 
                    captchaId = captcha.CaptchaId,
                    captchaImage = captcha.CaptchaBase64,
                    expiresAt = captcha.ExpiresAt
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        private void ClearCaptchaSession()
        {
            Session.Remove("CaptchaText");
            Session.Remove("CaptchaExpiry");
            Session.Remove("CaptchaId");
        }
    }
}
