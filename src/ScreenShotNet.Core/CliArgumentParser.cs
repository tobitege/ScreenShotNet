using System;
using System.Drawing;
using System.Globalization;

namespace ScreenShotNet
{
    public static class CliArgumentParser
    {
        public static bool TryParseArguments(string[] args, out CliOptions options, out string errorMessage, out bool showHelp)
        {
            options = new CliOptions();
            errorMessage = null;
            showHelp = false;

            if (args == null || args.Length == 0)
            {
                errorMessage = "No arguments supplied.";
                return false;
            }

            bool regionProvided = false;
            bool fileProvided = false;
            bool clipboardProvided = false;
            bool formatProvided = false;
            bool watermarkTextProvided = false;
            bool watermarkPositionProvided = false;
            bool watermarkSizeProvided = false;
            bool watermarkColorProvided = false;

            string watermarkText = null;
            int watermarkX = 0;
            int watermarkY = 0;
            float watermarkSize = 24f;
            Color watermarkColor = Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF);

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                string normalizedArg = arg.ToLowerInvariant();

                switch (normalizedArg)
                {
                    case "--help":
                    case "-h":
                    case "/?":
                        showHelp = true;
                        return true;
                    case "--region":
                    case "-r":
                        if (!TryReadNextValue(args, ref i, out var regionValue))
                        {
                            errorMessage = $"Missing value for {arg}.";
                            return false;
                        }

                        if (!TryParseRegion(regionValue, out var region))
                        {
                            errorMessage = "Invalid region. Expected format: x,y,width,height with width/height > 0.";
                            return false;
                        }

                        options.Region = region;
                        regionProvided = true;
                        break;
                    case "--delay":
                    case "-d":
                        if (!TryReadNextValue(args, ref i, out var delayValue))
                        {
                            errorMessage = $"Missing value for {arg}.";
                            return false;
                        }

                        if (!double.TryParse(delayValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var delaySeconds) || delaySeconds < 0)
                        {
                            errorMessage = "Invalid delay. Provide a non-negative number in seconds.";
                            return false;
                        }

                        options.DelaySeconds = delaySeconds;
                        break;
                    case "--window-title":
                        if (!TryReadNextValue(args, ref i, out var windowTitleValue))
                        {
                            errorMessage = "Missing value for --window-title.";
                            return false;
                        }

                        if (string.IsNullOrWhiteSpace(windowTitleValue))
                        {
                            errorMessage = "--window-title cannot be empty.";
                            return false;
                        }

                        options.WindowTitle = windowTitleValue;
                        break;
                    case "--clipboard":
                    case "-c":
                        clipboardProvided = true;
                        options.CopyToClipboard = true;
                        break;
                    case "--file":
                    case "-f":
                        if (!TryReadNextValue(args, ref i, out var fileValue))
                        {
                            errorMessage = $"Missing value for {arg}.";
                            return false;
                        }

                        if (string.IsNullOrWhiteSpace(fileValue))
                        {
                            errorMessage = "File path cannot be empty.";
                            return false;
                        }

                        fileProvided = true;
                        options.SaveToFile = true;
                        options.FilePath = fileValue;
                        break;
                    case "--format":
                        if (!TryReadNextValue(args, ref i, out var formatValue))
                        {
                            errorMessage = "Missing value for --format.";
                            return false;
                        }

                        if (!ScreenshotOperations.TryNormalizeOutputFormat(formatValue, out var normalizedFormat))
                        {
                            errorMessage = "Invalid output format. Supported formats: png, jpg, bmp, gif, tiff.";
                            return false;
                        }

                        formatProvided = true;
                        options.OutputFormat = normalizedFormat;
                        break;
                    case "--watermark-text":
                        if (!TryReadNextValue(args, ref i, out var watermarkTextValue))
                        {
                            errorMessage = "Missing value for --watermark-text.";
                            return false;
                        }

                        if (string.IsNullOrEmpty(watermarkTextValue))
                        {
                            errorMessage = "--watermark-text cannot be empty.";
                            return false;
                        }

                        watermarkTextProvided = true;
                        watermarkText = watermarkTextValue;
                        break;
                    case "--watermark-pos":
                        if (!TryReadNextValue(args, ref i, out var watermarkPosValue))
                        {
                            errorMessage = "Missing value for --watermark-pos.";
                            return false;
                        }

                        if (!TryParsePosition(watermarkPosValue, out watermarkX, out watermarkY))
                        {
                            errorMessage = "Invalid watermark position. Expected format: x,y.";
                            return false;
                        }

                        watermarkPositionProvided = true;
                        break;
                    case "--watermark-size":
                        if (!TryReadNextValue(args, ref i, out var watermarkSizeValue))
                        {
                            errorMessage = "Missing value for --watermark-size.";
                            return false;
                        }

                        if (!float.TryParse(watermarkSizeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out watermarkSize) || watermarkSize <= 0)
                        {
                            errorMessage = "Invalid watermark size. Provide a number greater than 0.";
                            return false;
                        }

                        watermarkSizeProvided = true;
                        break;
                    case "--watermark-color":
                        if (!TryReadNextValue(args, ref i, out var watermarkColorValue))
                        {
                            errorMessage = "Missing value for --watermark-color.";
                            return false;
                        }

                        if (!TryParseColor(watermarkColorValue, out watermarkColor))
                        {
                            errorMessage = "Invalid watermark color. Use #RRGGBB, #AARRGGBB, or a known color name.";
                            return false;
                        }

                        watermarkColorProvided = true;
                        break;
                    default:
                        errorMessage = $"Unknown argument: {arg}";
                        return false;
                }
            }

