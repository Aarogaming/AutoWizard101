using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

internal class Program
{
	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

	[StructLayout(LayoutKind.Sequential)]
	public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

	static int Main(string[] args)
	{
		try
		{
			Run().GetAwaiter().GetResult();
			return 0;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine("Integration test failed: " + ex);
			return 2;
		}
	}

	static async Task Run()
	{
		Console.WriteLine("Searching for window titled 'Wizard101'...");
		IntPtr hwnd = FindWindow(null, "Wizard101");
		Console.WriteLine("Raw FindWindow handle: " + hwnd);
		if (hwnd == IntPtr.Zero)
		{
			Console.WriteLine("Window not found by exact title. Enumerating top-level windows for titles containing 'Wizard101'...");
			var processes = System.Diagnostics.Process.GetProcesses().Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) && p.MainWindowTitle.IndexOf("Wizard101", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
			if (processes.Count == 0)
			{
				Console.WriteLine("No process with window title containing 'Wizard101' found. Aborting.");
				return;
			}

			hwnd = processes[0].MainWindowHandle;
			Console.WriteLine("Using process window handle: " + hwnd + " title='" + processes[0].MainWindowTitle + "'");
		}

		if (!GetWindowRect(hwnd, out RECT rect))
			throw new Exception("Failed to get window rect");

		int width = rect.Right - rect.Left;
		int height = rect.Bottom - rect.Top;
		Console.WriteLine($"Window rect: Left={rect.Left} Top={rect.Top} W={width} H={height}");

		string screenshotsDir = Path.Combine(Directory.GetCurrentDirectory(), "screenshots");
		Directory.CreateDirectory(screenshotsDir);
		string screenshotPath = Path.Combine(screenshotsDir, "integration_screenshot.png");

		using (var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb))
		using (var g = Graphics.FromImage(bmp))
		{
			g.CopyFromScreen(new Point(rect.Left, rect.Top), Point.Empty, new Size(width, height), CopyPixelOperation.SourceCopy);
			bmp.Save(screenshotPath, ImageFormat.Png);
		}

		Console.WriteLine("Saved screenshot to: " + screenshotPath);

		// OCR
		string apiKey = Environment.GetEnvironmentVariable("OCR_SPACE_APIKEY");
		if (string.IsNullOrEmpty(apiKey))
		{
			Console.WriteLine("Environment variable OCR_SPACE_APIKEY is not set. Set it to run OCR or press Enter to skip OCR.");
			if (Console.KeyAvailable == false)
			{
				Console.ReadLine();
			}
			return;
		}

		using var httpClient = new HttpClient();
		httpClient.DefaultRequestHeaders.Add("apikey", apiKey);
		using var formData = new MultipartFormDataContent();
		using var fs = File.OpenRead(screenshotPath);
		using var streamContent = new StreamContent(fs);
		var bytes = await streamContent.ReadAsByteArrayAsync();
		var fileContent = new ByteArrayContent(bytes);
		fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
		formData.Add(fileContent, "file", Path.GetFileName(screenshotPath));

		Console.WriteLine("Posting to OCR API...");
		var response = await httpClient.PostAsync("https://api.ocr.space/parse/image", formData);
		var json = await response.Content.ReadAsStringAsync();
		Console.WriteLine("OCR response: \n" + json);
	}
}
