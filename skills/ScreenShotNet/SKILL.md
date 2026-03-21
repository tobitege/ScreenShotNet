---
name: ScreenShotNet-screenshot
description: Capture region screenshots with ScreenShotNet, including delay, optional foreground window activation, output target, and optional watermark overlay
---

# ScreenShotNet Skill

Use this skill when a user wants to take screenshots via the `ScreenShotNet` project.

## When to use

- User asks to capture a specific region.
- User asks for delayed capture.
- User asks to output to clipboard or file.
- User asks for watermark text with position, size, and color.

## Preconditions

- Windows environment.
- `ScreenShotNet` is built.
- Binary path exists:
  `<project-root>\src\bin\Debug\net48\ScreenShotNet.exe`
  (or Release equivalent).

## CLI contract

- Required: `--region x,y,width,height`
- Optional: `--delay seconds`
- Optional: `--window-title "titlePrefix"`
- At least one target: `--clipboard` and/or `--file fullPath`
- Optional watermark:
  - `--watermark-text "text"`
  - `--watermark-pos x,y`
  - `--watermark-size number`
  - `--watermark-color #RRGGBB | #AARRGGBB | NamedColor`

## Command templates

- Clipboard:
  `& "<project-root>\bin\Any CPU\Debug\net480\ScreenShotNet.exe" --region 0,0,400,300 --clipboard`

- File:
  `& "<project-root>\bin\Any CPU\Debug\net480\ScreenShotNet.exe" --region 100,100,640,480 --delay 1.5 --file "D:\temp\capture.png"`

- File after bringing a window to the foreground:
  `& "<project-root>\bin\Any CPU\Debug\net480\ScreenShotNet.exe" --region 100,100,640,480 --window-title "Visual Studio" --file "D:\temp\capture.png"`

- File + watermark:
  `& "<project-root>\bin\Any CPU\Debug\net480\ScreenShotNet.exe" --region 50,50,500,300 --watermark-text "Draft" --watermark-pos 12,24 --watermark-size 18 --watermark-color "#80FF0000" --file "D:\temp\capture-watermark.png"`

## Validation rules

- Reject invalid region format or non-positive width/height.
- Reject missing target.
- Use `--window-title` as a case-insensitive title prefix match, not a wildcard or arbitrary substring match.
- Reject watermark options without `--watermark-text`.
- Surface CLI stderr and exit code to user.

## Response style

- Report exact command run.
- Report exit code.
- For file output, report absolute saved path.
- For clipboard output, confirm completion.
- If both targets are used, report both outcomes.
