using System.Drawing;

namespace ScreenShotNet
{
    public sealed class WatermarkOptions
    {
        public string Text { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public float Size { get; set; }

        public Color Color { get; set; }
    }
}
