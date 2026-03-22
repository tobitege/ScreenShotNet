<!-- markdownlint-disable MD022 MD024 -->
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-03-21

### Added

- New stdio MCP server with direct image tool output for assistant-driven screenshot capture.
- Shared `ScreenShotNet.Core` library for capture, watermark, clipboard, file output, and argument handling used by both the CLI and MCP server.
- Window-title based activation and full window capture support in the shared capture pipeline.
- `capture_center_screenshot` MCP tool for capturing a centered region inside a matched window by specifying `windowTitle`, `width`, and `height`.
- `withCursor` MCP option for marking the mouse cursor location at capture time as a reticle overlay.

### Changed

- Updated README and skill documentation for the MCP workflow and the expanded screenshot toolset.
- Improved cursor alignment for `withCursor` on DPI-scaled and multi-monitor Windows setups.
- Expanded tests and solution/project structure to support the new shared core and MCP server layout.
- `SKILL.md` shortened.

## [0.1.0] - 2026-02-14

Initial release.
