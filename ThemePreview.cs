using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace AuroraThemeManager
{
    public enum PreviewMode
    {
        GameWindows,
        TacticalMap,
        GalacticMap
    }

    public sealed partial class ThemePreview : Control
    {
        private AuroraConfig config;
        private PreviewMode mode;

        internal PreviewMode Mode
        {
            get { return mode; }
            set
            {
                if (mode == value)
                    return;
                mode = value;
                hoverKey = null;
                tip.Hide(this);
                Invalidate();
            }
        }

        public ThemePreview()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            tip = new ToolTip { UseAnimation = false, UseFading = false };
        }

        internal AuroraConfig Config
        {
            get { return config; }
            set { config = value; Invalidate(); }
        }

        public event Action<string> ColorClicked;

        #region Hit areas

        private sealed class HitArea
        {
            public string Color;
            public string Title;
            public string Description;
            public Func<PointF, bool> Contains;

            public string Key { get { return Color + "|" + Title; } }
        }

        private readonly List<HitArea> hits = new List<HitArea>();
        private readonly ToolTip tip;
        private string hoverKey;

        private void Hit(string color, string title, string description, Func<PointF, bool> contains)
        {
            hits.Add(new HitArea { Color = color, Title = title, Description = description, Contains = contains });
        }

        private void HitRect(string color, string title, string description, RectangleF r)
        {
            r.Inflate(2, 2);
            Hit(color, title, description, p => r.Contains(p));
        }

        private void HitDisc(string color, string title, string description, float cx, float cy, float r)
        {
            float reach = Math.Max(r + 3, 6);
            Hit(color, title, description, p => Distance(p.X, p.Y, cx, cy) <= reach);
        }

        private void HitRing(string color, string title, string description, float cx, float cy, float r)
        {
            Hit(color, title, description, p => Math.Abs(Distance(p.X, p.Y, cx, cy) - r) <= 3);
        }

        private void HitLine(string color, string title, string description, float x1, float y1, float x2, float y2)
        {
            Hit(color, title, description, p => SegmentDistance(p, x1, y1, x2, y2) <= 4);
        }

        private static float Distance(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2, dy = y1 - y2;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static float SegmentDistance(PointF p, float x1, float y1, float x2, float y2)
        {
            float dx = x2 - x1, dy = y2 - y1;
            float length = dx * dx + dy * dy;
            float t = length == 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X - x1) * dx + (p.Y - y1) * dy) / length));
            return Distance(p.X, p.Y, x1 + t * dx, y1 + t * dy);
        }

        private HitArea HitAt(Point location)
        {
            for (int i = hits.Count - 1; i >= 0; i--)
                if (hits[i].Contains(location))
                    return hits[i];
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            HitArea hit = HitAt(e.Location);
            string key = hit == null ? null : hit.Key;
            if (key == hoverKey)
                return;

            hoverKey = key;
            if (hit == null)
            {
                tip.Hide(this);
                Cursor = Cursors.Default;
                return;
            }

            Cursor = Cursors.Hand;
            string description = (hit.Description ?? "").TrimEnd('.');
            string text = (description.Length > 0 ? description + "\n\n" : "") + hit.Color + "  (click to change)";
            tip.Show(text, this, e.X + 14, e.Y + 18, 20000);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverKey = null;
            tip.Hide(this);
            Cursor = Cursors.Default;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left)
                return;
            HitArea hit = HitAt(e.Location);
            if (hit == null)
                return;

            hoverKey = null;
            tip.Hide(this);
            Action<string> handler = ColorClicked;
            if (handler != null)
                handler(hit.Color);
        }

        #endregion

        private Color ColorOf(string name, Color fallback)
        {
            AuroraConfig.ColorEntry entry = config.Find(name);
            if (entry == null)
                return fallback;
            return entry.AlphaAllowed ? entry.Argb : entry.Value;
        }

        private Font SampleFont(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(Control.DefaultFont.FontFamily, size, style);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            hits.Clear();
            ReleaseMapResources();
            if (config == null)
            {
                g.Clear(Color.Black);
                TextRenderer.DrawText(g, "Theme preview", Font, ClientRectangle, Color.Gray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            Color back = ColorOf("Background", Color.Black);
            g.Clear(back);
            if (mode != PreviewMode.GameWindows)
                HitRect("Background", "Map background", "The background of windows, lists and maps.", ClientRectangle);

            using (Font small = SampleFont(8.25f))
            using (Font bold = SampleFont(8.25f, FontStyle.Bold))
            {
                switch (mode)
                {
                    case PreviewMode.TacticalMap:
                        DrawTacticalSample(g, ClientRectangle);
                        break;
                    case PreviewMode.GalacticMap:
                        DrawGalacticSample(g, ClientRectangle);
                        break;
                    default:
                        DrawGameWindows(g, small, bold, ClientRectangle);
                        break;
                }
            }
        }

        private void Heading(Graphics g, Font font, string text, Rectangle area, Color color)
        {
            g.DrawString(text, font, new SolidBrush(color), area.X + 6, area.Y + 3);
        }

        private void Line(Graphics g, Color c, float width, float x1, float y1, float x2, float y2)
        {
            using (var pen = new Pen(c, width))
                g.DrawLine(pen, x1, y1, x2, y2);
        }

        private void Disc(Graphics g, Color c, float cx, float cy, float r)
        {
            using (var brush = new SolidBrush(c))
                g.FillEllipse(brush, cx - r, cy - r, 2 * r, 2 * r);
        }

        private void Ring(Graphics g, Color c, float cx, float cy, float r, float width = 1f, DashStyle dash = DashStyle.Solid)
        {
            using (var pen = new Pen(c, width) { DashStyle = dash })
                g.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
        }

        private RectangleF DrawLabel(Graphics g, string text, Font font, Color c, float x, float y)
        {
            using (var brush = new SolidBrush(c))
                g.DrawString(text, font, brush, x, y);
            SizeF size = g.MeasureString(text, font);
            return new RectangleF(x, y, size.Width, size.Height);
        }

        private RectangleF ColorText(Graphics g, string text, Font font, string color, Color fallback, float x, float y, string title, string description)
        {
            RectangleF r = DrawLabel(g, text, font, ColorOf(color, fallback), x, y);
            HitRect(color, title, description, r);
            return r;
        }

        #region Buttons

        private Button sampleButton;

        private static FlatStyle ParseFlatStyle(AuroraConfig.ButtonStyleEntry style)
        {
            FlatStyle result;
            if (style != null && Enum.TryParse(style.FlatStyle, true, out result))
                return result;
            return FlatStyle.Standard;
        }

        private void DrawButton(Graphics g, Font font, Rectangle r, Color face, Color text, string caption, Image image, AuroraConfig.ButtonStyleEntry style)
        {
            try
            {
                if (sampleButton == null)
                    sampleButton = new Button { UseVisualStyleBackColor = false, TabStop = false };

                Button b = sampleButton;
                b.Size = r.Size;
                b.FlatStyle = ParseFlatStyle(style);
                b.FlatAppearance.BorderSize = style == null ? 1 : Math.Max(0, style.BorderSize);
                b.FlatAppearance.BorderColor = ColorOf("Background", Color.Black);
                b.BackColor = face;
                b.ForeColor = text;
                b.Font = font;
                b.Text = caption;
                b.Image = image;
                b.ImageAlign = ContentAlignment.MiddleCenter;

                using (var bmp = new Bitmap(r.Width, r.Height))
                {
                    b.DrawToBitmap(bmp, new Rectangle(Point.Empty, r.Size));
                    g.DrawImageUnscaled(bmp, r.Location);
                }
                b.Image = null;
                return;
            }
            catch
            {
            }

            using (var brush = new SolidBrush(face))
                g.FillRectangle(brush, r);

            int border = style == null ? 0 : style.BorderSize;
            if (border > 0)
            {
                using (var pen = new Pen(ColorOf("Background", Color.Black), border))
                    g.DrawRectangle(pen, r.X + border / 2f, r.Y + border / 2f, r.Width - border, r.Height - border);
            }
            if (image != null)
                g.DrawImage(image, r.X + (r.Width - image.Width) / 2, r.Y + (r.Height - image.Height) / 2, image.Width, image.Height);

            TextRenderer.DrawText(g, caption, font, r, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        #endregion

        #region Toolbar icons

        private static readonly string[] ToolbarIconNames = { "Fleet", "ColonySummary", "Research", "Class", "Commanders", "Galactic" };

        private readonly string[] iconKeys = new string[ToolbarIconNames.Length];
        private readonly Bitmap[] iconBitmaps = new Bitmap[ToolbarIconNames.Length];

        /// <summary>
        /// One SVG per toolbar slot, never the same file twice.  A slot whose own icon is missing gets
        /// the first SVG in the folder that no other slot uses.
        /// </summary>
        private static string[] ToolbarIconPaths()
        {
            var paths = new string[ToolbarIconNames.Length];
            try
            {
                string dir = IconCatalog.IconsDirectory();
                if (!Directory.Exists(dir))
                    return paths;
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < paths.Length; i++)
                {
                    string path = Path.Combine(dir, ToolbarIconNames[i] + ".svg");
                    if (File.Exists(path) && used.Add(path))
                        paths[i] = path;
                }
                var spare = new Queue<string>(Directory.GetFiles(dir, "*.svg")
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .Where(f => !used.Contains(f)));
                for (int i = 0; i < paths.Length; i++)
                    if (paths[i] == null && spare.Count > 0)
                        paths[i] = spare.Dequeue();
            }
            catch
            {
            }
            return paths;
        }

        private Bitmap ToolbarIcon(string path, int slot, Color color, int size)
        {
            if (path == null)
                return null;

            string hex = string.Format(CultureInfo.InvariantCulture, "#{0:x2}{1:x2}{2:x2}", color.R, color.G, color.B);
            string key;
            try { key = path + "|" + File.GetLastWriteTimeUtc(path).Ticks + "|" + hex + "|" + size; }
            catch { return null; }

            if (iconKeys[slot] == key)
                return iconBitmaps[slot];

            Bitmap bmp = null;
            try
            {
                string svg = IconCatalog.Normalize(File.ReadAllText(path, Encoding.UTF8));
                svg = Regex.Replace(svg, "fill=\"#ffffff\"", "fill=\"" + hex + "\"", RegexOptions.IgnoreCase);
                svg = Regex.Replace(svg, "fill:\\s*#(ffffff|fff)\\b", "fill:" + hex, RegexOptions.IgnoreCase);
                bmp = IconCatalog.Render(svg, size);
            }
            catch { }

            if (iconBitmaps[slot] != null)
                iconBitmaps[slot].Dispose();
            iconBitmaps[slot] = bmp;
            iconKeys[slot] = key;
            return bmp;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (sampleButton != null)
                    sampleButton.Dispose();
                tip.Dispose();
                oobSymbols.Dispose();
                ReleaseMapResources();
                for (int i = 0; i < iconBitmaps.Length; i++)
                    if (iconBitmaps[i] != null)
                        iconBitmaps[i].Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
