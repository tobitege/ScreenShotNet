using System.Drawing;

namespace ScreenShotNet
{
    public sealed class CliOptions
    {
        public Rectangle Region { get; set; }

        public double DelaySeconds { get; set; }

        public string WindowTitle { get; set; }

        public bool CopyToClipboard { get; set; }

        public bool SaveToFile { get; set; }

        public string FilePath { get; set; }

        public string OutputFormat { get; set; }

        public WatermarkOptions Watermark { get; set; }
    }
}
