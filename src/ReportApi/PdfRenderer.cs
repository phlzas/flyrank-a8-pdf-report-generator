using System.Diagnostics;
using Microsoft.Playwright;

public static class PdfRenderer
{
    static readonly string EdgeExe =
        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";

    public static async Task<string> RenderToPdfAsync(string html, string outPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

        try
        {
            await RenderWithPlaywrightAsync(html, outPath);
            return "playwright-msedge";
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[PdfRenderer] Playwright failed ({ex.Message}); falling back to Edge headless.");
            await RenderWithEdgeHeadlessAsync(html, outPath);
            return "edge-headless";
        }
    }

    static async Task RenderWithPlaywrightAsync(string html, string outPath)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Channel = "msedge" });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.PdfAsync(new PagePdfOptions { Path = outPath, Format = "A4", PrintBackground = true });
    }

    static async Task RenderWithEdgeHeadlessAsync(string html, string outPath)
    {
        var tmpHtml = Path.Combine(Path.GetTempPath(), $"flyrank-report-{Guid.NewGuid():N}.html");
        await File.WriteAllTextAsync(tmpHtml, html);
        var uri = new Uri(tmpHtml).AbsoluteUri;

        var psi = new ProcessStartInfo
        {
            FileName = EdgeExe,
            Arguments = $"--headless=new --disable-gpu --no-pdf-header-footer --print-to-pdf=\"{outPath}\" \"{uri}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var proc = Process.Start(psi)!;
        await proc.StandardOutput.ReadToEndAsync();
        await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        File.Delete(tmpHtml);

        if (!File.Exists(outPath) || new FileInfo(outPath).Length == 0)
            throw new InvalidOperationException($"Edge headless did not produce a PDF at {outPath}.");
    }
}
