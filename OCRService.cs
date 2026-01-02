using System;
using System.Drawing;
using System.IO;
using Tesseract;

namespace ConsoleApp2
{
    public class OCRService
    {
        private static readonly string TessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");

        public static OCRResult PerformOCR(string imagePath, string language = "eng")
        {
            var result = new OCRResult();

            try
            {
                Console.WriteLine($"?? Starting OCR on: {Path.GetFileName(imagePath)}");
                Console.WriteLine($"   Language: {language}");

                // Check if tessdata exists
                if (!Directory.Exists(TessDataPath))
                {
                    throw new Exception($"Tessdata folder not found at: {TessDataPath}. Please download language data from https://github.com/tesseract-ocr/tessdata");
                }

                // For multi-language (e.g., "eng+urd"), check each language separately
                var languages = language.Split('+');
                foreach (var lang in languages)
                {
                    var langFile = Path.Combine(TessDataPath, $"{lang.Trim()}.traineddata");
                    if (!File.Exists(langFile))
                    {
                        throw new Exception($"Language file not found: {langFile}. Download '{lang}.traineddata' from https://github.com/tesseract-ocr/tessdata");
                    }
                }

                // Preprocess image for better OCR accuracy
                var preprocessedPath = PreprocessImage(imagePath);

                // Perform OCR
                using (var engine = new TesseractEngine(TessDataPath, language, EngineMode.Default))
                {
                    // Configure for better accuracy
                    engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-/ ");
                    
                    using (var img = Pix.LoadFromFile(preprocessedPath))
                    {
                        using (var page = engine.Process(img))
                        {
                            result.ExtractedText = page.GetText();
                            result.Confidence = page.GetMeanConfidence();
                            result.Success = true;

                            Console.WriteLine($"? OCR Complete:");
                            Console.WriteLine($"   Confidence: {result.Confidence:P}");
                            Console.WriteLine($"   Text length: {result.ExtractedText?.Length ?? 0} characters");
                            if (!string.IsNullOrEmpty(result.ExtractedText) && result.ExtractedText.Length > 0)
                            {
                                var previewLength = Math.Min(100, result.ExtractedText.Length);
                                var preview = result.ExtractedText.Substring(0, previewLength).Replace("\n", " ");
                                Console.WriteLine($"   Preview: {preview}...");
                            }
                        }
                    }
                }

                // Cleanup preprocessed image
                try { File.Delete(preprocessedPath); } catch { }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? OCR Error: {ex.Message}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private static string PreprocessImage(string imagePath)
        {
            try
            {
                var preprocessedPath = Path.Combine(Path.GetTempPath(), $"ocr_preprocessed_{Guid.NewGuid()}.png");

                using (var original = new Bitmap(imagePath))
                {
                    // Create a properly sized bitmap
                    var processed = new Bitmap(original.Width, original.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    
                    using (var g = Graphics.FromImage(processed))
                    {
                        // High quality rendering
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        
                        // Draw original image
                        g.DrawImage(original, 0, 0, original.Width, original.Height);
                    }
                    
                    // Save with high quality
                    processed.Save(preprocessedPath, System.Drawing.Imaging.ImageFormat.Png);
                    processed.Dispose();
                }

                Console.WriteLine($"   ? Image preprocessed: {Path.GetFileName(preprocessedPath)}");
                return preprocessedPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ? Preprocessing failed: {ex.Message}, using original");
                return imagePath;
            }
        }

        public static OCRResult PerformMultiLanguageOCR(string imagePath)
        {
            // Try English + Urdu
            Console.WriteLine("?? Attempting multi-language OCR (English + Urdu)...");

            var result = new OCRResult();

            try
            {
                // Check available languages
                var languages = new[] { "eng", "urd" }; // English and Urdu
                var availableLangs = new System.Collections.Generic.List<string>();

                foreach (var lang in languages)
                {
                    var langFile = Path.Combine(TessDataPath, $"{lang}.traineddata");
                    if (File.Exists(langFile))
                    {
                        availableLangs.Add(lang);
                    }
                }

                if (availableLangs.Count == 0)
                {
                    throw new Exception("No language files found. Download from https://github.com/tesseract-ocr/tessdata");
                }

                var langString = string.Join("+", availableLangs);
                Console.WriteLine($"   Using languages: {langString}");

                return PerformOCR(imagePath, langString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Multi-language OCR failed: {ex.Message}");
                Console.WriteLine("   Falling back to English only...");
                return PerformOCR(imagePath, "eng");
            }
        }
    }

    public class OCRResult
    {
        public bool Success { get; set; }
        public string ExtractedText { get; set; }
        public float Confidence { get; set; }
        public string ErrorMessage { get; set; }
    }
}
