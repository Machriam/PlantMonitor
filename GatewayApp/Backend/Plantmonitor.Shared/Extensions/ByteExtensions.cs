using System.Drawing;

namespace Plantmonitor.Shared.Extensions;

public static class ByteExtensions
{
    public static float[] Rgb2Hsl(this byte[] rgb)
    {
        var color = rgb.Length < 3 ? Color.FromArgb(0, 0, 0) : Color.FromArgb(rgb[0], rgb[1], rgb[2]);
        return [color.GetHue(), color.GetSaturation(), color.GetBrightness()];
    }
}
