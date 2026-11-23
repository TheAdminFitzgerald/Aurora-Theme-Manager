using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AuroraThemeManager
{
    /// <summary>One of Aurora's event types.</summary>
    internal sealed class EventType
    {
        public int Id;
        public string Name;
        public EventCategory Category;
    }

    /// <summary>A group of event types, named in Resources\Events\EventTypes.csv.  Unlisted types go in Other.</summary>
    internal sealed class EventCategory
    {
        public string Name;

        private static readonly Dictionary<string, EventCategory> byName =
            new Dictionary<string, EventCategory>(StringComparer.OrdinalIgnoreCase);

        internal static EventCategory Other { get { return ByName("Other"); } }

        /// <summary>The category with this name (ignoring case), created on first use.</summary>
        internal static EventCategory ByName(string name)
        {
            name = string.IsNullOrWhiteSpace(name) ? "Other" : name.Trim();
            lock (byName)
            {
                EventCategory category;
                if (!byName.TryGetValue(name, out category))
                    byName[name] = category = new EventCategory { Name = name };
                return category;
            }
        }
    }

    /// <summary>The text and background colours of one event type.</summary>
    internal sealed class EventColour
    {
        public Color Text;
        public Color Back;
    }

    /// <summary>
    /// Event colours in the CSV format Aurora's Events window imports: <c>EventTypeID,Background,Text</c>,
    /// colours as signed 32-bit ARGB (alpha ignored).  The working set is kept in ATM\EventColours.csv.
    /// </summary>
    internal sealed class EventColourSet
    {
        private readonly Dictionary<int, EventColour> colours = new Dictionary<int, EventColour>();

        internal static string WorkingPath
        {
            get { return Path.Combine(Program.DataDirectory, "EventColours.csv"); }
        }

        internal int Count { get { return colours.Count; } }

        internal IEnumerable<int> Ids { get { return colours.Keys; } }

        internal EventColour Get(int id)
        {
            EventColour colour;
            return colours.TryGetValue(id, out colour) ? colour : null;
        }

        internal void Set(int id, Color text, Color back)
        {
            colours[id] = new EventColour { Text = Opaque(text), Back = Opaque(back) };
        }

        internal void Clear()
        {
            colours.Clear();
        }

        /// <summary>Gives every event type the same text and background colour.</summary>
        internal void Fill(IEnumerable<EventType> types, Color text, Color back)
        {
            colours.Clear();
            foreach (EventType type in types)
                Set(type.Id, text, back);
        }

        private static Color Opaque(Color c)
        {
            return Color.FromArgb(255, c.R, c.G, c.B);
        }

        #region CSV

        /// <summary>Reads an event colour CSV, counting malformed lines in <paramref name="skipped"/>.</summary>
        internal static EventColourSet Read(string path, out int skipped)
        {
            return Read(File.ReadAllLines(path), out skipped);
        }

        internal static EventColourSet Read(IEnumerable<string> lines, out int skipped)
        {
            var set = new EventColourSet();
            skipped = 0;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0)
                    continue;
                string[] parts = line.Split(',');
                int id, back, text;
                if (parts.Length < 3
                    || !int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id)
                    || !int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out back)
                    || !int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out text))
                {
                    skipped++;
                    continue;
                }
                set.Set(id, Color.FromArgb(text), Color.FromArgb(back));
            }
            return set;
        }

        /// <summary>Writes the set in Aurora's format, sorted by event type.</summary>
        internal void Write(string path)
        {
            var sb = new StringBuilder();
            foreach (KeyValuePair<int, EventColour> pair in colours.OrderBy(p => p.Key))
            {
                sb.Append(pair.Key.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(pair.Value.Back.ToArgb().ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(pair.Value.Text.ToArgb().ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            }
            string temp = path + ".tmp";
            File.WriteAllText(temp, sb.ToString(), new UTF8Encoding(false));
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }

        #endregion

        #region Event types

        /// <summary>
        /// Event type names from AuroraDB.db (read only), falling back to the built-in list.  Unnamed
        /// types and "Message Continued" are left out.
        /// </summary>
        internal static List<EventType> LoadTypes(out bool fromDatabase)
        {
            fromDatabase = false;
            var types = new Dictionary<int, string>();
            var categories = new Dictionary<int, EventCategory>();
            foreach (Tuple<int, string, string> row in BuiltInTypes())
            {
                types[row.Item1] = row.Item3;
                categories[row.Item1] = EventCategory.ByName(row.Item2);
            }
            try
            {
                foreach (KeyValuePair<int, string> pair in DatabaseTypes())
                    types[pair.Key] = pair.Value;
                fromDatabase = true;
            }
            catch (Exception ex)
            {
                Log.Error("Events: could not read event types from AuroraDB.db, using ATM's list: " + ex.Message);
            }
            return Sorted(types, categories);
        }

        /// <summary>The built-in event types only, for the Visual Studio designer.</summary>
        internal static List<EventType> BuiltInTypeList()
        {
            var types = new Dictionary<int, string>();
            var categories = new Dictionary<int, EventCategory>();
            foreach (Tuple<int, string, string> row in BuiltInTypes())
            {
                types[row.Item1] = row.Item3;
                categories[row.Item1] = EventCategory.ByName(row.Item2);
            }
            return Sorted(types, categories);
        }

        /// <summary>Sample colours for the Visual Studio designer (Resources\Events\DesignSample.csv).</summary>
        internal static EventColourSet DesignSample()
        {
            const string name = "AuroraThemeManager.Resources.Events.DesignSample.csv";
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null)
                    return new EventColourSet();
                using (var reader = new StreamReader(stream))
                {
                    int skipped;
                    return Read(reader.ReadToEnd().Split('\n'), out skipped);
                }
            }
        }

        private static List<EventType> Sorted(Dictionary<int, string> types, Dictionary<int, EventCategory> categories)
        {
            return types
                .Where(p => p.Value.Length > 0 && !p.Value.Equals("Message Continued", StringComparison.OrdinalIgnoreCase))
                .Select(p => new EventType
                {
                    Id = p.Key,
                    Name = p.Value,
                    Category = categories.ContainsKey(p.Key) ? categories[p.Key] : EventCategory.Other,
                })
                .OrderBy(t => t.Category.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // EventTypeID,Category,Name lines; # starts a comment.
        private static IEnumerable<Tuple<int, string, string>> BuiltInTypes()
        {
            const string name = "AuroraThemeManager.Resources.Events.EventTypes.csv";
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null)
                    yield break;
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.StartsWith("#"))
                            continue;
                        string[] parts = line.Split(new[] { ',' }, 3);
                        int id;
                        if (parts.Length == 3 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                            yield return Tuple.Create(id, parts[1].Trim(), parts[2].Trim());
                    }
                }
            }
        }

        private static List<KeyValuePair<int, string>> DatabaseTypes()
        {
            var list = new List<KeyValuePair<int, string>>();
            string dbPath = Path.Combine(AuroraConfig.GameDirectory(), "AuroraDB.db");
            if (!File.Exists(dbPath))
                throw new FileNotFoundException("AuroraDB.db not found", dbPath);

            Assembly sqlite = Assembly.UnsafeLoadFrom(Path.Combine(AuroraConfig.GameDirectory(), "System.Data.SQLite.dll"));
            Type connectionType = sqlite.GetType("System.Data.SQLite.SQLiteConnection", true);
            using (var connection = (DbConnection)Activator.CreateInstance(connectionType, "Data Source=" + dbPath + ";Read Only=True;"))
            {
                connection.Open();
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT EventTypeID, Description FROM DIM_EventType";
                    using (DbDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader.IsDBNull(0))
                                continue;
                            int id = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
                            string text = reader.IsDBNull(1) ? "" : reader.GetValue(1).ToString().Trim();
                            list.Add(new KeyValuePair<int, string>(id, text));
                        }
                    }
                }
            }
            return list;
        }

        #endregion
    }
}
