using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AuroraThemeManager
{
    public partial class Settings : Form
    {
        #region Static - Settings file

        internal static string CurrentPreset = "";

        internal static bool FirstLaunch = true;

        internal static bool ShowTransparencySelectors;

        private static string GetConfigFilePath()
        {
            return Path.Combine(Program.DataDirectory, "ATM.config");
        }

        public static string GetPresetsDirectory()
        {
            string presetsDir = Path.Combine(Program.DataDirectory, "Presets");
            Directory.CreateDirectory(presetsDir);
            return presetsDir;
        }

        public static void LoadSettings()
        {
            try
            {
                string path = GetConfigFilePath();
                if (!File.Exists(path))
                {
                    Log.Info("LoadSettings: no ATM.config, using defaults");
                    return;
                }

                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;
                    int equals = line.IndexOf('=');
                    if (equals > 0)
                        values[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
                }

                string text;
                CurrentPreset = values.TryGetValue("current_preset", out text) && text.Length > 0 ? text : "";
                bool first;
                FirstLaunch = !values.TryGetValue("firstlaunch", out text) || !bool.TryParse(text, out first) || first;
                bool showTransparency;
                ShowTransparencySelectors = (values.TryGetValue("transparency_selectors", out text) || values.TryGetValue("alpha_all_colors", out text))
                    && bool.TryParse(text, out showTransparency) && showTransparency;

                Log.Info("LoadSettings: loaded " + path);
            }
            catch (Exception ex)
            {
                Log.Error("LoadSettings: failed, using defaults: " + ex.Message);
            }
        }

        public static void SaveSettings()
        {
            try
            {
                File.WriteAllLines(GetConfigFilePath(), new[]
                {
                    "# Aurora Theme Manager settings",
                    "current_preset=" + CurrentPreset,
                    "firstlaunch=" + FirstLaunch,
                    "transparency_selectors=" + ShowTransparencySelectors
                });
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save ATM.config: " + ex.Message);
            }
        }

        #endregion

        #region Static - Presets

        private const string PresetExtension = ".xml";
        private const string LegacyPresetExtension = ".preset";
        private static bool legacyPresetsConverted;

        internal static string PresetPath(string name)
        {
            return Path.Combine(GetPresetsDirectory(), name + PresetExtension);
        }

        /// <summary>
        /// Themes are stored as AuroraConfig.xml files, so a theme can be copied into an Aurora folder as
        /// its AuroraConfig.xml, and any AuroraConfig.xml dropped into Presets shows up as a theme.
        /// </summary>
        internal static List<string> PresetNames()
        {
            ConvertLegacyPresets();
            var names = new List<string>(AuroraDefaults.BuiltInThemes);
            try
            {
                names.AddRange(Directory.GetFiles(GetPresetsDirectory(), "*" + PresetExtension)
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Where(n => !AuroraDefaults.Presets.ContainsKey(n))
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            }
            catch { }
            return names;
        }

        internal static Dictionary<string, string> ReadPreset(string name)
        {
            Dictionary<string, string> builtIn;
            if (AuroraDefaults.Presets.TryGetValue(name, out builtIn))
                return new Dictionary<string, string>(builtIn);

            string path = PresetPath(name);
            if (!File.Exists(path))
                return new Dictionary<string, string>();
            try
            {
                return AuroraConfig.Load(path).ToPreset();
            }
            catch (Exception ex)
            {
                Log.Error("Could not read theme " + path + ": " + ex.Message);
                return new Dictionary<string, string>();
            }
        }

        /// <summary>Saves the theme as a copy of AuroraConfig.xml as it stands.</summary>
        internal static void WritePreset(string name, AuroraConfig config)
        {
            config.Save();
            File.Copy(config.Path, PresetPath(name), true);
        }

        /// <summary>
        /// Turns themes saved by older versions, Name=R,G,B lines in a .preset file, into AuroraConfig.xml
        /// files.  Each is built on Aurora's default config with the old values applied, and the old file
        /// is kept as .preset.bak.
        /// </summary>
        private static void ConvertLegacyPresets()
        {
            if (legacyPresetsConverted)
                return;
            legacyPresetsConverted = true;
            string[] legacy;
            try { legacy = Directory.GetFiles(GetPresetsDirectory(), "*" + LegacyPresetExtension); }
            catch { return; }

            foreach (string file in legacy)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                string target = PresetPath(name);
                try
                {
                    if (!File.Exists(target))
                    {
                        AuroraConfig.CreateDefault(target);
                        AuroraConfig theme = AuroraConfig.Load(target);
                        theme.ApplyPreset(ReadLegacyPreset(file));
                        theme.Save();
                        Log.Info("Converted theme " + Path.GetFileName(file) + " to " + Path.GetFileName(target));
                    }
                    string backup = file + ".bak";
                    if (File.Exists(backup))
                        File.Delete(backup);
                    File.Move(file, backup);
                }
                catch (Exception ex)
                {
                    Log.Error("Could not convert theme " + file + ": " + ex.Message);
                }
            }
        }

        private static Dictionary<string, string> ReadLegacyPreset(string path)
        {
            var values = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(path))
            {
                string text = line.Trim();
                if (text.Length == 0 || text.StartsWith("#"))
                    continue;
                int equals = text.IndexOf('=');
                if (equals > 0)
                    values[text.Substring(0, equals).Trim()] = text.Substring(equals + 1).Trim();
            }
            return values;
        }

        #endregion

        #region Instance Fields

        private const string ModifiedSuffix = " (modified)";

        private readonly AuroraConfig config;
        private bool suppressEvents;
        private bool backupMade;
        private bool saveFailureShown;
        private DateTime configWriteTime;

        private string selectedTheme;

        private bool themeModified;

        #endregion

        #region Constructor

        internal Settings(AuroraConfig config)
        {
            this.config = config;
            configWriteTime = ConfigWriteTime();

            Load += (s, e) => AppIcon.ApplyTo(this);

            suppressEvents = true;
            InitializeComponent();
            BuildDynamicUi();
            FixDefaultFont();
            RefreshPresetList();
            SelectPreset(CurrentPreset);
            if (selectedTheme == null)
                SelectPreset(PresetNames().FirstOrDefault(n => !DiffersFromTheme(n)));
            themeModified = selectedTheme != null && DiffersFromTheme(selectedTheme);
            UpdateThemeDisplay();
            RefreshAllControls();
            suppressEvents = false;
        }

        private void FixDefaultFont()
        {
            string current = config.FontFamily;
            bool isDefault = string.IsNullOrEmpty(current)
                || string.Equals(current, AuroraDefaults.FontFallbacks[0], StringComparison.OrdinalIgnoreCase);
            if (!isDefault || IsFontInstalled(current))
                return;

            string resolved = ResolveFont(current);
            if (string.Equals(resolved, current, StringComparison.OrdinalIgnoreCase))
                return;
            Log.Info("Map font " + (string.IsNullOrEmpty(current) ? "(none)" : current) + " is not installed, using " + resolved);
            config.FontFamily = resolved;
            if (File.Exists(config.Path))
                TrySaveConfig();
        }

        #endregion

        #region Saving the config and presets

        private static bool IsEditableTheme(string name)
        {
            return !string.IsNullOrEmpty(name) && !AuroraDefaults.Presets.ContainsKey(name);
        }

        private bool DiffersFromTheme(string name)
        {
            if (string.IsNullOrEmpty(name))
                return true;
            Dictionary<string, string> saved = ReadPreset(name);
            if (saved.Count == 0)
                return true;
            foreach (KeyValuePair<string, string> pair in config.ToPreset())
            {
                string value;
                if (!saved.TryGetValue(pair.Key, out value))
                    continue;
                if (pair.Key == "MapDisplay.FontFamily")
                    value = ResolveFont(value);
                if (!string.Equals(Comparable(value), Comparable(pair.Value), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string Comparable(string value)
        {
            string text = value.Replace(" ", "");
            string[] parts = text.Split(',');
            return parts.Length == 4 && parts[3] == "255" ? string.Join(",", parts, 0, 3) : text;
        }

        private DateTime ConfigWriteTime()
        {
            try { return File.Exists(config.Path) ? File.GetLastWriteTimeUtc(config.Path) : DateTime.MinValue; }
            catch { return DateTime.MinValue; }
        }

        private void SaveConfig()
        {
            if (!backupMade && File.Exists(config.Path))
            {
                string backup = config.Path + ".bak";
                if (!File.Exists(backup))
                    File.Copy(config.Path, backup);
                backupMade = true;
            }
            config.Save();
            configWriteTime = ConfigWriteTime();
        }

        private bool TrySaveConfig()
        {
            try
            {
                SaveConfig();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Could not save " + config.Path + ": " + ex.Message);
                if (!saveFailureShown)
                {
                    saveFailureShown = true;
                    MessageBox.Show("Could not write " + config.Path + ":\n" + ex.Message, Program.AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }
        }

        private void ReloadIfChangedOnDisk()
        {
            if (!File.Exists(config.Path) || ConfigWriteTime() == configWriteTime)
                return;
            try
            {
                AuroraConfig fresh = AuroraConfig.Load(config.Path);
                config.ApplyPreset(fresh.ToPreset());
                config.MinimumSaveDays = fresh.MinimumSaveDays;
                configWriteTime = ConfigWriteTime();
                Log.Info("AuroraConfig.xml was changed outside Aurora Theme Manager; reloaded it");
                RefreshAllControls();
                preview.Invalidate();
            }
            catch (Exception ex)
            {
                Log.Error("Could not reload " + config.Path + ": " + ex.Message);
            }
        }

        private void OnConfigChanged()
        {
            if (suppressEvents)
                return;

            TrySaveConfig();
            ApplyWindowTheme();

            bool modified = selectedTheme != null && DiffersFromTheme(selectedTheme);
            if (modified != themeModified)
            {
                themeModified = modified;
                UpdateThemeDisplay();
            }

            preview.Invalidate();
        }

        private void RefreshPresetList()
        {
            bool previous = suppressEvents;
            suppressEvents = true;
            themeComboBox.Items.Clear();
            foreach (string name in PresetNames())
                themeComboBox.Items.Add(name);
            suppressEvents = previous;
        }

        private void RereadPresetList()
        {
            List<string> names = PresetNames();
            bool same = names.Count == themeComboBox.Items.Count;
            for (int i = 0; same && i < names.Count; i++)
            {
                string item = (string)themeComboBox.Items[i];
                same = item == names[i] || item == names[i] + ModifiedSuffix;
            }
            if (same)
                return;

            bool modified = themeModified;
            RefreshPresetList();
            SelectPreset(selectedTheme);
            if (selectedTheme != null)
            {
                themeModified = modified;
                UpdateThemeDisplay();
            }
            Log.Info("Theme list re-read: " + names.Count + " themes");
        }

        private void SelectPreset(string name)
        {
            bool previous = suppressEvents;
            suppressEvents = true;
            int index = IndexOfTheme(name);
            themeComboBox.SelectedIndex = index;
            selectedTheme = index >= 0 ? name : null;
            if (index < 0)
                themeModified = false;
            suppressEvents = previous;
            UpdateThemeDisplay();
        }

        private int IndexOfTheme(string name)
        {
            if (string.IsNullOrEmpty(name))
                return -1;
            for (int i = 0; i < themeComboBox.Items.Count; i++)
            {
                string item = (string)themeComboBox.Items[i];
                if (item == name || item == name + ModifiedSuffix)
                    return i;
            }
            return -1;
        }

        private void UpdateThemeDisplay()
        {
            bool previous = suppressEvents;
            suppressEvents = true;
            int selected = themeComboBox.SelectedIndex;
            for (int i = 0; i < themeComboBox.Items.Count; i++)
            {
                string item = (string)themeComboBox.Items[i];
                string plain = item.EndsWith(ModifiedSuffix, StringComparison.Ordinal) ? item.Substring(0, item.Length - ModifiedSuffix.Length) : item;
                string wanted = i == selected && themeModified ? plain + ModifiedSuffix : plain;
                if (item != wanted)
                    themeComboBox.Items[i] = wanted;
            }
            if (themeComboBox.SelectedIndex != selected)
                themeComboBox.SelectedIndex = selected;
            suppressEvents = previous;

            btnSaveTheme.Visible = themeModified && IsEditableTheme(selectedTheme);
            btnResetTheme.Visible = themeModified && selectedTheme != null;
            LayoutThemeButtons();
        }

        private void LayoutThemeButtons()
        {
            int x = themeComboBox.Right + 10;
            if (btnSaveTheme.Visible)
            {
                btnSaveTheme.Left = x;
                x = btnSaveTheme.Right + 8;
            }
            if (btnResetTheme.Visible)
            {
                btnResetTheme.Left = x;
                x = btnResetTheme.Right + 8;
            }
            btnSavePreset.Left = x;
            btnDeletePreset.Left = btnSavePreset.Right + 8;
            int pathLeft = btnDeletePreset.Right + 12;
            int pathWidth = btnPreview.Left - 6 - pathLeft;
            lblPath.Visible = pathWidth >= 40;
            if (!lblPath.Visible)
                return;
            lblPath.SetBounds(pathLeft, btnSavePreset.Top, pathWidth, btnSavePreset.Height);
            lblPath.Text = FitPath(config.Path, lblPath.Font, pathWidth - 4);
        }

        private static string FitPath(string path, Font font, int width)
        {
            if (TextRenderer.MeasureText(path, font).Width <= width)
                return path;
            string file = Path.GetFileName(path);
            string folder = path.Substring(0, path.Length - file.Length);
            for (int keep = folder.Length - 1; keep > 0; keep--)
            {
                string text = folder.Substring(0, keep) + "...\\" + file;
                if (TextRenderer.MeasureText(text, font).Width <= width)
                    return text;
            }
            return "...\\" + file;
        }

        private void ThemeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressEvents || themeComboBox.SelectedIndex < 0)
                return;

            string item = (string)themeComboBox.SelectedItem;
            string name = item.EndsWith(ModifiedSuffix, StringComparison.Ordinal) ? item.Substring(0, item.Length - ModifiedSuffix.Length) : item;

            ReloadIfChangedOnDisk();

            bool unsaved = selectedTheme != null
                ? DiffersFromTheme(selectedTheme)
                : !PresetNames().Any(n => !DiffersFromTheme(n));

            if (unsaved)
            {
                bool editable = IsEditableTheme(selectedTheme);
                string question = editable
                    ? "AuroraConfig.xml has changes that are not saved in the theme '" + selectedTheme + "'.\n\n"
                      + "Save them to '" + selectedTheme + "' before loading '" + name + "'?"
                    : "AuroraConfig.xml has changes that are not saved in any theme.\n\n"
                      + "Save them as a new theme before loading '" + name + "'?";
                question += "\n\nNo discards them and overwrites AuroraConfig.xml with '" + name + "'.";

                DialogResult answer = MessageBox.Show(this, question, "Unsaved Changes",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (answer == DialogResult.Cancel)
                {
                    SelectPreset(selectedTheme);
                    return;
                }
                if (answer == DialogResult.Yes && !(editable ? SaveSelectedTheme() : SaveThemeAs()))
                {
                    SelectPreset(selectedTheme);
                    return;
                }
            }

            LoadTheme(name);
        }

        private void LoadTheme(string name)
        {
            Dictionary<string, string> values = ReadPreset(name);
            if (values.Count == 0)
            {
                MessageBox.Show("The theme '" + name + "' could not be read.", Program.AppName);
                SelectPreset(selectedTheme);
                return;
            }

            int applied = config.ApplyPreset(values);
            if (!IsFontInstalled(config.FontFamily))
            {
                string resolved = ResolveFont(config.FontFamily);
                Log.Info("Theme font " + config.FontFamily + " is not installed, using " + resolved);
                config.FontFamily = resolved;
            }
            Log.Info("Loaded theme '" + name + "' (" + applied + " values)");

            themeModified = false;
            SelectPreset(name);
            RefreshAllControls();
            CurrentPreset = name;
            SaveSettings();
            TrySaveConfig();

            preview.Invalidate();
        }

        private bool SaveSelectedTheme()
        {
            if (!IsEditableTheme(selectedTheme))
                return false;
            try
            {
                WritePreset(selectedTheme, config);
                Log.Info("Updated theme '" + selectedTheme + "'");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save the theme '" + selectedTheme + "':\n" + ex.Message, Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            themeModified = false;
            UpdateThemeDisplay();
            return true;
        }

        private void BtnSaveTheme_Click(object sender, EventArgs e)
        {
            SaveSelectedTheme();
        }

        private void BtnResetTheme_Click(object sender, EventArgs e)
        {
            if (selectedTheme == null)
                return;
            if (MessageBox.Show(this, "Discard the unsaved changes and reload the theme '" + selectedTheme + "'?", "Reset Theme",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            LoadTheme(selectedTheme);
        }

        private void BtnSavePreset_Click(object sender, EventArgs e)
        {
            SaveThemeAs();
        }

        private bool SaveThemeAs()
        {
            using (var dialog = new Form())
            {
                dialog.Text = "Save Theme As";
                dialog.Size = new Size(350, 150);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                var label = new Label { Text = "Enter theme name:", Location = new Point(10, 20), AutoSize = true };
                string defaultName = IsEditableTheme(selectedTheme) ? selectedTheme : "";
                var textBox = new TextBox { Location = new Point(10, 45), Size = new Size(310, 25), Text = defaultName };
                var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, Location = new Point(160, 80), Size = new Size(75, 25) };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(245, 80), Size = new Size(75, 25) };
                dialog.Controls.AddRange(new Control[] { label, textBox, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;

                if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(textBox.Text))
                    return false;

                string name = textBox.Text.Trim();
                if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    MessageBox.Show("A theme name cannot contain these characters: " + new string(Path.GetInvalidFileNameChars().Where(c => c > 31).ToArray()),
                        "Invalid Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                if (AuroraDefaults.Presets.Keys.Any(k => k.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("'" + name + "' is a built-in theme name.", "Invalid Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                if (File.Exists(PresetPath(name)) &&
                    MessageBox.Show("Theme '" + name + "' already exists.  Overwrite it?", "Confirm Overwrite",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return false;

                try
                {
                    WritePreset(name, config);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Could not save the theme '" + name + "':\n" + ex.Message, Program.AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                Log.Info("Saved theme '" + name + "'");

                CurrentPreset = name;
                SaveSettings();
                themeModified = false;
                RefreshPresetList();
                SelectPreset(name);
                return true;
            }
        }

        private void BtnDeletePreset_Click(object sender, EventArgs e)
        {
            string name = selectedTheme;
            if (name == null || AuroraDefaults.Presets.ContainsKey(name))
            {
                MessageBox.Show("The built-in themes cannot be deleted.", Program.AppName);
                return;
            }

            if (MessageBox.Show("Delete theme '" + name + "'?", "Confirm Delete",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try { File.Delete(PresetPath(name)); } catch (Exception ex) { MessageBox.Show(ex.Message); return; }
            Log.Info("Deleted theme '" + name + "'");
            themeModified = false;
            CurrentPreset = "";
            SaveSettings();
            RefreshPresetList();
            SelectPreset(null);
        }

        #endregion

        #region Fonts

        private static readonly string[] StyleWords =
        {
            "regular", "bold", "italic", "oblique", "light", "semilight", "semibold", "demibold", "demi",
            "medium", "black", "thin", "extralight", "extrabold", "heavy", "book", "roman", "normal"
        };

        private static List<string> installedFamilies;

        private static List<string> InstalledFamilies()
        {
            if (installedFamilies == null)
                installedFamilies = ScanInstalledFamilies();
            return installedFamilies;
        }

        internal static bool IsFontInstalled(string name)
        {
            return !string.IsNullOrEmpty(name)
                && InstalledFamilies().Any(f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase));
        }

        internal static string ResolveFont(string wanted)
        {
            if (IsFontInstalled(wanted))
                return wanted;
            foreach (string name in AuroraDefaults.FontFallbacks)
                if (IsFontInstalled(name))
                    return name;

            string system = null;
            try { system = SystemFonts.MessageBoxFont.FontFamily.Name; } catch { }
            if (IsFontInstalled(system))
                return system;
            try { system = Control.DefaultFont.FontFamily.Name; } catch { }
            if (IsFontInstalled(system))
                return system;

            List<string> installed = InstalledFamilies();
            if (installed.Count > 0)
                return installed[0];
            return FontFamily.GenericSansSerif.Name;
        }

        private static List<string> ScanInstalledFamilies()
        {
            try
            {
                var names = RunningUnderWine() ? PrefixFontFamilies() : SystemFontFamilies();
                if (names.Count == 0)
                    names = SystemFontFamilies();

                var set = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
                var result = new List<string>();
                foreach (string name in set)
                {
                    if (!IsExcluded(name) && !IsStyleVariant(name, set) && IsUsable(name))
                        result.Add(name);
                }
                result.Sort(StringComparer.OrdinalIgnoreCase);
                return result;
            }
            catch
            {
                return new List<string>();
            }
        }

        private static List<string> SystemFontFamilies()
        {
            var names = new List<string>();
            using (var fonts = new InstalledFontCollection())
            {
                foreach (FontFamily family in fonts.Families)
                    AddFamily(names, family);
            }
            return names;
        }

        private static List<string> PrefixFontFamilies()
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string fontsDir = null;
            try { fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts); } catch { }
            if (string.IsNullOrEmpty(fontsDir))
                fontsDir = Path.Combine(Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\windows", "Fonts");

            try
            {
                if (Directory.Exists(fontsDir))
                {
                    foreach (string file in Directory.GetFiles(fontsDir))
                        files.Add(file);
                }
            }
            catch { }

            foreach (var hive in new[] { Microsoft.Win32.Registry.LocalMachine, Microsoft.Win32.Registry.CurrentUser })
            {
                try
                {
                    using (var key = hive.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\Fonts"))
                    {
                        if (key == null)
                            continue;
                        foreach (string valueName in key.GetValueNames())
                        {
                            string data = key.GetValue(valueName) as string;
                            if (string.IsNullOrEmpty(data))
                                continue;
                            string path = Path.IsPathRooted(data) ? data : Path.Combine(fontsDir, data);
                            if (File.Exists(path))
                                files.Add(path);
                        }
                    }
                }
                catch { }
            }

            var names = new List<string>();
            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".ttf" && ext != ".otf" && ext != ".ttc")
                    continue;
                try
                {
                    using (var collection = new PrivateFontCollection())
                    {
                        collection.AddFontFile(file);
                        foreach (FontFamily family in collection.Families)
                            AddFamily(names, family);
                    }
                }
                catch
                {
                }
            }
            return names;
        }

        private static bool IsExcluded(string name)
        {
            return AuroraDefaults.ExcludedFonts.Any(f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsUsable(string name)
        {
            try
            {
                using (var family = new FontFamily(name))
                using (var font = new Font(family, 10f))
                    return string.Equals(font.Name, name, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(font.FontFamily.Name, name, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void AddFamily(List<string> names, FontFamily family)
        {
            string name = family.Name;
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith("@", StringComparison.Ordinal))
                return;
            bool regular = false;
            try { regular = family.IsStyleAvailable(FontStyle.Regular); } catch { }
            if (regular)
                names.Add(name.Trim());
        }

        private static bool IsStyleVariant(string name, HashSet<string> all)
        {
            foreach (string other in all)
            {
                if (other.Length >= name.Length || !name.StartsWith(other + " ", StringComparison.OrdinalIgnoreCase))
                    continue;
                string rest = name.Substring(other.Length + 1).Trim();
                bool onlyStyles = rest.Length > 0;
                foreach (string word in rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (Array.IndexOf(StyleWords, word.ToLowerInvariant()) < 0)
                    {
                        onlyStyles = false;
                        break;
                    }
                }
                if (onlyStyles)
                    return true;
            }
            return false;
        }

        private static bool RunningUnderWine()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Wine"))
                    return key != null;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
