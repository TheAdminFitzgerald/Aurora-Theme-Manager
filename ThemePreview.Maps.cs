using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace AuroraThemeManager
{
    public sealed partial class ThemePreview
    {
        #region Map canvas

        private enum Stroke { Solid, Dashed }

        private enum Mark { Dot, Circle, Square }

        private enum Side { Below, Above, Right, Left }

        private readonly List<IDisposable> mapResources = new List<IDisposable>();

        private void ReleaseMapResources()
        {
            foreach (IDisposable resource in mapResources)
                resource.Dispose();
            mapResources.Clear();
        }

        /// <summary>
        /// A small illustrative drawing surface for the map previews.  Positions are given in percent of
        /// the pane and sizes in units of one percent of its shorter side, so a scene keeps its shape at
        /// any window size.  Every shape is also registered as a hit area from its own outline or fill,
        /// so hovering or clicking anything drawn finds the color it was drawn in.
        /// </summary>
        private sealed class MapCanvas
        {
            private const float MarkSize = 1.8f;
            private const float LabelGap = 0.8f;
            private const float HitReach = 6f;

            private readonly ThemePreview owner;
            private readonly Graphics g;
            private readonly Font font;
            private readonly RectangleF area;
            private readonly float unit;

            internal MapCanvas(ThemePreview owner, Graphics g, Font font, RectangleF area)
            {
                this.owner = owner;
                this.g = g;
                this.font = font;
                this.area = area;
                unit = Math.Max(4f, Math.Min(area.Width, area.Height) / 100f);
            }

            internal PointF At(float x, float y)
            {
                return new PointF(area.X + area.Width * x / 100f, area.Y + area.Height * y / 100f);
            }

            internal float U(float units)
            {
                return units * unit;
            }

            internal PointF Around(PointF centre, float radiusUnits, float degrees)
            {
                double a = degrees * Math.PI / 180;
                return new PointF(centre.X + U(radiusUnits) * (float)Math.Cos(a), centre.Y + U(radiusUnits) * (float)Math.Sin(a));
            }

            private Pen PenFor(Color color, float width, Stroke stroke)
            {
                var pen = new Pen(color, width);
                if (stroke == Stroke.Dashed)
                    pen.DashPattern = new[] { 4f, 3f };
                return pen;
            }

            private GraphicsPath Keep(GraphicsPath path)
            {
                owner.mapResources.Add(path);
                return path;
            }

            private void HitOutline(GraphicsPath path, string color, string title, string tip, float width)
            {
                var reach = new Pen(Color.Black, Math.Max(width, 1f) + HitReach);
                owner.mapResources.Add(reach);
                owner.Hit(color, title, tip, p => path.IsOutlineVisible(p, reach));
            }

            private void HitFill(GraphicsPath path, string color, string title, string tip)
            {
                var reach = new Pen(Color.Black, HitReach);
                owner.mapResources.Add(reach);
                owner.Hit(color, title, tip, p => path.IsVisible(p) || path.IsOutlineVisible(p, reach));
            }

            internal float MarkRadius(float scale = 1f)
            {
                return U(MarkSize) * scale / 2f;
            }

            internal void Marker(PointF p, Mark mark, string color, Color fallback, string title, string tip, float scale = 1f)
            {
                Color c = owner.ColorOf(color, fallback);
                float r = MarkRadius(scale);
                GraphicsPath path = Keep(new GraphicsPath());
                switch (mark)
                {
                    case Mark.Square:
                        path.AddRectangle(new RectangleF(p.X - r, p.Y - r, 2 * r, 2 * r));
                        break;
                    default:
                        path.AddEllipse(p.X - r, p.Y - r, 2 * r, 2 * r);
                        break;
                }

                if (mark == Mark.Circle)
                {
                    float width = 1.5f;
                    using (Pen pen = PenFor(c, width, Stroke.Solid))
                        g.DrawPath(pen, path);
                    HitOutline(path, color, title, tip, width);
                }
                else
                {
                    using (var brush = new SolidBrush(c))
                        g.FillPath(brush, path);
                    HitFill(path, color, title, tip);
                }
            }

            /// <summary>A bold letter X centred on the point, sized to stand a little taller than a marker.</summary>
            internal RectangleF Symbol(PointF p, string text, float scale, string color, Color fallback, string title, string tip)
            {
                float height = U(MarkSize) * scale;
                using (var bold = new Font(System.Drawing.SystemFonts.DefaultFont.FontFamily, height, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(owner.ColorOf(color, fallback)))
                using (var format = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    SizeF size = g.MeasureString(text, bold, PointF.Empty, format);
                    var rect = new RectangleF(p.X - size.Width / 2, p.Y - size.Height / 2, size.Width, size.Height);
                    g.DrawString(text, bold, brush, rect, format);
                    owner.HitRect(color, title, tip, rect);
                    return rect;
                }
            }

            internal void Outline(PointF p, Mark mark, float scale, string color, Color fallback, string title, string tip)
            {
                Color c = owner.ColorOf(color, fallback);
                float r = MarkRadius(scale);
                GraphicsPath path = Keep(new GraphicsPath());
                if (mark == Mark.Square)
                    path.AddRectangle(new RectangleF(p.X - r, p.Y - r, 2 * r, 2 * r));
                else
                    path.AddEllipse(p.X - r, p.Y - r, 2 * r, 2 * r);
                using (Pen pen = PenFor(c, 1.5f, Stroke.Solid))
                    g.DrawPath(pen, path);
                HitOutline(path, color, title, tip, 1.5f);
            }

            internal void Circle(PointF centre, float radius, Stroke stroke, float width, string color, Color fallback, string title, string tip)
            {
                Orbit(centre, radius, radius, 0, stroke, width, color, fallback, title, tip);
            }

            internal void Orbit(PointF centre, float rx, float ry, float degrees, Stroke stroke, float width, string color, Color fallback, string title, string tip)
            {
                GraphicsPath path = Keep(new GraphicsPath());
                path.AddEllipse(centre.X - rx, centre.Y - ry, 2 * rx, 2 * ry);
                if (degrees != 0)
                    using (var rotate = new Matrix())
                    {
                        rotate.RotateAt(degrees, centre);
                        path.Transform(rotate);
                    }
                using (Pen pen = PenFor(owner.ColorOf(color, fallback), width, stroke))
                    g.DrawPath(pen, path);
                HitOutline(path, color, title, tip, width);
            }

            internal void Segment(PointF a, PointF b, Stroke stroke, float width, string color, Color fallback, string title, string tip, int alpha = 255)
            {
                GraphicsPath path = Keep(new GraphicsPath());
                path.AddLine(a, b);
                Color c = owner.ColorOf(color, fallback);
                using (Pen pen = PenFor(Color.FromArgb(alpha * c.A / 255, c), width, stroke))
                    g.DrawPath(pen, path);
                HitOutline(path, color, title, tip, width);
            }

            internal void Frame(RectangleF r, Stroke stroke, float width, string color, Color fallback, string title, string tip)
            {
                GraphicsPath path = Keep(new GraphicsPath());
                path.AddRectangle(r);
                using (Pen pen = PenFor(owner.ColorOf(color, fallback), width, stroke))
                    g.DrawPath(pen, path);
                HitOutline(path, color, title, tip, width);
            }

            internal void Disc(PointF centre, float radius, float outline, string fill, Color fillFallback, string fillTitle, string fillTip,
                string edge, Color edgeFallback, string edgeTitle, string edgeTip)
            {
                GraphicsPath path = Keep(new GraphicsPath());
                path.AddEllipse(centre.X - radius, centre.Y - radius, 2 * radius, 2 * radius);
                using (var brush = new SolidBrush(owner.ColorOf(fill, fillFallback)))
                    g.FillPath(brush, path);
                owner.Hit(fill, fillTitle, fillTip, p => path.IsVisible(p));
                if (outline > 0 && edge != null)
                {
                    using (Pen pen = PenFor(owner.ColorOf(edge, edgeFallback), outline, Stroke.Solid))
                        g.DrawPath(pen, path);
                    HitOutline(path, edge, edgeTitle, edgeTip, outline);
                }
            }

            /// <summary>
            /// Writes text beside a point, clear of anything of the given radius drawn there, and returns
            /// where it went.
            /// </summary>
            internal RectangleF Label(string text, PointF anchor, Side side, float clearance, string color, Color fallback, string title, string tip)
            {
                SizeF size = g.MeasureString(text, font);
                float gap = U(LabelGap);
                PointF at;
                switch (side)
                {
                    case Side.Above:
                        at = new PointF(anchor.X - size.Width / 2, anchor.Y - clearance - gap - size.Height);
                        break;
                    case Side.Right:
                        at = new PointF(anchor.X + clearance + gap, anchor.Y - size.Height / 2);
                        break;
                    case Side.Left:
                        at = new PointF(anchor.X - clearance - gap - size.Width, anchor.Y - size.Height / 2);
                        break;
                    default:
                        at = new PointF(anchor.X - size.Width / 2, anchor.Y + clearance + gap);
                        break;
                }
                var rect = new RectangleF(at, size);
                using (var brush = new SolidBrush(owner.ColorOf(color, fallback)))
                    g.DrawString(text, font, brush, at);
                owner.HitRect(color, title, tip, rect);
                return rect;
            }
        }

        private Font MapFont(float size)
        {
            try { return new Font(string.IsNullOrEmpty(config.FontFamily) ? Settings.ResolveFont(null) : config.FontFamily, size); }
            catch { return new Font(Settings.ResolveFont(null), size); }
        }

        private void DrawMapSample(Graphics g, Rectangle area, Action<MapCanvas> scene)
        {
            GraphicsState saved = g.Save();
            g.SetClip(area);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (Font font = MapFont(8.25f))
                scene(new MapCanvas(this, g, font, area));
            g.Restore(saved);
        }

        private void DrawTacticalSample(Graphics g, Rectangle area)
        {
            DrawMapSample(g, area, DrawTacticalScene);
        }

        private void DrawGalacticSample(Graphics g, Rectangle area)
        {
            DrawMapSample(g, area, DrawGalacticScene);
        }

        #endregion

        #region Tactical map scene

        private const string BodyName = "TacMapBodyNameColour";

        private void BodyLabel(MapCanvas m, string text, PointF p, float clearance)
        {
            m.Label(text, p, Side.Below, clearance, BodyName, Color.White, "Body name", "Names on the tactical map.");
        }

        private void DrawTacticalScene(MapCanvas m)
        {
            float mark = m.MarkRadius();
            const string orbitTip = "Orbits.";

            PointF star = m.At(26, 52);
            foreach (float r in new[] { 11f, 19f, 28f, 41f })
                m.Circle(star, m.U(r), Stroke.Solid, 1f, "TacMapOrbitColour", Color.Gray, "Orbit", orbitTip);
            PointF cometCentre = m.Around(star, 9, 200);
            const float cometTilt = 20f;
            m.Orbit(cometCentre, m.U(30), m.U(17), cometTilt, Stroke.Solid, 1f, "TacMapOrbitColour", Color.Gray, "Orbit", orbitTip);

            m.Marker(star, Mark.Dot, "TacMapSystemBodyColour", Color.LightYellow, "System body", "System bodies.", 1.4f);
            BodyLabel(m, "Sol", star, m.MarkRadius(1.4f));

            PointF venus = m.Around(star, 11, 145);
            m.Marker(venus, Mark.Dot, "ColonyCostLessThanFiveColour", Color.Brown, "Colony cost 3.00 - 4.99", "Bodies with a colony cost under 5.");
            BodyLabel(m, "Venus", venus, mark);

            PointF earth = m.Around(star, 19, -40);
            m.Circle(earth, mark + m.U(1.0f), Stroke.Solid, 1.5f, "TacMapSurveyedColour", Color.Green, "Surveyed body", "Surveyed bodies.");
            m.Circle(earth, mark + m.U(2.0f), Stroke.Solid, 1.5f, "TacMapMineralsColour", Color.Yellow, "Minerals", "Bodies with known minerals.");
            m.Circle(earth, mark + m.U(3.0f), Stroke.Solid, 1.5f, "TacMapColonyColour", Color.Blue, "Colony", "Bodies with your colonies.");
            m.Marker(earth, Mark.Dot, "ColonyCostLessThanTwoColour", Color.Blue, "Colony cost 0.00 - 1.99", "Bodies with a colony cost under 2.");
            BodyLabel(m, "Earth", earth, mark + m.U(3.0f));

            PointF mars = m.Around(star, 28, 25);
            m.Marker(mars, Mark.Dot, "ColonyCostLessThanThreeColour", Color.Cyan, "Colony cost 2.00 - 2.99", "Bodies with a colony cost under 3.");
            BodyLabel(m, "Mars", mars, mark);

            PointF packet = new PointF(mars.X + (earth.X - mars.X) * 0.5f, mars.Y + (earth.Y - mars.Y) * 0.5f);
            m.Segment(mars, packet, Stroke.Solid, 1f, "TacMapMDPacketColour", Color.Orange, "Mineral packet", "Mass driver packets.");
            m.Marker(packet, Mark.Dot, "TacMapMDPacketColour", Color.Orange, "Mineral packet", "Mass driver packets.", 0.8f);
            m.Label("Packet to Earth", packet, Side.Right, mark, "TacMapMDPacketColour", Color.Orange, "Mineral packet", "Mass driver packets.");

            PointF jupiter = m.Around(star, 41, -120);
            m.Circle(jupiter, m.U(4), Stroke.Solid, 1f, "TacMapOrbitColour", Color.Gray, "Orbit", orbitTip);
            m.Marker(jupiter, Mark.Dot, "GasGiantColour", Color.Purple, "Gas giant", "Gas giants.", 1.4f);
            BodyLabel(m, "Jupiter", jupiter, m.U(4));
            PointF io = m.Around(jupiter, 4, 20);
            m.Marker(io, Mark.Dot, "TacMapSystemBodyColour", Color.Gray, "System body", "System bodies.", 0.7f);
            m.Label("Io", io, Side.Right, m.MarkRadius(0.7f), BodyName, Color.White, "Body name", "Names on the tactical map.");

            PointF comet = OnEllipse(cometCentre, m.U(30), m.U(17), cometTilt, 205);
            PointF tail = new PointF(comet.X + (comet.X - star.X) * 0.18f, comet.Y + (comet.Y - star.Y) * 0.18f);
            m.Segment(comet, tail, Stroke.Solid, 2f, "CometColour", Color.White, "Comet tail", "Comets.", 128);
            m.Marker(comet, Mark.Dot, "CometColour", Color.White, "Comet", "Comets.");
            BodyLabel(m, "Comet", comet, mark);

            PointF station = m.At(47, 32);
            m.Marker(station, Mark.Dot, "TacMapDeepSpacePopColour", Color.Yellow, "Deep space population", "Populations in deep space.");
            BodyLabel(m, "Gamma Station", station, mark);

            const string jpTip = "Jump points and jump gates.";
            PointF jp1 = m.At(8, 88);
            m.Marker(jp1, Mark.Dot, "JumpPointColour", Color.Purple, "Jump point", jpTip);
            m.Outline(jp1, Mark.Square, 2.2f, "JumpPointColour", Color.Purple, "Jump gate", jpTip);
            m.Label("JP1: Alpha Centauri", jp1, Side.Right, m.MarkRadius(2.2f), "JumpPointColour", Color.Purple, "Jump point", jpTip);
            PointF jp2 = m.At(40, 84);
            m.Marker(jp2, Mark.Dot, "JumpPointColour", Color.Purple, "Jump point", jpTip);
            m.Outline(jp2, Mark.Square, 2.2f, "JumpPointAlienColour", Color.Red, "Alien jump gate", "Alien jump gates.");
            m.Label("JP2: Unexplored", jp2, Side.Below, m.MarkRadius(2.2f), "JumpPointColour", Color.Purple, "Jump point", jpTip);

            PointF lagrange = m.Around(star, 41, -70);
            m.Marker(lagrange, Mark.Dot, "TacMapLagrangePointColour", Color.Orange, "Lagrange point", "Lagrange points.");
            m.Label("LP1", lagrange, Side.Right, mark, "TacMapLagrangePointColour", Color.Orange, "Lagrange point", "Lagrange points.");

            const string slTip = "Survey locations.";
            PointF sl1 = m.At(5, 40), sl2 = m.At(58, 60);
            m.Marker(sl1, Mark.Circle, "TacMapSurveyLocationColour", Color.Wheat, "Survey location", slTip);
            m.Marker(sl2, Mark.Dot, "TacMapSurveyLocationColour", Color.Wheat, "Survey location", slTip);
            BodyLabel(m, "SL 3", sl1, mark);
            BodyLabel(m, "SL 7", sl2, mark);

            PointF rift = m.At(10, 70);
            m.Disc(rift, m.U(2.6f), 2f, "TacMapRiftCentreColour", Color.Black, "Aether rift centre", "Aether rift centres.",
                "TacMapRiftOutlineColour", Color.Purple, "Aether rift outline", "Aether rift outlines.");
            m.Label("Aether Rift", rift, Side.Right, m.U(2.6f), BodyName, Color.White, "Body name", "Names on the tactical map.");
            PointF gate = m.At(26, 92);
            m.Marker(gate, Mark.Dot, "TacMapAetherGateColour", Color.Purple, "Aether gate", "Aether gates.");
            m.Label("Aether Gate", gate, Side.Right, mark, "TacMapAetherGateColour", Color.Purple, "Aether gate", "Aether gates.");

            m.Segment(rift, jp1, Stroke.Solid, 2f, "TacMapDistanceLineColour", Color.Green, "Distance line", "The distance measuring line.");
            PointF middle = new PointF((rift.X + jp1.X) / 2, (rift.Y + jp1.Y) / 2);
            m.Label("420m km", middle, Side.Right, m.U(1), "TacMapDistanceLineColour", Color.Green, "Distance line", "The distance measuring line.");

            DrawTacticalFleets(m);

            PointF scaleLeft = m.At(80, 96), scaleRight = m.At(97, 96);
            m.Segment(scaleLeft, scaleRight, Stroke.Solid, 2f, "TacticalScaleColour", Color.Green, "Scale bar", "The map scale bar.");
            m.Label("500m km", new PointF((scaleLeft.X + scaleRight.X) / 2, scaleLeft.Y), Side.Above, 1, "TacticalScaleColour", Color.Green, "Scale bar", "The map scale bar.");
        }

        private static PointF OnEllipse(PointF centre, float rx, float ry, float tilt, float degrees)
        {
            double t = degrees * Math.PI / 180, r = tilt * Math.PI / 180;
            double x = rx * Math.Cos(t), y = ry * Math.Sin(t);
            return new PointF(centre.X + (float)(x * Math.Cos(r) - y * Math.Sin(r)), centre.Y + (float)(x * Math.Sin(r) + y * Math.Cos(r)));
        }

        private void Contact(MapCanvas m, PointF p, float headingDegrees, string color, string label, string title, string tip)
        {
            PointF trail = m.Around(p, 3.5f, headingDegrees + 180);
            m.Segment(trail, p, Stroke.Solid, 1f, color, Color.White, title, tip);
            m.Marker(p, Mark.Dot, color, Color.White, title, tip);
            m.Label(label, p, Side.Right, m.MarkRadius(), color, Color.White, title, tip);
        }

        private void DrawTacticalFleets(MapCanvas m)
        {
            PointF fleet = m.At(84, 74);
            var rings = new[]
            {
                new { Color = "TacMapWeaponRangeColour", Fallback = Color.Orange, Radius = 6f, Stroke = Stroke.Dashed, Label = "Weapons", Title = "Weapon range" },
                new { Color = "TacMapFireControlColour", Fallback = Color.Red, Radius = 11f, Stroke = Stroke.Dashed, Label = "Fire Control", Title = "Fire control range" },
                new { Color = "TacMapActiveSensorColour", Fallback = Color.Blue, Radius = 17f, Stroke = Stroke.Dashed, Label = "Active Sensor", Title = "Active sensor range" },
                new { Color = "TacMapThermalSensorColour", Fallback = Color.Red, Radius = 23f, Stroke = Stroke.Dashed, Label = "Thermal Sensor", Title = "Thermal sensor range" },
                new { Color = "TacMapEMSensorColour", Fallback = Color.Plum, Radius = 29f, Stroke = Stroke.Dashed, Label = "EM Sensor", Title = "EM sensor range" },
                new { Color = "TacMapELINTSensorColour", Fallback = Color.Yellow, Radius = 35f, Stroke = Stroke.Dashed, Label = "ELINT Sensor", Title = "ELINT sensor range" },
            };
            foreach (var ring in rings)
            {
                m.Circle(fleet, m.U(ring.Radius), ring.Stroke, 1.25f, ring.Color, ring.Fallback, ring.Title, ring.Title + ".");
                m.Label(ring.Label, fleet, Side.Above, m.U(ring.Radius), ring.Color, ring.Fallback, ring.Title, ring.Title + ".");
            }

            const string fleetTip = "Your fleets.";
            PointF fleetTrail = m.Around(fleet, 3.5f, 150);
            m.Segment(fleetTrail, fleet, Stroke.Solid, 1f, "TacMapFleetColour", Color.Yellow, "Fleet", fleetTip);
            m.Marker(fleet, Mark.Dot, "TacMapFleetColour", Color.Yellow, "Fleet", fleetTip);
            m.Label("TF 1", fleet, Side.Right, m.MarkRadius(), "TacMapFleetColour", Color.Yellow, "Fleet", fleetTip);

            PointF hostile = m.At(66, 16);
            m.Circle(hostile, m.U(9), Stroke.Dashed, 1.25f, "TacMapAlienSensorColour", Color.Red, "Alien sensor range", "Known alien sensor range.");
            m.Label("Alien Sensor", hostile, Side.Above, m.U(9), "TacMapAlienSensorColour", Color.Red, "Alien sensor range", "Known alien sensor range.");
            Contact(m, hostile, 15, "HostileColour", "Raider", "Hostile contact", "Hostile contacts.");
            Contact(m, m.At(86, 8), -15, "NeutralColour", "Freighter", "Neutral contact", "Neutral contacts.");
            Contact(m, m.At(30, 6), 10, "FriendlyColour", "Scout", "Friendly contact", "Friendly contacts.");
            Contact(m, m.At(50, 12), -10, "AlliedColour", "Cruiser", "Allied contact", "Allied contacts.");

            PointF civilian = m.At(56, 44);
            m.Marker(civilian, Mark.Dot, "TacMapCivilianColour", Color.Cyan, "Civilian fleet", "Civilian fleets.");
            m.Label("Freighter 12", civilian, Side.Right, m.MarkRadius(), "TacMapCivilianColour", Color.Cyan, "Civilian fleet", "Civilian fleets.");

            PointF salvo = m.At(16, 12);
            m.Segment(m.Around(salvo, 3.5f, 135), salvo, Stroke.Solid, 1f, "TacMapSalvoColour", Color.LightBlue, "Missile salvo", "Your missile salvos.");
            m.Marker(salvo, Mark.Dot, "TacMapSalvoColour", Color.LightBlue, "Missile salvo", "Your missile salvos.", 0.8f);
            m.Label("4x Missile", salvo, Side.Right, m.MarkRadius(0.8f), "TacMapSalvoColour", Color.LightBlue, "Missile salvo", "Your missile salvos.");

            PointF wreck = m.At(60, 90);
            RectangleF wreckMark = m.Symbol(wreck, "X", 1.6f, "WreckColour", Color.Red, "Wreck", "Wrecks.");
            RectangleF wreckLabel = m.Label("Wreck", wreck, Side.Right, wreckMark.Width / 2, "WreckColour", Color.Red, "Wreck", "Wrecks.");
            m.Label("Life Pods", new PointF(wreck.X, wreckLabel.Top), Side.Above, 0, "TacMapLifepodColour", Color.Orange, "Life pods", "Life pods.");

            PointF waypoint = m.At(18, 34);
            m.Marker(waypoint, Mark.Dot, "TacMapWaypointColour", Color.Violet, "Waypoint", "Waypoints.", 0.8f);
            m.Label("Waypoint", waypoint, Side.Right, m.MarkRadius(0.8f), "TacMapWaypointColour", Color.Violet, "Waypoint", "Waypoints.");
        }

        #endregion

        #region Galactic map scene

        private const float NodeUnits = 5f;

        private void DrawGalacticScene(MapCanvas m)
        {
            var systems = new Dictionary<string, PointF>
            {
                ["Sol"] = m.At(10, 16),
                ["Alpha Centauri"] = m.At(36, 12),
                ["Sirius"] = m.At(62, 18),
                ["Procyon"] = m.At(88, 13),
                ["Lalande 21185"] = m.At(20, 48),
                ["Epsilon Eridani"] = m.At(46, 46),
                ["Tau Ceti"] = m.At(72, 50),
                ["Luyten's Star"] = m.At(91, 44),
                ["Wolf 359"] = m.At(10, 80),
                ["Ross 128"] = m.At(34, 84),
                ["Barnard's Star"] = m.At(60, 82),
                ["Gliese 1061"] = m.At(85, 82),
            };

            foreach (string[] pair in new[]
            {
                new[] { "Sol", "Alpha Centauri" }, new[] { "Sol", "Lalande 21185" }, new[] { "Sirius", "Procyon" },
                new[] { "Lalande 21185", "Epsilon Eridani" }, new[] { "Epsilon Eridani", "Tau Ceti" }, new[] { "Tau Ceti", "Luyten's Star" },
                new[] { "Lalande 21185", "Wolf 359" }, new[] { "Wolf 359", "Ross 128" }, new[] { "Ross 128", "Epsilon Eridani" },
                new[] { "Ross 128", "Barnard's Star" }, new[] { "Barnard's Star", "Gliese 1061" },
            })
                m.Segment(systems[pair[0]], systems[pair[1]], Stroke.Solid, 2f, "GalMapJumpPointConnectionsColour", Color.Gray,
                    "Jump point connection", "Jump links between systems.");
            m.Segment(systems["Alpha Centauri"], systems["Sirius"], Stroke.Solid, 3f, "GalMapStabilisedConnectionsColour", Color.Orange,
                "Gated connection", "Jump links gated at both ends.");
            m.Segment(systems["Gliese 1061"], systems["Tau Ceti"], Stroke.Solid, 3f, "GalMapStabilisedConnectionsColour", Color.Orange,
                "Gated connection", "Jump links gated at both ends.");
            m.Segment(systems["Procyon"], systems["Luyten's Star"], Stroke.Solid, 2.5f, "GalMapRestrictedColour", Color.Magenta,
                "Military restricted connection", "Restricted links.");

            Node(m, systems["Sol"], "Sol", "GalMapSystemSelectedColour", "Selected system", "The selected system.");
            StatusRing(m, systems["Sol"], 1, Stroke.Dashed, "GalMapNavalHQColour", "Naval headquarters", "Naval headquarters.");

            Node(m, systems["Alpha Centauri"], "Alpha Centauri", "GalMapSystemIconColour", "System", "System icons.");
            Count(m, systems["Alpha Centauri"], "ColonyCostLessThanTwoColour", 3, "Habitable worlds", "Worlds with a colony cost under 2.");

            Node(m, systems["Sirius"], "Sirius", "GalMapSystemIconColour", "System", "System icons.");
            StatusRing(m, systems["Sirius"], 1, Stroke.Solid, "GalMapSurveyIncompleteColour", "Survey incomplete", "Systems not fully surveyed.");

            Node(m, systems["Procyon"], "Procyon", "GalMapSystemIconColour", "System", "System icons.");
            StatusRing(m, systems["Procyon"], 1, Stroke.Dashed, "GalMapRestrictedColour", "Military restricted system", "Restricted systems.");

            Node(m, systems["Lalande 21185"], "Lalande 21185", "GalMapSystemIconColour", "System", "System icons.");
            StatusRing(m, systems["Lalande 21185"], 1, Stroke.Dashed, "GalMapMaintenanceLocationColour", "Maintenance location", "Maintenance locations.");
            Count(m, systems["Lalande 21185"], "ColonyCostLessThanThreeColour", 2, "Low cost worlds", "Worlds with a colony cost under 3.");

            Node(m, systems["Epsilon Eridani"], "Epsilon Eridani", "GalMapSystemIconColour", "System", "System icons.");
            StatusRing(m, systems["Epsilon Eridani"], 1, Stroke.Solid, "GalMapDormantJPColour", "Possible dormant jump point", "Possible dormant jump points.");

            Node(m, systems["Tau Ceti"], "Tau Ceti", "GalMapSystemIconColour", "System", "System icons.");
            StatusRing(m, systems["Tau Ceti"], 1, Stroke.Dashed, "GalMapGroundSurveyColour", "Ground survey locations", "Ground survey locations.");
            Count(m, systems["Tau Ceti"], "ColonyCostLessThanFiveColour", 1, "Medium cost worlds", "Worlds with a colony cost under 5.");

            Node(m, systems["Luyten's Star"], "Luyten's Star", "GalMapSystemIconColour", "System", "System icons.");
            StatusRing(m, systems["Luyten's Star"], 1, Stroke.Dashed, "GalMapRestrictedColour", "Blocked system", "Restricted systems.");

            Node(m, systems["Wolf 359"], "Wolf 359", "GalMapBlackHoleColour", "Black hole", "Black hole systems.");

            Node(m, systems["Ross 128"], "Ross 128", "GalMapSystemIconColour", "System", "System icons.");
            StatusRing(m, systems["Ross 128"], 1, Stroke.Dashed, "GalMapMineralSearchColour", "Mineral search", "Mineral search locations.");
            Count(m, systems["Ross 128"], "GalMapCometsColour", 4, "Comets", "Comet counts.");

            Node(m, systems["Barnard's Star"], "Barnard's Star", "GalMapSupernovaColour", "Supernova", "Supernova systems.");
            Node(m, systems["Gliese 1061"], "Gliese 1061", "GalMapSystemIconColour", "System", "System icons.");

            PointF a = systems["Barnard's Star"], b = systems["Gliese 1061"];
            float pad = m.U(NodeUnits + 3);
            var box = RectangleF.FromLTRB(Math.Min(a.X, b.X) - pad, Math.Min(a.Y, b.Y) - pad, Math.Max(a.X, b.X) + pad, Math.Max(a.Y, b.Y) + pad + m.U(3));
            m.Frame(box, Stroke.Solid, 2f, "GalMapMultiSystemSelectColour", Color.White, "Multiple system select", "The multi-system selection box.");
        }

        private void Node(MapCanvas m, PointF p, string name, string fill, string title, string tip)
        {
            m.Disc(p, m.U(NodeUnits), 1.5f, fill, Color.Green, title, tip, "GalMapSystemOutlineColour", Color.Lime, "System outline", "System icon outlines.");
            m.Label(name, p, Side.Below, m.U(NodeUnits + 2), "StandardTextColour", Color.White, "System name", "System names.");
        }

        private void StatusRing(MapCanvas m, PointF p, int ring, Stroke stroke, string color, string title, string tip)
        {
            m.Circle(p, m.U(NodeUnits + 1.2f * ring), stroke, 2f, color, Color.Red, title, tip);
        }

        /// <summary>A marker dot in the middle of a system with its count beside it, both inside the circle.</summary>
        private void Count(MapCanvas m, PointF p, string color, int count, string title, string tip)
        {
            var spot = p;
            m.Marker(spot, Mark.Dot, color, Color.Green, title, tip, 0.8f);
            m.Label(count.ToString(System.Globalization.CultureInfo.InvariantCulture), spot, Side.Right, m.MarkRadius(0.8f) - m.U(0.4f),
                "StandardTextColour", Color.White, "Count", "Marker counts.");
        }

        #endregion
    }
}
