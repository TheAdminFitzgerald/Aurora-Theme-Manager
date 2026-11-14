using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AuroraThemeManager
{
    public partial class Settings
    {
        #region Controls

        private readonly Dictionary<string, Button> swatches = new Dictionary<string, Button>();
        private readonly Dictionary<string, string> colorCaptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, NumericUpDown> alphaBoxes = new Dictionary<string, NumericUpDown>();

        private readonly Dictionary<string, CheckBox> styleFlatBoxes = new Dictionary<string, CheckBox>();
        private readonly Dictionary<string, NumericUpDown> styleBorderBoxes = new Dictionary<string, NumericUpDown>();

        private readonly ThemePreview preview = new ThemePreview { TabStop = false };
        private PreviewForm previewForm;

        private void ShowPreview()
        {
            if (previewForm != null && !previewForm.IsDisposed)
            {
                if (previewForm.WindowState == FormWindowState.Minimized)
                    previewForm.WindowState = FormWindowState.Normal;
                previewForm.Activate();
                return;
            }

            if (previewForm == null)
                FormClosing += (s, e) =>
                {
                    if (previewForm != null && !previewForm.IsDisposed)
                        previewForm.Close();
                };
            previewForm = new PreviewForm(preview) { Size = new Size(Scaled(956, grpThemes.Width / (float)DesignThemesWidth), Height) };
            Rectangle screen = Screen.FromControl(this).WorkingArea;
            int x = Right + 8;
            if (x + previewForm.Width > screen.Right)
                x = Math.Max(screen.Left, screen.Right - previewForm.Width);
            previewForm.Location = new Point(x, Math.Max(screen.Top, Math.Min(Top, screen.Bottom - previewForm.Height)));
            previewForm.Show();
        }

        private static readonly Color PanelBack = Color.FromArgb(24, 24, 24);

        private readonly List<Control> dynamicControls = new List<Control>();

        #endregion

        #region Layout

        private void BuildDynamicUi()
        {
            bool exists = File.Exists(config.Path);
            lblPath.ForeColor = exists ? Color.Gray : Color.Salmon;
            tips.SetToolTip(lblPath, (exists ? "Editing " : "Not found yet, will be created at ") + config.Path);

            themeComboBox.SelectedIndexChanged += ThemeComboBox_SelectedIndexChanged;
            themeComboBox.DropDown += (s, e) => RereadPresetList();
            themeComboBox.Enter += (s, e) => RereadPresetList();
            btnSaveTheme.Click += BtnSaveTheme_Click;
            btnResetTheme.Click += BtnResetTheme_Click;
            tips.SetToolTip(btnResetTheme, "Discard the unsaved changes and reload the selected theme.");
            btnSavePreset.Click += BtnSavePreset_Click;
            btnDeletePreset.Click += BtnDeletePreset_Click;
            tips.SetToolTip(btnSaveTheme, "Overwrite the selected theme with the current settings.");

            preview.Config = config;
            preview.ColorClicked += Preview_ColorClicked;
            btnPreview.Click += (s, e) => ShowPreview();
            tips.SetToolTip(btnPreview, "Show the window, tactical map and galactic map in the current colors.");
            FormClosed += (s, e) => preview.Dispose();

            BindColorTabs();
            UpdateAlphaVisibility();
            InitIconsTab();
            InitSettingsColumn();
            ScaleDynamicControls();
            ApplyWindowTheme();

            Load += (s, e) => ApplyTabOrder(this);
        }

        #region Tab order

        private void ApplyTabOrder(Control container)
        {
            if (container is TabControl)
            {
                foreach (Control page in container.Controls)
                    ApplyTabOrder(page);
                return;
            }

            IEnumerable<Control> controls = container.Controls.Cast<Control>();
            List<Control> ordered = controls.Any(c => c is GroupBox) ? InColumnOrder(controls) : InReadingOrder(controls);

            int index = 0;
            foreach (Control c in ordered)
            {
                c.TabIndex = index++;
                if (c is GroupBox || c is TabPage || c is Panel)
                {
                    ApplyTabOrder(c);
                    continue;
                }
                c.TabStop = c is Button || c is CheckBox || c is NumericUpDown || c == cboFont || c is TabControl;
                if (c is TabControl)
                    ApplyTabOrder(c);
            }
        }

        private static List<Control> InReadingOrder(IEnumerable<Control> controls)
        {
            var result = new List<Control>();
            var row = new List<Control>();
            int rowTop = int.MinValue;
            foreach (Control c in controls.OrderBy(c => c.Top).ThenBy(c => c.Left))
            {
                if (row.Count > 0 && c.Top - rowTop > 4)
                {
                    result.AddRange(row.OrderBy(r => r.Left));
                    row.Clear();
                }
                if (row.Count == 0)
                    rowTop = c.Top;
                row.Add(c);
            }
            result.AddRange(row.OrderBy(r => r.Left));
            return result;
        }

        private static List<Control> InColumnOrder(IEnumerable<Control> controls)
        {
            var result = new List<Control>();
            var column = new List<Control>();
            int columnLeft = int.MinValue;
            foreach (Control c in controls.OrderBy(c => c.Left).ThenBy(c => c.Top))
            {
                if (column.Count > 0 && c.Left - columnLeft > 4)
                {
                    result.AddRange(column.OrderBy(k => k.Top));
                    column.Clear();
                }
                if (column.Count == 0)
                    columnLeft = c.Left;
                column.Add(c);
            }
            result.AddRange(column.OrderBy(k => k.Top));
            return result;
        }

        #endregion

        private const int DesignThemesWidth = 842;
        private const int DesignThemesHeight = 58;

        private void ScaleDynamicControls()
        {
            float widthScale = grpThemes.Width / (float)DesignThemesWidth;
            float heightScale = grpThemes.Height / (float)DesignThemesHeight;
            if (Math.Abs(widthScale - 1f) < 0.01f && Math.Abs(heightScale - 1f) < 0.01f)
                return;
            var factor = new SizeF(widthScale, heightScale);
            foreach (Control control in dynamicControls)
                control.Scale(factor);
        }

        private static GroupBox NewGroup(string text, int x, int y, int w, int h)
        {
            return new GroupBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                ForeColor = Color.White,
                BackColor = Color.Black
            };
        }

        private static Button NewButton(string text, int x, int y, int w)
        {
            return new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(48, 48, 48),
                ForeColor = Color.White,
                UseVisualStyleBackColor = false
            };
        }

        private static CheckBox NewCheck(string text, int x, int y, int w)
        {
            return new CheckBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, 20),
                ForeColor = Color.White
            };
        }

        private static Label NewLabel(string text, int x, int y, int w, int h = 20)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White
            };
        }

        #endregion

        #region Color tabs

        private static readonly string[] RedundantPrefixes = { "Tactical Map ", "Galactic Map ", "Org Chart " };

        private static string StripPrefix(string text)
        {
            foreach (string prefix in RedundantPrefixes)
                if (text.StartsWith(prefix, StringComparison.Ordinal) && text.Length > prefix.Length)
                    return text.Substring(prefix.Length);
            return text;
        }

        private const int LabelWidth = 142;
        private const int SwatchWidth = 60;
        private const int AlphaWidth = 46;
        private const int GroupWidth = 272;
        private const int RowHeight = 24;
        private const int ColumnGap = 6;
        private const int TabMargin = 3;

        private const int FirstRowTop = 18;
        private const int RowsBottomPad = 10;
        private const int OuterBottomPad = 6;

        private static int GroupHeight(int rows)
        {
            return FirstRowTop + rows * RowHeight + RowsBottomPad;
        }

        private static int Scaled(int value, float factor)
        {
            return (int)Math.Round(value * factor);
        }

        private TabPage[] ColorTabs
        {
            get { return new[] { tabGeneral, tabMaps }; }
        }

        private readonly HashSet<Control> hiddenColorControls = new HashSet<Control>();

        private readonly List<GroupBox> unplacedGroups = new List<GroupBox>();

        private void HideColorControl(Control control)
        {
            control.Visible = false;
            hiddenColorControls.Add(control);
        }

        private void BindColorTabs()
        {
            var bound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TabPage tab in ColorTabs)
                BindColorControls(tab, bound);
            AddMissingAlphaBoxes();

            Load += (s, e) =>
            {
                LayoutColorTabs();
                LayoutThemeButtons();
            };

            var unplaced = config.Groups()
                .Select(g => new KeyValuePair<string, List<AuroraConfig.ColorEntry>>(g.Key, g.Value.Where(e => !bound.Contains(e.Name)).ToList()))
                .Where(g => g.Value.Count > 0)
                .ToList();
            if (unplaced.Count == 0)
                return;

            foreach (var group in unplaced)
            {
                var box = NewGroup(StripPrefix(group.Key), 0, 0, GroupWidth, GroupHeight(group.Value.Count));
                box.Font = Font;
                tips.SetToolTip(box, group.Key);
                for (int i = 0; i < group.Value.Count; i++)
                    AddColorRow(box, group.Value[i], FirstRowTop + i * RowHeight);
                tabGeneral.Controls.Add(box);
                dynamicControls.Add(box);
                unplacedGroups.Add(box);
            }
            Log.Info("Settings: " + unplaced.Sum(g => g.Value.Count) + " colors in AuroraConfig.xml have no place on the color tabs; shown on Game Windows");
        }

        private void BindColorControls(Control parent, HashSet<string> bound)
        {
            foreach (Control control in parent.Controls)
            {
                string name = control.Tag as string;

                var group = control as GroupBox;
                if (group != null)
                {
                    if (name != null)
                        tips.SetToolTip(group, name);
                    int before = bound.Count;
                    BindColorControls(group, bound);
                    if (name != null && bound.Count == before)
                        HideColorControl(group);
                    continue;
                }

                if (name == null)
                    continue;

                AuroraConfig.ColorEntry entry = config.Find(name);
                Control label = parent.Controls["lbl" + name];

                var swatch = control as Button;
                if (swatch != null)
                {
                    if (entry == null)
                    {
                        HideColorControl(swatch);
                        if (label != null) HideColorControl(label);
                        continue;
                    }
                    swatch.Tag = entry;
                    swatch.Click += Swatch_Click;
                    swatches[entry.Name] = swatch;
                    string tip = entry.AbsentDefault.HasValue
                        ? entry.Name + "\nNot in this AuroraConfig.xml yet; added by a later Aurora (3.1.0).  Setting it writes it to the file."
                        : entry.Name;
                    tips.SetToolTip(swatch, tip);
                    if (label != null)
                    {
                        tips.SetToolTip(label, tip);
                        colorCaptions[entry.Name] = label.Text;
                    }
                    bound.Add(entry.Name);
                    if (entry.Name != NoAlphaSelector && parent.Controls["alpha" + name] == null)
                        missingAlpha.Add(new KeyValuePair<Control, Button>(parent, swatch));
                    continue;
                }

                var alpha = control as NumericUpDown;
                if (alpha != null)
                {
                    if (entry == null)
                    {
                        HideColorControl(alpha);
                        continue;
                    }
                    BindAlpha(alpha, entry);
                }
            }
        }

        private void AddColorRow(GroupBox box, AuroraConfig.ColorEntry entry, int y)
        {
            var lbl = NewLabel(StripPrefix(entry.Label) + ":", 6, y, LabelWidth);
            tips.SetToolTip(lbl, entry.Name);

            int x = 6 + LabelWidth + 4;
            var swatch = new Button
            {
                Location = new Point(x, y),
                Size = new Size(SwatchWidth, 21),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Tag = entry,
                Font = new Font("Consolas", 7f)
            };
            swatch.Click += Swatch_Click;
            tips.SetToolTip(swatch, entry.Name);
            swatches[entry.Name] = swatch;
            colorCaptions[entry.Name] = lbl.Text;
            box.Controls.Add(lbl);
            box.Controls.Add(swatch);

            if (entry.Name != NoAlphaSelector)
                box.Controls.Add(NewAlphaBox(entry, new Point(x + SwatchWidth + 4, y)));
        }

        private NumericUpDown NewAlphaBox(AuroraConfig.ColorEntry entry, Point location)
        {
            var alpha = new NumericUpDown
            {
                Name = "alpha" + entry.Name,
                Location = location,
                Size = new Size(AlphaWidth, 20),
                Minimum = 0,
                Maximum = 255,
                BackColor = Color.FromArgb(32, 32, 32),
                ForeColor = Color.White
            };
            BindAlpha(alpha, entry);
            return alpha;
        }

        private void BindAlpha(NumericUpDown alpha, AuroraConfig.ColorEntry entry)
        {
            alpha.Tag = entry;
            alpha.ValueChanged += Alpha_ValueChanged;
            alphaBoxes[entry.Name] = alpha;
            tips.SetToolTip(alpha, entry.Name + " transparency (0 fully transparent, 255 solid)");
            alpha.Enter -= SelectAllOnEnter;
            alpha.Enter += SelectAllOnEnter;
        }

        private void SelectAllOnEnter(object sender, EventArgs e)
        {
            var box = (NumericUpDown)sender;
            BeginInvoke((Action)(() =>
            {
                if (!box.IsDisposed && box.ContainsFocus)
                    box.Select(0, box.Text.Length);
            }));
        }

        private readonly List<KeyValuePair<Control, Button>> missingAlpha = new List<KeyValuePair<Control, Button>>();

        private void AddMissingAlphaBoxes()
        {
            foreach (KeyValuePair<Control, Button> pair in missingAlpha)
            {
                var entry = (AuroraConfig.ColorEntry)pair.Value.Tag;
                Button swatch = pair.Value;
                var alpha = NewAlphaBox(entry, new Point(swatch.Right + 4, swatch.Top));
                alpha.Visible = false;
                if (hiddenColorControls.Contains(swatch))
                    hiddenColorControls.Add(alpha);
                pair.Key.Controls.Add(alpha);
                dynamicControls.Add(alpha);
            }
            missingAlpha.Clear();
        }

        private void Swatch_Click(object sender, EventArgs e)
        {
            EditColor((AuroraConfig.ColorEntry)((Button)sender).Tag);
        }

        private void Preview_ColorClicked(string name)
        {
            AuroraConfig.ColorEntry entry = config.Find(name);
            if (entry != null)
                EditColor(entry, previewForm != null && !previewForm.IsDisposed && previewForm.Visible ? previewForm : (IWin32Window)this);
        }

        private void EditColor(AuroraConfig.ColorEntry entry)
        {
            EditColor(entry, this);
        }

        private void EditColor(AuroraConfig.ColorEntry entry, IWin32Window owner)
        {
            using (var dialog = new ColorPicker())
            {
                dialog.Title = ColorCaption(entry);
                dialog.Color = entry.Value;
                dialog.FullOpen = true;
                dialog.AnyColor = true;
                if (dialog.ShowDialog(owner) != DialogResult.OK)
                    return;

                if (dialog.Color.R == entry.Value.R && dialog.Color.G == entry.Value.G && dialog.Color.B == entry.Value.B)
                    return;

                entry.Value = Color.FromArgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
            }

            Button swatch;
            if (swatches.TryGetValue(entry.Name, out swatch))
                PaintSwatch(swatch, entry);
            OnConfigChanged();
        }

        private string ColorCaption(AuroraConfig.ColorEntry entry)
        {
            string caption;
            if (!colorCaptions.TryGetValue(entry.Name, out caption) || string.IsNullOrWhiteSpace(caption))
                caption = StripPrefix(entry.Label);
            return caption.Trim().TrimEnd(':').Trim();
        }

        private void Alpha_ValueChanged(object sender, EventArgs e)
        {
            if (suppressEvents) return;
            var box = (NumericUpDown)sender;
            var entry = (AuroraConfig.ColorEntry)box.Tag;
            entry.Alpha = (int)box.Value;
            OnConfigChanged();
        }

        private static void PaintSwatch(Button button, AuroraConfig.ColorEntry entry)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = entry.Value;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.White;
            button.FlatAppearance.MouseOverBackColor = entry.Value;
            button.FlatAppearance.MouseDownBackColor = entry.Value;
            button.Text = string.Empty;
        }

        #endregion

        #region Color tab layout

        private void LayoutColorTabs()
        {
            float heightScale = grpThemes.Height / (float)DesignThemesHeight;
            int bottom = tabMain.DisplayRectangle.Height - Scaled(TabMargin, heightScale);
            foreach (TabPage tab in ColorTabs)
                StretchRows(tab, bottom, tab == tabGeneral ? unplacedGroups : null, heightScale);
        }

        private List<List<GroupBox>> GroupColumns(IEnumerable<GroupBox> boxes, float heightScale)
        {
            int tolerance = Scaled(ColumnGap, heightScale);
            var columns = new List<List<GroupBox>>();
            foreach (GroupBox box in boxes.OrderBy(b => b.Left).ThenBy(b => b.Top))
            {
                List<GroupBox> column = columns.FirstOrDefault(c => Math.Abs(c[0].Left - box.Left) <= tolerance);
                if (column == null)
                    columns.Add(column = new List<GroupBox>());
                column.Add(box);
            }
            return columns;
        }

        private void StretchRows(Control container, int bottom, List<GroupBox> extra, float heightScale)
        {
            int gap = Scaled(ColumnGap, heightScale);
            List<GroupBox> designed = container.Controls.OfType<GroupBox>()
                .Where(b => extra == null || !extra.Contains(b))
                .ToList();

            foreach (GroupBox box in designed)
                if (!IsOuterBox(box))
                    CloseHiddenRows(box, heightScale);

            List<GroupBox> visible = designed.Where(b => !hiddenColorControls.Contains(b)).ToList();
            foreach (GroupBox hidden in designed.Where(b => hiddenColorControls.Contains(b)))
            {
                List<GroupBox> column = visible.Where(b => Math.Abs(b.Left - hidden.Left) <= gap).ToList();
                GroupBox below = column.Where(b => b.Top > hidden.Top).OrderBy(b => b.Top).FirstOrDefault();
                GroupBox above = column.Where(b => b.Top < hidden.Top).OrderByDescending(b => b.Top).FirstOrDefault();
                if (below != null)
                    below.SetBounds(below.Left, hidden.Top, below.Width, below.Bottom - hidden.Top);
                else if (above != null)
                    above.Height = hidden.Bottom - above.Top;
            }
            if (visible.Count == 0)
                return;

            if (extra != null)
                foreach (GroupBox box in extra)
                {
                    List<GroupBox> shortest = GroupColumns(visible, heightScale).OrderBy(c => c.Max(b => b.Bottom)).First();
                    box.SetBounds(shortest[0].Left, shortest.Max(b => b.Bottom) + gap, shortest[0].Width, box.Height);
                    visible.Add(box);
                }

            List<int> rows = visible.Select(b => b.Top).Distinct().OrderBy(t => t).ToList();
            int spare = Math.Max(0, bottom - visible.Max(b => b.Bottom));
            int each = spare / rows.Count;
            int rest = spare % rows.Count;
            var offset = new int[rows.Count + 1];
            for (int i = 0; i < rows.Count; i++)
                offset[i + 1] = offset[i] + each + (i < rest ? 1 : 0);

            foreach (GroupBox box in visible)
            {
                int first = rows.IndexOf(box.Top);
                int last = rows.FindLastIndex(t => t < box.Bottom);
                if (first < 0 || last < first)
                    continue;
                int top = box.Top + offset[first];
                int height = box.Bottom + offset[last + 1] - top;
                box.SetBounds(box.Left, top, box.Width, height);
            }

            foreach (List<GroupBox> column in GroupColumns(visible, heightScale))
            {
                GroupBox last = column.OrderBy(b => b.Bottom).Last();
                if (last.Bottom < bottom)
                    last.Height = bottom - last.Top;
            }

            foreach (GroupBox box in visible.Where(IsOuterBox))
                StretchRows(box, box.Height - Scaled(OuterBottomPad, heightScale), null, heightScale);
        }

        private static bool IsOuterBox(GroupBox box)
        {
            return box.Controls.OfType<GroupBox>().Any();
        }

        private void CloseHiddenRows(GroupBox box, float heightScale)
        {
            int pitch = Scaled(RowHeight, heightScale);
            var rows = new List<List<Control>>();
            foreach (Control c in box.Controls.Cast<Control>().OrderBy(c => c.Top))
            {
                if (rows.Count == 0 || c.Top - rows[rows.Count - 1][0].Top >= pitch / 2)
                    rows.Add(new List<Control>());
                rows[rows.Count - 1].Add(c);
            }
            if (rows.Count == 0 || !rows.Any(r => r.All(c => hiddenColorControls.Contains(c))))
                return;

            int y = rows[0][0].Top;
            foreach (List<Control> row in rows)
            {
                if (row.All(c => hiddenColorControls.Contains(c)))
                    continue;
                int top = row[0].Top;
                foreach (Control c in row)
                    c.Top = y + (c.Top - top);
                y += pitch;
            }
        }

        #endregion

        #region Settings column

        private const string FontTip = "The font Aurora uses on the tactical and galactic maps.";
        private const string EventLogTip = "Events from this many recent days are always saved.  Older events are saved, newest first, "
                                         + "up to a total of 10,000.\nThis is a game setting and is not stored in themes.";

        private const string NoAlphaSelector = "Background";

        private void UpdateAlphaVisibility()
        {
            foreach (KeyValuePair<string, NumericUpDown> pair in alphaBoxes)
            {
                Button swatch;
                bool rowShown = !swatches.TryGetValue(pair.Key, out swatch) || !hiddenColorControls.Contains(swatch);
                pair.Value.Visible = rowShown && ShowTransparencySelectors;
            }
        }

        private void InitAlphaOption()
        {
            chkTransparency.Checked = ShowTransparencySelectors;
            chkTransparency.CheckedChanged += (s, e) =>
            {
                ShowTransparencySelectors = chkTransparency.Checked;
                SaveSettings();
                UpdateAlphaVisibility();
            };
        }

        private void InitSettingsColumn()
        {
            InitAlphaOption();

            BindButtonStyle("ButtonStyle", lblButtonsRow, chkButtonFlat, lblButtonBorder, numButtonBorder);
            BindButtonStyle("ToolbarButtonStyle", lblToolbarRow, chkToolbarFlat, lblToolbarBorder, numToolbarBorder);

            cboFont.SelectedIndexChanged += CboFont_SelectedIndexChanged;

            numMinimumSaveDays.ValueChanged += (s, e) =>
            {
                if (suppressEvents) return;
                config.MinimumSaveDays = (int)numMinimumSaveDays.Value;
                OnConfigChanged();
            };
            foreach (Control c in new Control[] { grpEventLog, lblDays, numMinimumSaveDays })
                tips.SetToolTip(c, EventLogTip);

            if (config.UnhandledElements.Count > 0)
                Log.Info("Left unchanged in AuroraConfig.xml, no setting for: " + string.Join(", ", config.UnhandledElements.ToArray()));
        }

        private static bool IsFlat(AuroraConfig.ButtonStyleEntry style)
        {
            return string.Equals(style.FlatStyle, "Flat", StringComparison.OrdinalIgnoreCase);
        }

        private void BindButtonStyle(string element, Label name, CheckBox flat, Label caption, NumericUpDown border)
        {
            AuroraConfig.ButtonStyleEntry style = config.FindStyle(element);
            if (style == null)
            {
                foreach (Control c in new Control[] { name, flat, caption, border })
                    c.Enabled = false;
                tips.SetToolTip(name, element + " is not in AuroraConfig.xml.");
                return;
            }

            tips.SetToolTip(name, style.ElementName);
            flat.Tag = style;
            border.Tag = style;
            tips.SetToolTip(flat, "Ticked: Flat.  Unticked: Standard.");
            tips.SetToolTip(border, "Border size, used by flat buttons only.");
            flat.CheckedChanged += (s, e) =>
            {
                border.Enabled = flat.Checked;
                if (suppressEvents) return;
                style.FlatStyle = flat.Checked ? "Flat" : "Standard";
                OnConfigChanged();
            };
            border.ValueChanged += (s, e) =>
            {
                if (suppressEvents) return;
                style.BorderSize = (int)border.Value;
                OnConfigChanged();
            };
            styleFlatBoxes[element] = flat;
            styleBorderBoxes[element] = border;
        }

        private void PopulateFontList()
        {
            bool previous = suppressEvents;
            suppressEvents = true;

            List<string> installed = InstalledFamilies();
            cboFont.Items.Clear();

            string current = config.FontFamily;
            bool known = installed.Any(f => string.Equals(f, current, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(current) && !known)
                cboFont.Items.Add(current + " (not installed)");
            foreach (string family in installed)
                cboFont.Items.Add(family);

            int index = -1;
            for (int i = 0; i < cboFont.Items.Count; i++)
            {
                string item = (string)cboFont.Items[i];
                if (string.Equals(item, current, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item, current + " (not installed)", StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }
            cboFont.SelectedIndex = index;

            bool missing = !known && !string.IsNullOrEmpty(current);
            string tip = FontTip;
            if (missing)
                tip = "The font named in AuroraConfig.xml, " + current + ", is not installed, so Aurora will fall back to\nanother font.  Pick an installed one.\n\n" + tip;
            lblFontNote.ForeColor = missing ? Color.Salmon : ThemeText;
            foreach (Control c in new Control[] { lblFontNote, cboFont, lblFontSample, lblFontNote.Parent })
                if (c != null) tips.SetToolTip(c, tip);

            UpdateFontSample();
            suppressEvents = previous;
        }

        private void CboFont_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressEvents || cboFont.SelectedItem == null) return;

            string text = (string)cboFont.SelectedItem;
            if (text.EndsWith(" (not installed)", StringComparison.Ordinal))
                return;

            config.FontFamily = text;
            UpdateFontSample();
            OnConfigChanged();
        }

        private void UpdateFontSample()
        {
            try
            {
                Font old = lblFontSample.Font;
                lblFontSample.Font = new Font(IsFontInstalled(config.FontFamily) ? config.FontFamily : ResolveFont(config.FontFamily), 10f);
                if (old != null && old != Font) old.Dispose();
            }
            catch
            {
            }
        }

        #endregion

        #region Refreshing controls from the config

        private void RefreshAllControls()
        {
            bool previous = suppressEvents;
            suppressEvents = true;

            foreach (AuroraConfig.ColorEntry entry in config.Colors)
            {
                Button swatch;
                if (swatches.TryGetValue(entry.Name, out swatch))
                    PaintSwatch(swatch, entry);

                NumericUpDown alpha;
                if (alphaBoxes.TryGetValue(entry.Name, out alpha))
                    alpha.Value = Math.Max(0, Math.Min(255, entry.Alpha));
            }

            foreach (AuroraConfig.ButtonStyleEntry style in config.ButtonStyles)
            {
                CheckBox flat;
                if (styleFlatBoxes.TryGetValue(style.ElementName, out flat))
                {
                    flat.Checked = IsFlat(style);
                    NumericUpDown borderBox;
                    if (styleBorderBoxes.TryGetValue(style.ElementName, out borderBox))
                        borderBox.Enabled = flat.Checked;
                }
                NumericUpDown border;
                if (styleBorderBoxes.TryGetValue(style.ElementName, out border))
                    border.Value = Math.Max(border.Minimum, Math.Min(border.Maximum, style.BorderSize));
            }

            numMinimumSaveDays.Value = Math.Max(numMinimumSaveDays.Minimum, Math.Min(numMinimumSaveDays.Maximum, config.MinimumSaveDays));

            suppressEvents = previous;

            PopulateFontList();
            ApplyWindowTheme();
            preview.Invalidate();
        }

        #endregion
    }
}
