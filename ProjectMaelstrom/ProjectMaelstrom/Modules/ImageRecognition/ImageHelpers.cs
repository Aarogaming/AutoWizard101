using Emgu.CV;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using Tesseract;

namespace ProjectMaelstrom.Modules.ImageRecognition;

internal static class ImageHelpers
{
    public static async Task<string> ExtractTextFromImage(string imagePath)
    {
        ImageHelpers.ConvertImageToGrayscale(imagePath);
        string apiKey = Properties.Settings.Default["OCR_SPACE_APIKEY"]?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(apiKey))
        {
            return ExtractTextWithLocalOcr(imagePath);
        }
        string apiUrl = "https://api.ocr.space/parse/image";

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("apikey", apiKey);

        using var formData = new MultipartFormDataContent();

        using FileStream fileStream = File.OpenRead(imagePath);
        using var streamContent = new StreamContent(fileStream);
        using var fileContent = new ByteArrayContent(await streamContent.ReadAsByteArrayAsync());

        string fileExtension = Path.GetExtension(imagePath).ToLower();
        string contentType = fileExtension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            _ => throw new NotSupportedException($"File extension {fileExtension} is not supported")
        };

        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        formData.Add(fileContent, "file", Path.GetFileName(imagePath));

        var response = await httpClient.PostAsync(apiUrl, formData);
        response.EnsureSuccessStatusCode();

        string jsonResponse = await response.Content.ReadAsStringAsync();
        JObject parsedResponse = JObject.Parse(jsonResponse);
        if (parsedResponse["ParsedResults"] is JArray results && results.Count > 0 && results[0]["ParsedText"] is JToken textToken)
        {
            string text = textToken.ToString();
            return text;
        }
        else
        {
            throw new Exception("OCR response parsing failed: Invalid JSON structure.");
        }
    }

    private static string ExtractTextWithLocalOcr(string imagePath)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string? tessdataPath = FindTessdataPath(baseDir);

        if (tessdataPath == null)
        {
            throw new InvalidOperationException("Local OCR fallback failed: tessdata folder not found. Place English trained data in a 'tessdata' folder next to the executable.");
        }

        try
        {
            using var engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default);
            using var img = Pix.LoadFromFile(imagePath);
            using var page = engine.Process(img);
            return page.GetText();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Local OCR fallback failed: {ex.Message}", ex);
        }
    }

    private static string? FindTessdataPath(string baseDir)
    {
        string candidate = Path.Combine(baseDir, "tessdata");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        candidate = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        return null;
    }

    public static void ConvertImageToGrayscale(string imagePath)
    {
        using var img = new Mat(imagePath);
        using var grayImg = new Mat();
        CvInvoke.CvtColor(img, grayImg, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
        grayImg.Clone().Save(imagePath);
    }
}
