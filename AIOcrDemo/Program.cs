using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using PdfiumViewer;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

class PDFToImageAndOCR
{
    private static readonly HttpClient client = new HttpClient();
    private const string OpenAIApiKey = "your-api-key";
    private const string OpenAIApiUrl = "https://api.openai.com/v1/chat/completions";

    static async Task Main(string[] args)
    {
        string pdfPath = @"pdf\test.pdf";
        string outputFolder = @"output";
        string textOutputFolder = Path.Combine(outputFolder, "text_files");

        ExtractPagesAsImages(pdfPath, outputFolder);
        await PerformOCROnExtractedImages(outputFolder, textOutputFolder);
    }

    static void ExtractPagesAsImages(string pdfPath, string outputFolder)
    {
        // Ensure output folder exists
        Directory.CreateDirectory(outputFolder);

        // Load the PDF document
        using (var document = PdfDocument.Load(pdfPath))
        {
            // Set DPI for high quality (300 DPI is standard for print quality)
            const int DPI = 300;

            // Process each page
            for (int pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
            {
                // Get page size in pixels at specified DPI
                var pageSize = document.PageSizes[pageIndex];
                int width = (int)(pageSize.Width / 72 * DPI);
                int height = (int)(pageSize.Height / 72 * DPI);

                // Render page to image
                using (var image = document.Render(pageIndex, width, height, DPI, DPI, PdfRenderFlags.ForPrinting))
                {
                    // Save image as PNG
                    string outputPath = Path.Combine(outputFolder, $"page_{pageIndex + 1}.png");
                    image.Save(outputPath, ImageFormat.Png);
                    Console.WriteLine($"Saved page {pageIndex + 1} as {outputPath}");
                }
            }
        }
    }

    static async Task PerformOCROnExtractedImages(string imageFolder, string textOutputFolder)
    {
        // Ensure text output folder exists
        Directory.CreateDirectory(textOutputFolder);

        foreach (string imagePath in Directory.GetFiles(imageFolder, "*.png"))
        {
            string base64Image = Convert.ToBase64String(File.ReadAllBytes(imagePath));
            string ocrResult = await GetOCRFromOpenAI(base64Image);

            string textFileName = Path.GetFileNameWithoutExtension(imagePath) + ".txt";
            string textFilePath = Path.Combine(textOutputFolder, textFileName);
            File.WriteAllText(textFilePath, ocrResult);
            Console.WriteLine($"OCR result saved for {Path.GetFileName(imagePath)} in {textFilePath}");
        }
    }

    static async Task<string> GetOCRFromOpenAI(string base64Image)
    {
        var requestBody = new
        {
            model = "gpt-4o-mini",
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = "Please perform OCR on this image and return only the extracted text." },
                        new { type = "image_url", image_url = new { url = $"data:image/png;base64,{base64Image}" } }
                    }
                }
            },
            max_tokens = 8000
        };

        var request = new HttpRequestMessage(HttpMethod.Post, OpenAIApiUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };

        request.Headers.Add("Authorization", $"Bearer {OpenAIApiKey}");

        var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
            return jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
        else
        {
            throw new Exception($"Error calling OpenAI API: {responseBody}");
        }
    }
}