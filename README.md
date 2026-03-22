# ScreenShotNet

<!-- markdownlint-disable MD033 -->
<p align="center">
  <img src="ScreenShotNet.png" alt="ScreenShotNet" width="512" />
</p>

<p align="center">
  <a href="https://x.com/tobitege">
    <img src="https://img.shields.io/badge/X-%40tobitege-000000?logo=x&logoColor=white" alt="X @tobitege" />
  </a>
</p>

<p align="center">
ScreenShotNet captures scripted rectangular screenshots. MIT licensed.
</p>
<!-- markdownlint-enable MD033 -->

## Usage

This is intended as a barebones CLI tool that can be called by AI to create a partial screenshot either to clipboard or file. Obviously this is not a replacement for fully featured UI tools like the awesome [Greenshot](https://github.com/greenshot/greenshot) (also open source).

```text
ScreenShotNet --region <x,y,width,height> [--delay <seconds>] [--window-title <titlePrefix>] [--clipboard] [--file <path>] [--format <png|jpg|bmp|gif|tiff>] [--watermark-text <text> --watermark-pos <x,y> --watermark-size <size> --watermark-color <color>]
```

Placeholder notes:

- '<...>' means replace this with your value. Do not type '<' or '>'.
- '[...]' means optional.

## Skill

The repo includes a reusable skill file here:

- [skills/ScreenShotNet/SKILL.md](skills/ScreenShotNet/SKILL.md)

In plain terms, this skill is a ready-made instruction sheet for AI assistants. It tells the assistant when to use ScreenShotNet, which command shape is valid, and what to report back.

Usual installation process (example for Cursor):

1. Copy the skills/ScreenShotNet folder into your Cursor skills directory (for example: %USERPROFILE%\\.cursor\skills\ScreenShotNet).
2. Reload/restart Cursor so the new skill is picked up.
3. Ask the assistant to use the ScreenShotNet skill when doing screenshot tasks.

This requires that the tool has been built already and the skill might need editing for correct paths!

## MCP server

The repo now also includes a stdio MCP server project here:

- `src/ScreenShotNet.Mcp`

This is the better fit when an assistant should receive the screenshot image directly as tool output instead of:

1. calling the CLI,
2. saving a file somewhere, and
3. opening that file in a separate step.

The MCP tool returns image content directly and can still optionally save the capture to disk or copy it to the clipboard.

### Run the MCP server

Requirements:

- Windows desktop session
- .NET 8 SDK/runtime because `src/ScreenShotNet.Mcp` targets `net8.0-windows`

```powershell
dotnet run --project .\src\ScreenShotNet.Mcp\ScreenShotNet.Mcp.csproj
```

### Tools

- `capture_screenshot`
  Capture a rectangular region using explicit coordinates.
  Parameters:
  `x`, `y`, `width`, `height` required; `delaySeconds`, `windowTitle`, `captureOffsetMode`, `withCursor`, `format`, `savePath`, `copyToClipboard`, `watermarkText`, `watermarkX`, `watermarkY`, `watermarkSize`, `watermarkColor` optional.
  If `windowTitle` is set, the first visible top-level window whose title starts with that value is restored and brought to the foreground before capture.
  `captureOffsetMode=relative` means `x` and `y` are measured from the matched window's top-left corner and requires `windowTitle`.
  If `withCursor` is `true`, the mouse cursor position at capture time is marked as a red reticle on the image.

- `capture_window_screenshot`
  Capture the full bounds of the first visible top-level window whose title starts with `windowTitle`.
  Parameters:
  `windowTitle` required; `delaySeconds`, `withCursor`, `format`, `savePath`, `copyToClipboard`, `watermarkText`, `watermarkX`, `watermarkY`, `watermarkSize`, `watermarkColor` optional.
  If `withCursor` is `true`, the mouse cursor position at capture time is marked as a red reticle on the image.

- `capture_center_screenshot`
  Capture a centered crop inside the matched window.
  Parameters:
  `windowTitle`, `width`, `height` required; `delaySeconds`, `withCursor`, `format`, `savePath`, `copyToClipboard`, `watermarkText`, `watermarkX`, `watermarkY`, `watermarkSize`, `watermarkColor` optional.
  The capture rectangle is centered within the matched window and must fit completely inside its resolved bounds.
  If `withCursor` is `true`, the mouse cursor position at capture time is marked as a red reticle on the image.

Example MCP call for a full window capture with cursor marker:

```json
{
  "tool": "capture_window_screenshot",
  "arguments": {
    "windowTitle": "Dual Universe",
    "withCursor": true,
    "format": "png"
  }
}
```

Example MCP call for a centered crop:

```json
{
  "tool": "capture_center_screenshot",
  "arguments": {
    "windowTitle": "Visual Studio",
    "width": 800,
    "height": 600,
    "withCursor": true,
    "format": "png"
  }
}
```

Notes:

- The server is Windows-only because it captures the live desktop.
- It should run in an interactive user session where the desktop is available.
- `withCursor` is currently an MCP parameter and not a CLI switch.
- `withCursor` is DPI-aware and tuned for multi-monitor desktop coordinates.
- If `withCursor=true` and the cursor is outside the captured region, no reticle is drawn.
- `capture_center_screenshot` computes the top-left corner automatically so the requested rectangle is centered within the matched window.
- The existing CLI remains useful for direct scripting, while the MCP server is the better path for assistants that can consume image tool results.

## Shell quoting rules (PowerShell and CMD)

- No quotes needed for simple values, for example: --region 0,0,400,300 --delay 1.5 --format jpg
- If a value contains spaces, wrap it in double quotes.
- In cmd.exe, single quotes do not quote values. Use double quotes only.
- In PowerShell, both quote styles work, but double quotes are used in this README for consistency.

Examples:

- PowerShell: ScreenShotNet --file "C:\temp\my capture.jpg" --watermark-text "Draft Build" --region 0,0,400,300
- cmd.exe:    ScreenShotNet --file "C:\temp\my capture.jpg" --watermark-text "Draft Build" --region 0,0,400,300

## Options

- -r, --region: required capture rectangle (x,y,width,height)
- -d, --delay: optional delay in seconds (default 0)
- --window-title: optional window title prefix; first visible top-level window with a title that starts with this value is brought to the foreground before capture
- -c, --clipboard: output screenshot to clipboard (can be combined with --file)
- -f, --file: output screenshot to file (adds .png if extension is missing; format inferred from extension if present)
- --format: explicit file format override (png, jpg, bmp, gif, tiff; requires --file)
- --watermark-text: optional watermark text to draw
- --watermark-pos: watermark text position in capture-local pixels (x,y)
- --watermark-size: watermark font size in points (default 24)
- --watermark-color: watermark color (#RRGGBB, #AARRGGBB, or known color name)
- -h, --help: show help and examples

## Examples

- ScreenShotNet --region 0,0,400,300 --clipboard
- ScreenShotNet --region 100,100,640,480 --delay 1.5 --file .\out\capture.png
- ScreenShotNet --region 100,100,640,480 --window-title "Visual Studio" --file .\out\capture.png
- ScreenShotNet --region 100,100,640,480 --file .\out\capture --format jpg
- ScreenShotNet --region 100,100,640,480 --file "D:\temp\my capture.jpg" --format jpg
- ScreenShotNet --region 100,100,640,480 --clipboard --file .\out\capture.png
- ScreenShotNet --region 50,50,500,300 --watermark-text "Draft" --watermark-pos 12,24 --watermark-size 18 --watermark-color "#80FF0000" --file .\out\capture-watermark.png

## Exit codes

- 0: success
- 2: invalid arguments or validation failure
- 3: runtime capture/output failure

## Building

- Default CLI build target is net48 for maximum compatibility.
- Enable modern targets (net9.0-windows, net10.0-windows, net11.0-windows) with:
  - dotnet build -p:EnableModernTfms=true
  - pwsh .\scripts\build_desktop.ps1 -EnableModernTfms

Current project layout:

- `src/ScreenShotNet.Core`: shared capture, watermark, clipboard, file, and format logic
- `src/ScreenShotNet.csproj`: existing CLI wrapper
- `src/ScreenShotNet.Mcp`: stdio MCP server
