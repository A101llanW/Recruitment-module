using System;
using System.Web.Mvc;
using HR.Web.Services;

namespace HR.Web.Controllers
{
    public class CaptchaController : Controller
    {
        private readonly RealisticCaptchaService _captchaService = new RealisticCaptchaService();

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
                // Check if captcha exists and hasn't expired
                var sessionText = Session["CaptchaText"] as string;
                var sessionExpiry = Session["CaptchaExpiry"] as DateTime?;
                var sessionId = Session["CaptchaId"] as string;
                
                if (string.IsNullOrEmpty(sessionText) || !sessionExpiry.HasValue || string.IsNullOrEmpty(sessionId))
                {
                    return Json(new { success = false, message = "CAPTCHA session expired" });
                }
                
                if (DateTime.UtcNow > sessionExpiry.Value)
                {
                    ClearCaptchaSession();
                    return Json(new { success = false, message = "CAPTCHA expired" });
                }
                
                if (sessionId != captchaId)
                {
                    return Json(new { success = false, message = "Invalid CAPTCHA ID" });
                }
                
                var isValid = _captchaService.ValidateCaptcha(captchaId, userInput);
                
                if (isValid && string.Equals(sessionText, userInput, StringComparison.OrdinalIgnoreCase))
                {
                    // Clear captcha after successful validation
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
