---
name: ScreenShotNet-screenshot
description: Capture Windows screenshots with ScreenShotNet via MCP when available, otherwise via CLI
---

# ScreenShotNet Skill

Use this skill when a user wants to take screenshots via CLI or MCP with `ScreenShotNet` on Windows.

Repo: [github.com/tobitege/ScreenShotNet](https://github.com/tobitege/ScreenShotNet)

## Prefer This Order

- Prefer MCP when the environment can consume image tool output directly.
- Fall back to the CLI when MCP is unavailable.

## Preconditions

- Windows interactive desktop session.
- MCP server start command: `dotnet run --project .\src\ScreenShotNet.Mcp\ScreenShotNet.Mcp.csproj`
- CLI fallback binary: `<project-root>\src\bin\Debug\net48\ScreenShotNet.exe`

## MCP Usage

- `capture_screenshot` for explicit rectangles.
- `capture_window_screenshot` for a full matched window.
- `capture_center_screenshot` for a centered crop inside the matched window.

## Important Rules

- `windowTitle` is a case-insensitive prefix match on visible top-level windows.
- For `capture_screenshot`, `captureOffsetMode=relative` requires `windowTitle` and treats `x` and `y` as offsets from the matched window's top-left corner.
- For `capture_center_screenshot`, `width` and `height` must fully fit inside the matched window bounds.
- Watermark position, size, or color require watermark text.
- For file output, report the absolute saved path.
- For clipboard output, confirm completion.

## CLI Fallback

- Command shape: `& "<project-root>\src\bin\Debug\net48\ScreenShotNet.exe" --region x,y,width,height [--delay seconds] [--window-title "titlePrefix"] [--clipboard] [--file "path"] [--format png|jpg|bmp|gif|tiff]`
- `--window-title` is also a case-insensitive prefix match.
- Surface CLI stderr and exit code to the user.

## Response Style

- For MCP usage, report the tool used and summarize the captured target.
- For `capture_center_screenshot`, mention the matched window and requested centered size.
- For CLI usage, report the exact command and exit code.
