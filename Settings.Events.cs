using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AuroraThemeManager
{
    public partial class Settings
    {
        #region Event colours tab

        private readonly EventColourSet eventColours = new EventColourSet();
        private List<EventType> eventTypes = new List<EventType>();
        private bool eventTypesFromDatabase;
        private EventColour copiedEventColour;

        private void InitEventsTab()
        {
            btnEventsScratch.Click += (s, e) => StartEventsFromScratch(true);
            btnEventsImport.Click += (s, e) => ImportEventColours();
            btnEventsExport.Click += (s, e) => ExportEventColours();
            btnEventsForeground.Click += (s, e) => EditSelectedEventColour(true);
            btnEventsBackground.Click += (s, e) => EditSelectedEventColour(false);
            eventGrid.SelectionChanged += UpdateEventButtons;
            eventGrid.CellMenu += ShowEventMenu;
            eventGrid.HeadingMenu += ShowEventHeadingMenu;

            tips.SetToolTip(btnEventsScratch, "Give every event the theme's text colour on its background colour.");
            tips.SetToolTip(btnEventsImport, "Load event colours from a CSV exported by Aurora's Events window (or by ATM).");
            tips.SetToolTip(btnEventsExport, "Save the event colours as a CSV to import with Import Colours in Aurora's Events window.");
            tips.SetToolTip(btnEventsForeground, "Change the text colour of the selected events.\nClick an event to select it, Ctrl+Click to add or remove one, Shift+Click for a range.");
            tips.SetToolTip(btnEventsBackground, "Change the background colour of the selected events.\nClick an event to select it, Ctrl+Click to add or remove one, Shift+Click for a range.");

#if DEBUG
            Shown += (s, e) => DebugScreenshot();
#endif
            eventTypes = EventColourSet.LoadTypes(out eventTypesFromDatabase);
            LoadWorkingEventColours();
            ShowEvents();
        }

        // Starts from the theme colours the first time.
        private void LoadWorkingEventColours()
        {
            string path = EventColourSet.WorkingPath;
            try
            {
                if (File.Exists(path))
                {
                    int skipped;
                    EventColourSet read = EventColourSet.Read(path, out skipped);
                    CopyEventColours(read);
                    FillMissingEventColours();
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Events: could not read " + path + ": " + ex.Message);
            }
            StartEventsFromScratch(false);
        }

        // Keeps unlisted types too, so an export round-trips everything imported.
        private void CopyEventColours(EventColourSet from)
        {
            eventColours.Clear();
            foreach (int id in from.Ids.ToList())
            {
                EventColour c = from.Get(id);
                eventColours.Set(id, c.Text, c.Back);
            }
        }

        // Types with no colour get the theme's colours.
        private int FillMissingEventColours()
        {
            int filled = 0;
            foreach (EventType type in eventTypes)
            {
                if (eventColours.Get(type.Id) != null)
                    continue;
                eventColours.Set(type.Id, EventDefaultText, EventDefaultBack);
                filled++;
            }
            return filled;
        }

        private Color EventDefaultText { get { return ThemeText; } }

        private Color EventDefaultBack { get { return config.ColorOr("Background", DesignBackground); } }

        private void SaveWorkingEventColours()
        {
            try
            {
                eventColours.Write(EventColourSet.WorkingPath);
            }
            catch (Exception ex)
            {
                Log.Error("Events: could not save " + EventColourSet.WorkingPath + ": " + ex.Message);
            }
        }

        private void ShowEvents()
        {
            eventGrid.SetItems(eventTypes, id => eventColours.Get(id));
            UpdateEventButtons();
        }

        private void UpdateEventButtons()
        {
            int count = eventGrid.SelectedTypes.Count;
            btnEventsForeground.Enabled = count > 0;
            btnEventsBackground.Enabled = count > 0;
        }

        private void EventColoursChanged()
        {
            SaveWorkingEventColours();
            eventGrid.RefreshColours();
        }

        private void StartEventsFromScratch(bool ask)
        {
            if (ask && MessageBox.Show(this,
                    "Give every event the theme's text colour on the theme's background colour?\n\nThe current event colours are lost unless you export them first.",
                    "Start From Scratch", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return;
            eventColours.Fill(eventTypes, EventDefaultText, EventDefaultBack);
            EventColoursChanged();
        }

        private void ImportEventColours()
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Import Event Colours",
                Filter = "CSV file (*.csv)|*.csv|All files (*.*)|*.*",
                InitialDirectory = AuroraConfig.GameDirectory(),
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    int skipped;
                    EventColourSet read = EventColourSet.Read(dialog.FileName, out skipped);
                    int known = eventTypes.Count(t => read.Get(t.Id) != null);
                    if (known == 0)
                    {
                        MessageBox.Show(this, "No event colours were found in " + Path.GetFileName(dialog.FileName)
                            + ".\n\nEach line should be: event type ID, background colour, text colour.",
                            "Import Event Colours", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    CopyEventColours(read);
                    int filled = FillMissingEventColours();
                    EventColoursChanged();
                    Log.Info("Events: imported " + known + " event colours from " + dialog.FileName
                        + (filled > 0 ? "; " + filled + " not in the file use the theme's colours" : "")
                        + (skipped > 0 ? "; skipped " + skipped + " unreadable lines" : ""));
                    if (filled > 0 || skipped > 0)
                    {
                        string note = "Imported " + known + " event colours.";
                        if (filled > 0)
                            note += "\n\n" + filled + " event types were not in the file and use the theme's colours.";
                        if (skipped > 0)
                            note += "\n\n" + skipped + " lines could not be read and were skipped.";
                        MessageBox.Show(this, note, "Import Event Colours", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Events: could not import " + dialog.FileName + ": " + ex.Message);
                    MessageBox.Show(this, "Could not read " + dialog.FileName + ":\n" + ex.Message,
                        "Import Event Colours", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportEventColours()
        {
            using (var dialog = new SaveFileDialog
            {
                Title = "Export Event Colours",
                Filter = "CSV file (*.csv)|*.csv|All files (*.*)|*.*",
                InitialDirectory = AuroraConfig.GameDirectory(),
                FileName = "Event Colours.csv",
                OverwritePrompt = true,
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    eventColours.Write(dialog.FileName);
                    Log.Info("Events: exported " + eventColours.Count + " event colours to " + dialog.FileName);
                    MessageBox.Show(this, "Saved " + eventColours.Count + " event colours to " + dialog.FileName + ".",
                        "Export Event Colours", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Log.Error("Events: could not export " + dialog.FileName + ": " + ex.Message);
                    MessageBox.Show(this, "Could not save " + dialog.FileName + ":\n" + ex.Message,
                        "Export Event Colours", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private Color? PickEventColour(string title, Color start)
        {
            using (var dialog = new ColorPicker())
            {
                dialog.Title = title;
                dialog.Color = start;
                dialog.FullOpen = true;
                dialog.AnyColor = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return null;
                return Color.FromArgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
            }
        }

        /// <summary>Sets the foreground or background of every selected event.</summary>
        private void EditSelectedEventColour(bool text)
        {
            List<EventType> types = eventGrid.SelectedTypes;
            if (types.Count == 0)
                return;
            EventColour first = eventColours.Get(types[0].Id) ?? new EventColour { Text = EventDefaultText, Back = EventDefaultBack };
            string what = types.Count == 1 ? types[0].Name : types.Count + " events";
            Color? picked = PickEventColour(what + (text ? ": foreground" : ": background"), text ? first.Text : first.Back);
            if (picked == null)
                return;
            foreach (EventType type in types)
            {
                EventColour current = eventColours.Get(type.Id) ?? new EventColour { Text = EventDefaultText, Back = EventDefaultBack };
                eventColours.Set(type.Id, text ? picked.Value : current.Text, text ? current.Back : picked.Value);
            }
            EventColoursChanged();
        }

        private void ShowEventMenu(EventType type, Point at)
        {
            var menu = ThemedMenu();
            menu.Items.Add("Copy Colours", null, (s, e) =>
            {
                EventColour c = eventColours.Get(type.Id);
                if (c != null)
                    copiedEventColour = new EventColour { Text = c.Text, Back = c.Back };
            });
            List<EventType> targets = eventGrid.SelectedTypes;
            string onto = targets.Count > 1 ? " to " + targets.Count + " Selected Events" : "";
            ToolStripItem paste = menu.Items.Add("Paste Colours" + onto, null, (s, e) =>
            {
                foreach (EventType t in targets)
                    eventColours.Set(t.Id, copiedEventColour.Text, copiedEventColour.Back);
                EventColoursChanged();
            });
            paste.Enabled = copiedEventColour != null;
            ToolStripItem pasteCategory = menu.Items.Add("Paste Colours to All " + type.Category.Name, null, (s, e) =>
            {
                foreach (EventType t in eventTypes.Where(t => t.Category == type.Category))
                    eventColours.Set(t.Id, copiedEventColour.Text, copiedEventColour.Back);
                EventColoursChanged();
            });
            pasteCategory.Enabled = copiedEventColour != null;
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Reset" + (targets.Count > 1 ? " " + targets.Count + " Selected Events" : "") + " to Theme Colours", null, (s, e) =>
            {
                foreach (EventType t in targets)
                    eventColours.Set(t.Id, EventDefaultText, EventDefaultBack);
                EventColoursChanged();
            });
            menu.Closed += (s, e) => BeginInvoke((Action)menu.Dispose);
            menu.Show(eventGrid, at);
        }

        private void ShowEventHeadingMenu(EventCategory category, List<EventType> types, Point at)
        {
            var menu = ThemedMenu();
            menu.Items.Add("Select All " + category.Name, null, (s, e) => eventGrid.Select(types, false));
            menu.Items.Add("Add " + category.Name + " to Selection", null, (s, e) => eventGrid.Select(types, true));
            menu.Items.Add(new ToolStripSeparator());
            ToolStripItem paste = menu.Items.Add("Paste Colours to All " + category.Name, null, (s, e) =>
            {
                foreach (EventType t in eventTypes.Where(t => t.Category == category))
                    eventColours.Set(t.Id, copiedEventColour.Text, copiedEventColour.Back);
                EventColoursChanged();
            });
            paste.Enabled = copiedEventColour != null;
            menu.Items.Add("Reset All " + category.Name + " to Theme Colours", null, (s, e) =>
            {
                foreach (EventType t in eventTypes.Where(t => t.Category == category))
                    eventColours.Set(t.Id, EventDefaultText, EventDefaultBack);
                EventColoursChanged();
            });
            menu.Closed += (s, e) => BeginInvoke((Action)menu.Dispose);
            menu.Show(eventGrid, at);
        }

#if DEBUG
        /// <summary>Debug only: ATM_SHOT_TAB opens a tab; ATM_SHOT saves the window as a PNG and exits.</summary>
        private void DebugScreenshot()
        {
            string tabName = Environment.GetEnvironmentVariable("ATM_SHOT_TAB");
            string shot = Environment.GetEnvironmentVariable("ATM_SHOT");
            if (!string.IsNullOrEmpty(tabName))
            {
                foreach (TabPage page in tabMain.TabPages)
                    if (page.Text.Equals(tabName, StringComparison.OrdinalIgnoreCase))
                        tabMain.SelectedTab = page;
            }
            if (string.IsNullOrEmpty(shot))
                return;
            var timer = new Timer { Interval = 1500 };
            timer.Tick += (s, e) =>
            {
                timer.Dispose();
                using (var bitmap = new Bitmap(Width, Height))
                {
                    DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size));
                    bitmap.Save(shot, System.Drawing.Imaging.ImageFormat.Png);
                }
                Close();
            };
            timer.Start();
        }
#endif

        #endregion
    }
}
