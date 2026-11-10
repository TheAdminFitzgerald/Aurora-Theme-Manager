using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace AuroraThemeManager
{
    internal enum OOBSymbol { Infantry, Mechanised, Armour, Reconnaissance, Artillery, AirDefence, Orbital, Engineer, Specialist, Support, Supply, Generic }

    internal sealed class OOBSymbols : IDisposable
    {
        internal const string IconColour = "OrgChartIconColour";

        private static readonly Dictionary<OOBSymbol, string> svgs = new Dictionary<OOBSymbol, string>();
        private readonly Dictionary<string, Bitmap> bitmaps = new Dictionary<string, Bitmap>();

        internal Bitmap Get(OOBSymbol symbol, Color fore, Color back, Size size)
        {
            if (size.Width <= 0 || size.Height <= 0)
                return null;

            string key = symbol + "|" + fore.ToArgb() + "|" + back.ToArgb() + "|" + size.Width + "x" + size.Height;
            Bitmap bmp;
            if (bitmaps.TryGetValue(key, out bmp))
                return bmp;

            string svg = Svg(symbol);
            if (svg != null)
            {
                svg = svg.Replace("#FG", Html(fore)).Replace("#BG", Html(back));
                bmp = IconCatalog.Render(svg, size.Width, size.Height);
            }
            bitmaps[key] = bmp;
            return bmp;
        }

        private static string Svg(OOBSymbol symbol)
        {
            string svg;
            lock (svgs)
            {
                if (svgs.TryGetValue(symbol, out svg))
                    return svg;

                string name = "AuroraThemeManager.Resources.OOB." + symbol + ".svg";
                using (Stream s = typeof(OOBSymbols).Assembly.GetManifestResourceStream(name))
                {
                    if (s == null)
                        Log.Info("OOBSymbols: " + name + " is not embedded");
                    else
                        using (var reader = new StreamReader(s, Encoding.UTF8))
                            svg = reader.ReadToEnd();
                }
                svgs[symbol] = svg;
                return svg;
            }
        }

        private static string Html(Color c)
        {
            return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }

        internal void Clear()
        {
            foreach (Bitmap bmp in bitmaps.Values)
                if (bmp != null)
                    bmp.Dispose();
            bitmaps.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
