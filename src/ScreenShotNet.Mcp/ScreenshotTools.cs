using System.ComponentModel;
using System.Drawing;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace ScreenShotNet.Mcp;

[McpServerToolType]
public static class ScreenshotTools
{
    [McpServerTool(Name = "capture_screenshot"), Description("Capture a rectangular region of the Windows desktop and return the screenshot image directly.")]
    public static IEnumerable<AIContent> CaptureScreenshot(
        [Description("Left edge of the capture region in screen pixels.")] int x,
        [Description("Top edge of the capture region in screen pixels.")] int y,
        [Description("Capture width in pixels. Must be greater than 0.")] int width,
        [Description("Capture height in pixels. Must be greater than 0.")] int height,
        [Description("Optional delay before capture in seconds.")] double delaySeconds = 0,
        [Description("Optional window title prefix. If provided, the first visible top-level window whose title starts with this value is restored and brought to the foreground before capture.")] string? windowTitle = null,
        [Description("Optional output image format for the returned image and saved file. Supported values: png, jpg, bmp, gif, tiff.")] string format = "png",
        [Description("Optional file path to also save the screenshot to.")] string? savePath = null,
        [Description("If true, also copy the screenshot to the Windows clipboard.")] bool copyToClipboard = false,
        [Description("Optional watermark text to draw onto the screenshot before returning it.")] string? watermarkText = null,
        [Description("Watermark X position in capture-local pixels. Required when watermarkText is set.")] int? watermarkX = null,
        [Description("Watermark Y position in capture-local pixels. Required when watermarkText is set.")] int? watermarkY = null,
        [Description("Optional watermark font size in points. Defaults to 24.")] float? watermarkSize = null,
        [Description("Optional watermark color as #RRGGBB, #AARRGGBB, or a known color name. Defaults to #80FFFFFF.")] string? watermarkColor = null)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be greater than zero.");
        }

        if (delaySeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delaySeconds), "Delay must be zero or greater.");
        }

        if (!ScreenshotOperations.TryNormalizeOutputFormat(format, out var normalizedFormat))
        {
            throw new ArgumentException("Invalid output format. Supported formats: png, jpg, bmp, gif, tiff.", nameof(format));
        }

        var watermark = BuildWatermarkOptions(watermarkText, watermarkX, watermarkY, watermarkSize, watermarkColor);

        using var screenshot = ScreenshotOperations.CaptureScreenshot(new Rectangle(x, y, width, height), delaySeconds, windowTitle);
        if (watermark != null)
        {
            ScreenshotOperations.ApplyWatermark(screenshot, watermark);
        }

        string? savedPath = null;
        if (!string.IsNullOrWhiteSpace(savePath))
        {
            if (!ScreenshotOperations.TrySaveScreenshotToFile(screenshot, savePath, normalizedFormat, out savedPath, out var fileError))
            {
                throw new InvalidOperationException(fileError);
            }
        }

        if (copyToClipboard)
        {
            if (!ScreenshotOperations.TrySetClipboardImageWithRetry(screenshot, maxAttempts: 8, delayMilliseconds: 75, out var clipboardError))
            {
                throw new InvalidOperationException(clipboardError);
            }
        }

        var summary = $"Captured {width}x{height} at {x},{y} as {normalizedFormat}.";
        if (!string.IsNullOrWhiteSpace(windowTitle))
        {
            summary += $" Activated window prefix '{windowTitle}'.";
        }

        if (!string.IsNullOrWhiteSpace(savedPath))
        {
            summary += $" Saved to {savedPath}.";
        }

        if (copyToClipboard)
        {
            summary += " Copied to clipboard.";
        }

        return
        [
            new TextContent(summary),
            new DataContent(ScreenshotOperations.ToDataUri(screenshot, normalizedFormat))
        ];
    }

    private static WatermarkOptions? BuildWatermarkOptions(string? watermarkText, int? watermarkX, int? watermarkY, float? watermarkSize, string? watermarkColor)
    {
        var hasText = !string.IsNullOrWhiteSpace(watermarkText);
        var hasAdditionalWatermarkFields = watermarkX.HasValue || watermarkY.HasValue || watermarkSize.HasValue || !string.IsNullOrWhiteSpace(watermarkColor);

        if (!hasText)
        {
            if (hasAdditionalWatermarkFields)
            {
                throw new ArgumentException("Watermark position, size, and color require watermarkText.");
            }

            return null;
        }

        if (!watermarkX.HasValue || !watermarkY.HasValue)
        {
            throw new ArgumentException("watermarkX and watermarkY are required when watermarkText is set.");
        }

        var parsedWatermarkColor = Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF);
        if (!string.IsNullOrWhiteSpace(watermarkColor) && !CliArgumentParser.TryParseColor(watermarkColor, out parsedWatermarkColor))
        {
            throw new ArgumentException("Invalid watermark color. Use #RRGGBB, #AARRGGBB, or a known color name.", nameof(watermarkColor));
        }

        if (watermarkSize.HasValue && watermarkSize.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(watermarkSize), "Watermark size must be greater than zero.");
        }

        return new WatermarkOptions
        {
            Text = watermarkText,
            X = watermarkX.Value,
            Y = watermarkY.Value,
            Size = watermarkSize ?? 24f,
            Color = parsedWatermarkColor
        };
    }
}
