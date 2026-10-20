using System;

namespace Wingmate.Core
{
    /// <summary>Hex and HSV conversions for the colour picker. Hue is 0..360, saturation and value 0..1.</summary>
    public static class ColorMath
    {
        public readonly record struct Rgb(byte R, byte G, byte B);

        public static string Hex(Rgb c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        public static Rgb? ParseHex(string? text) => WarnColors.TryParse(text) is { } t ? new Rgb(t.R, t.G, t.B) : null;

        private static byte B(double v) => (byte)Math.Clamp(Math.Round(v * 255), 0, 255);

        public static (double H, double S, double V) ToHsv(Rgb c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0, max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            return (Hue(r, g, b, max, d), max == 0 ? 0 : d / max, max);
        }

        public static Rgb FromHsv(double h, double s, double v)
        {
            h = Wrap(h); s = Math.Clamp(s, 0, 1); v = Math.Clamp(v, 0, 1);
            double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
            var (r, g, b) = Sector(h, c, x);
            return new Rgb(B(r + m), B(g + m), B(b + m));
        }

        private static double Hue(double r, double g, double b, double max, double d)
        {
            if (d == 0) return 0;
            double h = max == r ? 60 * (((g - b) / d) % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
            return h < 0 ? h + 360 : h;
        }

        private static (double, double, double) Sector(double h, double c, double x) =>
            h < 60 ? (c, x, 0) : h < 120 ? (x, c, 0) : h < 180 ? (0, c, x) : h < 240 ? (0, x, c) : h < 300 ? (x, 0, c) : (c, 0, x);

        private static double Wrap(double h) { h %= 360; return h < 0 ? h + 360 : h; }

    }
}
