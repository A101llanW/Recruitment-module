using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using HR.Web.Models;

namespace HR.Web.Services
{
    public class RealisticCaptchaService
    {
        private static readonly Random _random = new Random();
        private static readonly string[] _fonts = new[] { "Arial", "Verdana", "Tahoma", "Georgia" };
        private static readonly Color[] _colors = new[] { 
            Color.FromArgb(50, 50, 50), Color.FromArgb(100, 50, 150), 
            Color.FromArgb(150, 50, 100), Color.FromArgb(50, 100, 150) 
        };
        
        private static readonly Color[] _backgroundColors = new[] {
            Color.FromArgb(240, 248, 255), Color.FromArgb(255, 250, 240),
            Color.FromArgb(250, 250, 250), Color.FromArgb(248, 248, 255)
        };

        public CaptchaResponse GenerateCaptcha()
        {
            var width = 200;
            var height = 80;
            var text = GenerateRandomText(6);
            
            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                // Set high quality
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
                
                // Background
                var bgColor = _backgroundColors[_random.Next(_backgroundColors.Length)];
                using (var brush = new SolidBrush(bgColor))
                {
                    graphics.FillRectangle(brush, 0, 0, width, height);
                }
                
                // Add noise
                AddNoise(graphics, width, height);
                
                // Add interference lines
                AddInterferenceLines(graphics, width, height);
                
                // Draw text
                DrawText(graphics, text, width, height);
                
                // Convert to base64
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    var base64 = Convert.ToBase64String(ms.ToArray());
                    
                    return new CaptchaResponse
                    {
                        CaptchaId = Guid.NewGuid().ToString(),
                        CaptchaText = text,
                        CaptchaBase64 = base64,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                    };
                }
            }
        }

        public bool ValidateCaptcha(string captchaId, string userInput)
        {
            // In a real implementation, you'd store the captcha text in cache/database
            // For now, we'll use a simple validation
            return !string.IsNullOrEmpty(userInput) && userInput.Length >= 4;
        }

        private string GenerateRandomText(int length)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[_random.Next(s.Length)]).ToArray());
        }

        private void AddNoise(Graphics graphics, int width, int height)
        {
            for (int i = 0; i < 100; i++)
            {
                var x = _random.Next(width);
                var y = _random.Next(height);
                var color = Color.FromArgb(_random.Next(100, 200), _random.Next(100, 200), _random.Next(100, 200));
                using (var brush = new SolidBrush(color))
                {
                    graphics.FillEllipse(brush, x, y, 1, 1);
                }
            }
        }

        private void AddInterferenceLines(Graphics graphics, int width, int height)
        {
            for (int i = 0; i < 5; i++)
            {
                var x1 = _random.Next(width);
                var y1 = _random.Next(height);
                var x2 = _random.Next(width);
                var y2 = _random.Next(height);
                var color = Color.FromArgb(_random.Next(50, 150), _random.Next(50, 150), _random.Next(50, 150));
                using (var pen = new Pen(color, 1))
                {
                    graphics.DrawLine(pen, x1, y1, x2, y2);
                }
            }
        }

        private void DrawText(Graphics graphics, string text, int width, int height)
        {
            var fontSize = 24;
            var font = new Font(_fonts[_random.Next(_fonts.Length)], fontSize, FontStyle.Bold);
            
            using (var brush = new SolidBrush(_colors[_random.Next(_colors.Length)]))
            {
                var textSize = graphics.MeasureString(text, font);
                var x = (width - textSize.Width) / 2;
                var y = (height - textSize.Height) / 2;
                
                // Add slight rotation
                graphics.TranslateTransform(x + textSize.Width / 2, y + textSize.Height / 2);
                graphics.RotateTransform(_random.Next(-10, 10));
                graphics.TranslateTransform(-(x + textSize.Width / 2), -(y + textSize.Height / 2));
                
                graphics.DrawString(text, font, brush, x, y);
                graphics.ResetTransform();
            }
        }
    }

    public class CaptchaResponse
    {
        public string CaptchaId { get; set; }
        public string CaptchaText { get; set; }
        public string CaptchaBase64 { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
