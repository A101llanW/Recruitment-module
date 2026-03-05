using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace HR.Web.Services
{
    public class CaptchaResult
    {
        public string CaptchaCode { get; set; }
        public byte[] CaptchaImageBytes { get; set; }
        public string CaptchaBase64 => Convert.ToBase64String(CaptchaImageBytes);
    }

    public class CaptchaService
    {
        public CaptchaResult GenerateCaptcha(int width = 140, int height = 45)
        {
            var code = GenerateRandomCode(5);
            var result = new CaptchaResult { CaptchaCode = code };

            using (var bitmap = new Bitmap(width, height))
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.White);

                // Add some noise (lines)
                var random = new Random();
                using (var pen = new Pen(Color.LightGray, 1))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        g.DrawLine(pen, random.Next(0, width), random.Next(0, height), random.Next(0, width), random.Next(0, height));
                    }
                }

                // Add some noise (dots)
                for (int i = 0; i < 100; i++)
                {
                    int x = random.Next(0, width);
                    int y = random.Next(0, height);
                    bitmap.SetPixel(x, y, Color.FromArgb(random.Next(200, 255), random.Next(200, 255), random.Next(200, 255)));
                }

                // Draw the text
                using (var font = new Font("Arial", 22, FontStyle.Bold | FontStyle.Italic))
                using (var brush = new LinearGradientBrush(new Rectangle(0, 0, width, height), Color.FromArgb(79, 70, 229), Color.FromArgb(129, 140, 248), 45f))
                {
                    // Center the text slightly randomized
                    var size = g.MeasureString(code, font);
                    float x = (width - size.Width) / 2;
                    float y = (height - size.Height) / 2;
                    
                    // Char by char for slight rotation
                    float currentX = x;
                    foreach (char c in code)
                    {
                        var state = g.Save();
                        g.TranslateTransform(currentX + 5, y + size.Height / 2);
                        g.RotateTransform(random.Next(-15, 15));
                        g.DrawString(c.ToString(), font, brush, 0, -size.Height / 2);
                        g.Restore(state);
                        currentX += (size.Width / code.Length);
                    }
                }

                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    result.CaptchaImageBytes = ms.ToArray();
                }
            }

            return result;
        }

        private string GenerateRandomCode(int length)
        {
            var chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Removed ambiguous chars like 0, O, 1, I, L
            var random = new Random();
            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[random.Next(chars.Length)];
            }
            return new string(result);
        }
    }
}
