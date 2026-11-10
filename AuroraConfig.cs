using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace AuroraThemeManager
{
    internal sealed class AuroraConfig
    {
        #region Types

        internal sealed class ColorEntry
        {
            public string Name;
            public string Group;
            public string Label;
            public bool Moved;
            public bool HasAlpha;
            public int Alpha = 255;
            public Color Value;

            public Color? AbsentDefault;

            public bool AlphaAllowed
            {
                get { return !NoAlphaColors.Contains(Name); }
            }

            public Color Argb
            {
                get { return Color.FromArgb(Alpha, Value.R, Value.G, Value.B); }
            }
        }

        internal static readonly HashSet<string> NoAlphaColors = new HashSet<string>
        {
            "Background", "ButtonColour", "ToolbarButtonColour", "IconColour", "TextEditColour",
            "PositiveColour", "ObsoleteColour", "NoResearchColour", "PrisonerColour",
            "AlienComponentColour", "PrototypeComponentColour", "MatchingComponentColour", "ComponentTechDataColour",
            "ComponentResearchColour", "OrdnanceSeriesColour",
            "MiningColonyColour", "OtherColonyColour", "CMCPurchaseColonyColour",
            "NavalSubFleetColour", "NavalSquadronColour", "CivilianShippingLineColour",
            "GroundForcesOrganisationColour", "SupportingFormationColour", "SupportedFormationColour",
            "TacMapCometOutlineColour",
        };

        internal sealed class ButtonStyleEntry
        {
            public string ElementName;
            public string Label;
            public string FlatStyle = "Flat";
            public int BorderSize;
        }

        #endregion

        #region Constants

        internal const string FileName = "AuroraConfig.xml";

        internal static string NormalizeFlatStyle(string text)
        {
            string match = new[] { "Flat", "Popup", "Standard", "System" }
                .FirstOrDefault(f => string.Equals(f, (text ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
            if (match == "Popup")
                return "Flat";
            if (match == "System")
                return "Standard";
            return match;
        }

        private static readonly string[] ButtonStyleElements = { "ButtonStyle", "ToolbarButtonStyle" };

        internal const string NonPlayerRacesGroup = "Non-Player Races";
        internal const string OrderOfBattleGroup = "Order of Battle Chart";
        internal const string ShipDamageGroup = "Ship Damage Report";
        internal const string ClassDesignGroup = "Class Design";
        internal const string CiviliansGroup = "Civilians";

        private static readonly Dictionary<string, string> GroupOverrides = new Dictionary<string, string>
        {
            ["MiningColonyColour"] = "Other Status Colors",
            ["Map3DSystemIconColour"] = "3D Galaxy Map",
            ["HostileColour"] = NonPlayerRacesGroup,
            ["SupportedFormationColour"] = OrderOfBattleGroup,
            ["SupportingFormationColour"] = OrderOfBattleGroup,
            ["ArmourDamagedColour"] = ShipDamageGroup,
            ["MatchingComponentColour"] = ClassDesignGroup,
            ["ObsoleteColour"] = ClassDesignGroup,
            ["AlienComponentColour"] = ClassDesignGroup,
            ["PrototypeComponentColour"] = ClassDesignGroup,
            ["TacMapCivilianColour"] = CiviliansGroup,
            ["CivilianShippingLineColour"] = CiviliansGroup,
            ["CMCPurchaseColonyColour"] = CiviliansGroup
        };

        private static readonly KeyValuePair<string, Color>[] LaterVersionColors =
        {
            new KeyValuePair<string, Color>("NavalFleetBoxColour", Color.FromArgb(0, 255, 255)),
            new KeyValuePair<string, Color>("NavalSubFleetBoxColour", Color.FromArgb(80, 160, 200)),
        };

        private static readonly Dictionary<string, string> LabelOverrides = new Dictionary<string, string>
        {
            ["TacMapCivilianColour"] = "Civilian Fleet",
            ["ObsoleteColour"] = "Obsolete Component"
        };

        #endregion

        #region State

        private bool crlf = true;

        public string Path { get; private set; }
        public XDocument Document { get; private set; }

        public readonly List<ColorEntry> Colors = new List<ColorEntry>();
        public readonly List<ButtonStyleEntry> ButtonStyles = new List<ButtonStyleEntry>();
        public string FontFamily = "";
        public int MinimumSaveDays = 90;

        public readonly List<string> UnhandledElements = new List<string>();

        #endregion

        #region Locating the file

        internal static string GameDirectory()
        {
            return Program.ToolDirectory;
        }

        internal static string DefaultPath()
        {
            return System.IO.Path.Combine(GameDirectory(), FileName);
        }

        #endregion

        #region Loading

        internal static void CreateDefault(string path)
        {
            using (Stream template = typeof(AuroraConfig).Assembly.GetManifestResourceStream(AuroraDefaults.ThemeResource(AuroraDefaults.DefaultName)))
            using (FileStream file = File.Create(path))
                template.CopyTo(file);
        }

        /// <summary>Reads one of the AuroraConfig.xml files embedded in ATM.  It has no path and is never saved.</summary>
        internal static AuroraConfig LoadResource(string resourceName)
        {
            string text;
            using (Stream stream = typeof(AuroraConfig).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new FileNotFoundException("Missing embedded theme " + resourceName);
                using (var reader = new StreamReader(stream))
                    text = reader.ReadToEnd();
            }
            return FromText(text);
        }

        /// <summary>
        /// True when the file holds the same elements and values as Aurora's default AuroraConfig.xml.
        /// Whitespace, comments and line endings are ignored.
        /// </summary>
        internal static bool MatchesDefault(string path)
        {
            string text;
            using (Stream stream = typeof(AuroraConfig).Assembly.GetManifestResourceStream(AuroraDefaults.ThemeResource(AuroraDefaults.DefaultName)))
            using (var reader = new StreamReader(stream))
                text = reader.ReadToEnd();
            return XNode.DeepEquals(Normalized(XDocument.Parse(text)), Normalized(XDocument.Load(path)));
        }

        private static XElement Normalized(XDocument document)
        {
            var root = new XElement(document.Root);
            root.DescendantNodesAndSelf().OfType<XComment>().ToList().ForEach(c => c.Remove());
            foreach (XElement element in root.DescendantsAndSelf())
                if (!element.HasElements && !element.IsEmpty)
                    element.Value = element.Value.Trim();
            return root;
        }

        /// <summary>Reads AuroraConfig.xml text held in memory.  It has no path and is never saved.</summary>
        internal static AuroraConfig FromText(string text)
        {
            var config = new AuroraConfig { crlf = text.Contains("\r\n") };
            config.Document = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
            config.Parse();
            return config;
        }

        internal static AuroraConfig Load(string path = null)
        {
            var config = new AuroraConfig { Path = path ?? DefaultPath() };

            if (File.Exists(config.Path))
            {
                config.crlf = File.ReadAllText(config.Path).Contains("\r\n");
                config.Document = XDocument.Load(config.Path, LoadOptions.PreserveWhitespace);
            }
            else
            {
                config.Document = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement("AuroraConfig"));
            }

            config.Parse();
            return config;
        }

        private void Parse()
        {
            Colors.Clear();
            ButtonStyles.Clear();
            UnhandledElements.Clear();

            XElement root = Document.Root;
            if (root == null)
                return;

            XElement colours = root.Element("Colours");
            if (colours != null)
                ParseColours(colours);
            AddLaterVersionColors();

            foreach (string name in ButtonStyleElements)
            {
                XElement style = root.Element(name);
                if (style == null)
                    continue;

                var entry = new ButtonStyleEntry
                {
                    ElementName = name,
                    Label = Humanize(name),
                    FlatStyle = NormalizeFlatStyle((string)style.Element("FlatStyle")) ?? "Flat"
                };
                int border;
                if (int.TryParse(((string)style.Element("BorderSize") ?? "0").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out border))
                    entry.BorderSize = border;
                ButtonStyles.Add(entry);

                foreach (XElement child in style.Elements())
                {
                    if (child.Name.LocalName != "FlatStyle" && child.Name.LocalName != "BorderSize")
                        UnhandledElements.Add(name + "/" + child.Name.LocalName);
                }
            }

            XElement map = root.Element("MapDisplay");
            if (map != null)
            {
                FontFamily = ((string)map.Element("FontFamily") ?? "").Trim();
                foreach (XElement child in map.Elements())
                {
                    if (child.Name.LocalName != "FontFamily")
                        UnhandledElements.Add("MapDisplay/" + child.Name.LocalName);
                }
            }

            XElement log = root.Element("EventLog");
            if (log != null)
            {
                int days;
                if (int.TryParse(((string)log.Element("MinimumSaveDays") ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out days))
                    MinimumSaveDays = days;
                foreach (XElement child in log.Elements())
                {
                    if (child.Name.LocalName != "MinimumSaveDays")
                        UnhandledElements.Add("EventLog/" + child.Name.LocalName);
                }
            }

            var known = new HashSet<string>(ButtonStyleElements) { "Colours", "MapDisplay", "EventLog" };
            foreach (XElement child in root.Elements())
            {
                if (!known.Contains(child.Name.LocalName))
                    UnhandledElements.Add(child.Name.LocalName);
            }
        }

        private void ParseColours(XElement colours)
        {
            string group = "General";

            foreach (XNode node in colours.Nodes())
            {
                XComment comment = node as XComment;
                if (comment != null)
                {
                    string title = GroupFromComment(comment.Value);
                    if (title != null)
                        group = title;
                    continue;
                }

                XElement element = node as XElement;
                if (element == null)
                    continue;

                int r, g, b;
                if (!TryChannel(element, "R", out r) || !TryChannel(element, "G", out g) || !TryChannel(element, "B", out b))
                {
                    UnhandledElements.Add("Colours/" + element.Name.LocalName);
                    continue;
                }

                var entry = new ColorEntry
                {
                    Name = element.Name.LocalName,
                    Label = Humanize(element.Name.LocalName),
                    Value = Color.FromArgb(r, g, b)
                };

                string overrideLabel;
                if (LabelOverrides.TryGetValue(entry.Name, out overrideLabel))
                    entry.Label = overrideLabel;

                string overrideGroup;
                entry.Moved = GroupOverrides.TryGetValue(entry.Name, out overrideGroup);
                entry.Group = entry.Moved ? overrideGroup : group;

                int a;
                if (TryChannel(element, "A", out a))
                {
                    entry.HasAlpha = true;
                    entry.Alpha = a;
                }

                Colors.Add(entry);
            }
        }

        private void AddLaterVersionColors()
        {
            foreach (KeyValuePair<string, Color> color in LaterVersionColors)
            {
                if (Find(color.Key) != null)
                    continue;
                Colors.Add(new ColorEntry
                {
                    Name = color.Key,
                    Label = Humanize(color.Key),
                    Group = OrderOfBattleGroup,
                    Value = color.Value,
                    AbsentDefault = color.Value
                });
            }
        }

        private static bool TryChannel(XElement element, string attribute, out int value)
        {
            value = 0;
            XAttribute attr = element.Attribute(attribute);
            if (attr == null)
                return false;
            if (!int.TryParse(attr.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return false;
            value = Math.Max(0, Math.Min(255, value));
            return true;
        }

        #endregion

        #region Lookup

        public ColorEntry Find(string name)
        {
            return Colors.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public ButtonStyleEntry FindStyle(string elementName)
        {
            return ButtonStyles.FirstOrDefault(s => s.ElementName == elementName);
        }

        public Color ColorOr(string name, Color fallback)
        {
            ColorEntry entry = Find(name);
            return entry == null ? fallback : entry.Value;
        }

        public List<KeyValuePair<string, List<ColorEntry>>> Groups()
        {
            var order = new List<string>();
            var map = new Dictionary<string, List<ColorEntry>>();
            foreach (ColorEntry entry in Colors)
            {
                if (!map.ContainsKey(entry.Group))
                {
                    map[entry.Group] = new List<ColorEntry>();
                    order.Add(entry.Group);
                }
                map[entry.Group].Add(entry);
            }
            return order.Select(g => new KeyValuePair<string, List<ColorEntry>>(g,
                map[g].Where(c => !c.Moved).Concat(map[g].Where(c => c.Moved)).ToList())).ToList();
        }

        #endregion

        #region Saving

        public void Save(string path = null)
        {
            if (path != null)
                Path = path;

            XElement root = Document.Root;
            if (root == null)
            {
                root = new XElement("AuroraConfig");
                Document.Add(root);
            }

            XElement colours = EnsureChild(root, "Colours");
            foreach (ColorEntry entry in Colors)
            {
                XElement element = colours.Element(entry.Name);
                if (element == null && entry.AbsentDefault.HasValue && entry.Argb.ToArgb() == entry.AbsentDefault.Value.ToArgb())
                    continue;
                entry.AbsentDefault = null;
                if (element == null)
                {
                    element = new XElement(entry.Name);
                    AddOnOwnLine(colours, element);
                }

                element.SetAttributeValue("R", entry.Value.R.ToString(CultureInfo.InvariantCulture));
                element.SetAttributeValue("G", entry.Value.G.ToString(CultureInfo.InvariantCulture));
                element.SetAttributeValue("B", entry.Value.B.ToString(CultureInfo.InvariantCulture));
                if (entry.Alpha != 255)
                    entry.HasAlpha = true;
                if (entry.HasAlpha)
                    element.SetAttributeValue("A", entry.Alpha.ToString(CultureInfo.InvariantCulture));
            }

            foreach (ButtonStyleEntry style in ButtonStyles)
            {
                XElement element = EnsureChild(root, style.ElementName);
                SetChildText(element, "FlatStyle", style.FlatStyle);
                SetChildText(element, "BorderSize", style.BorderSize.ToString(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrEmpty(FontFamily))
            {
                XElement map = EnsureChild(root, "MapDisplay");
                SetChildText(map, "FontFamily", FontFamily);
            }

            XElement log = EnsureChild(root, "EventLog");
            SetChildText(log, "MinimumSaveDays", MinimumSaveDays.ToString(CultureInfo.InvariantCulture));

            string directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temp = Path + ".atm.tmp";
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                OmitXmlDeclaration = false,
                NewLineHandling = NewLineHandling.None
            };

            string text;
            using (var buffer = new Utf8StringWriter())
            {
                using (XmlWriter writer = XmlWriter.Create(buffer, settings))
                {
                    Document.Save(writer);
                }
                text = buffer.ToString();
            }
            text = Regex.Replace(text, "\"\\s/>", "\"/>");
            text = text.Replace("\r\n", "\n");
            if (crlf)
                text = text.Replace("\n", "\r\n");
            File.WriteAllText(temp, text, new UTF8Encoding(false));

            if (File.Exists(Path))
                File.Delete(Path);
            File.Move(temp, Path);
        }

        private static void AddOnOwnLine(XElement parent, XElement element)
        {
            XElement last = parent.Elements().LastOrDefault();
            var before = last?.PreviousNode as XText;
            string indent = before != null && string.IsNullOrWhiteSpace(before.Value) ? before.Value : "\n    ";
            if (last != null)
                last.AddAfterSelf(new XText(indent), element);
            else
                parent.AddFirst(new XText(indent), element);
        }

        private static XElement EnsureChild(XElement parent, string name)
        {
            XElement child = parent.Element(name);
            if (child == null)
            {
                child = new XElement(name);
                parent.Add(new XText("\n  "), child);
            }
            return child;
        }

        private static void SetChildText(XElement parent, string name, string value)
        {
            XElement child = parent.Element(name);
            if (child == null)
            {
                child = new XElement(name);
                parent.Add(new XText("\n    "), child);
            }
            child.Value = value;
        }

        private sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding { get { return new UTF8Encoding(false); } }
        }

        #endregion

        #region Presets

        public Dictionary<string, string> ToPreset()
        {
            var values = new Dictionary<string, string>();
            foreach (ColorEntry entry in Colors)
            {
                string text = entry.Value.R + "," + entry.Value.G + "," + entry.Value.B;
                if (entry.HasAlpha || entry.Alpha != 255)
                    text += "," + entry.Alpha;
                values[entry.Name] = text;
            }
            foreach (ButtonStyleEntry style in ButtonStyles)
            {
                values[style.ElementName + ".FlatStyle"] = style.FlatStyle;
                values[style.ElementName + ".BorderSize"] = style.BorderSize.ToString(CultureInfo.InvariantCulture);
            }
            if (!string.IsNullOrEmpty(FontFamily))
                values["MapDisplay.FontFamily"] = FontFamily;
            return values;
        }

        public int ApplyPreset(Dictionary<string, string> values)
        {
            int applied = 0;

            foreach (KeyValuePair<string, string> pair in values)
            {
                string key = pair.Key;
                string text = pair.Value.Trim();

                ColorEntry color = Find(key);
                if (color != null)
                {
                    int[] parts;
                    if (TryParseChannels(text, out parts))
                    {
                        color.Value = Color.FromArgb(parts[0], parts[1], parts[2]);
                        color.Alpha = parts.Length > 3 ? Math.Max(0, Math.Min(255, parts[3])) : 255;
                        applied++;
                    }
                    continue;
                }

                int dot = key.IndexOf('.');
                if (dot > 0)
                {
                    string element = key.Substring(0, dot);
                    string field = key.Substring(dot + 1);

                    ButtonStyleEntry style = FindStyle(element);
                    if (style != null && field == "FlatStyle" && NormalizeFlatStyle(text) != null)
                    {
                        style.FlatStyle = NormalizeFlatStyle(text);
                        applied++;
                    }
                    else if (style != null && field == "BorderSize")
                    {
                        int border;
                        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out border))
                        {
                            style.BorderSize = Math.Max(0, border);
                            applied++;
                        }
                    }
                    else if (element == "MapDisplay" && field == "FontFamily" && text.Length > 0)
                    {
                        FontFamily = text;
                        applied++;
                    }
                }
            }

            return applied;
        }

        private static bool TryParseChannels(string text, out int[] parts)
        {
            parts = null;
            string[] pieces = text.Split(',');
            if (pieces.Length != 3 && pieces.Length != 4)
                return false;

            var values = new int[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                int v;
                if (!int.TryParse(pieces[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                    return false;
                values[i] = Math.Max(0, Math.Min(255, v));
            }
            parts = values;
            return true;
        }

        #endregion

        #region Names

        private static readonly Regex CamelBoundary = new Regex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.Compiled);
        private static readonly Regex OriginalComment = new Regex(@"^\s*original\s+(?<name>[a-z ]+?)(\s+or\s+close)?\s*\((?<rgb>[0-9, ]+)\)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static string Humanize(string name)
        {
            string text = name;
            if (text.EndsWith("Colour", StringComparison.Ordinal) && text.Length > "Colour".Length)
                text = text.Substring(0, text.Length - "Colour".Length);

            text = text.Replace("TacMap", "TacticalMap").Replace("GalMap", "GalacticMap")
                       .Replace("MDPacket", "MassDriverPacket");
            text = CamelBoundary.Replace(text, " ");
            text = text.Replace("Tactical Map", "Tactical Map");
            return text.Trim();
        }

        private static string GroupFromComment(string comment)
        {
            string text = comment.Trim();
            if (text.Length == 0)
                return null;

            Match match = OriginalComment.Match(text);
            if (match.Success)
            {
                string color = match.Groups["name"].Value.Trim();
                color = char.ToUpperInvariant(color[0]) + color.Substring(1);
                string rgb = Regex.Replace(match.Groups["rgb"].Value, @"\s+", "");
                if (string.Equals(color, "White", StringComparison.OrdinalIgnoreCase))
                    return ShipDamageGroup;
                return color + " Status Colors (default " + rgb.Replace(",", ", ") + ")";
            }

            text = text.Replace("Artifical", "Artificial").Replace("Colours", "Colors");
            if (text == "Non-Hostile Relations")
                return NonPlayerRacesGroup;
            return text;
        }

        #endregion
    }
}