            if (!regionProvided)
            {
                errorMessage = "Missing required --region option.";
                return false;
            }

            if (!fileProvided && !clipboardProvided)
            {
                errorMessage = "Specify at least one output target: --clipboard and/or --file <path>.";
                return false;
            }

            if (formatProvided && !fileProvided)
            {
                errorMessage = "--format requires --file <path>.";
                return false;
            }

            if (!watermarkTextProvided && (watermarkPositionProvided || watermarkSizeProvided || watermarkColorProvided))
            {
                errorMessage = "Watermark position/size/color requires --watermark-text.";
                return false;
            }

            if (watermarkTextProvided && !watermarkPositionProvided)
            {
                errorMessage = "Missing --watermark-pos for watermark text.";
                return false;
            }

            if (watermarkTextProvided)
            {
                options.Watermark = new WatermarkOptions
                {
                    Text = watermarkText,
                    X = watermarkX,
                    Y = watermarkY,
                    Size = watermarkSize,
                    Color = watermarkColor
                };
            }

            return true;
        }

        public static bool TryParseRegion(string value, out Rectangle region)
        {
            region = Rectangle.Empty;
            var parts = value.Split(',');
            if (parts.Length != 4)
            {
                return false;
            }

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ||
                !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
            {
                return false;
            }

            if (width <= 0 || height <= 0)
            {
                return false;
            }

            region = new Rectangle(x, y, width, height);
            return true;
        }

        public static bool TryParsePosition(string value, out int x, out int y)
        {
            x = 0;
            y = 0;
            var parts = value.Split(',');
            if (parts.Length != 2)
            {
                return false;
            }

            return int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) &&
                   int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y);
        }

        public static bool TryParseColor(string value, out Color color)
        {
            color = Color.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                if (trimmed.Length == 7)
                {
                    if (!TryParseHexByte(trimmed.Substring(1, 2), out var r) ||
                        !TryParseHexByte(trimmed.Substring(3, 2), out var g) ||
                        !TryParseHexByte(trimmed.Substring(5, 2), out var b))
                    {
                        return false;
                    }

                    color = Color.FromArgb(255, r, g, b);
                    return true;
                }

                if (trimmed.Length == 9)
                {
                    if (!TryParseHexByte(trimmed.Substring(1, 2), out var a) ||
                        !TryParseHexByte(trimmed.Substring(3, 2), out var r) ||
                        !TryParseHexByte(trimmed.Substring(5, 2), out var g) ||
                        !TryParseHexByte(trimmed.Substring(7, 2), out var b))
                    {
                        return false;
                    }

                    color = Color.FromArgb(a, r, g, b);
                    return true;
                }

                return false;
            }

            if (!Enum.TryParse(trimmed, true, out KnownColor knownColor))
            {
                return false;
            }

            color = Color.FromKnownColor(knownColor);
            return true;
        }

        private static bool TryReadNextValue(string[] args, ref int currentIndex, out string value)
        {
            int nextIndex = currentIndex + 1;
            if (nextIndex >= args.Length)
            {
                value = null;
                return false;
            }

            string nextValue = args[nextIndex];
            if (IsOptionToken(nextValue))
            {
                value = null;
                return false;
            }

            value = nextValue;
            currentIndex = nextIndex;
            return true;
        }

        private static bool IsOptionToken(string value)
        {
            switch (value)
            {
                case "--help":
                case "-h":
                case "/?":
                case "--region":
                case "-r":
                case "--delay":
                case "-d":
                case "--window-title":
                case "--clipboard":
                case "-c":
                case "--file":
                case "-f":
                case "--format":
                case "--watermark-text":
                case "--watermark-pos":
                case "--watermark-size":
                case "--watermark-color":
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseHexByte(string value, out byte byteValue)
        {
            return byte.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byteValue);
        }
    }
}
