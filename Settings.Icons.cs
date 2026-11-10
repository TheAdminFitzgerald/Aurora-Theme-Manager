using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;

namespace AuroraThemeManager
{
    public partial class Settings
    {
        #region Icons tab state

        private ImageList auroraIconImages;

        private const int AuroraIconSize = 48;

        private List<GameIcon> allIcons;
        private GameIcon chosen;
        private byte[] customSvg;
        private string customFile;
        private Action syncHandler;

        #endregion

        #region Setting up the tab

        private const string IconLicenseText = "All icons from game-icons.net are licensed under CC BY 3.0";

        private static readonly KeyValuePair<string, string>[] IconLicenseLinks =
        {
            new KeyValuePair<string, string>("game-icons.net", "https://game-icons.net"),
            new KeyValuePair<string, string>("CC BY 3.0", "https://creativecommons.org/licenses/by/3.0/"),
        };

        private void InitIconsTab()
        {
            IconCatalog.Log = message => Log.Error(message);

            auroraIconImages = new ImageList { ImageSize = new Size(AuroraIconSize, AuroraIconSize), ColorDepth = ColorDepth.Depth32Bit };
            lvAuroraIcons.SmallImageList = auroraIconImages;
            lvAuroraIcons.Columns.Add("Icon", lvAuroraIcons.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
            lvAuroraIcons.SelectedIndexChanged += (s, e) => UpdateIconButtons();

            txtIconSearch.TextChanged += (s, e) => ApplyIconFilter();
            btnIconCheck.Click += (s, e) => CheckForNewIcons();
            tips.SetToolTip(btnIconCheck, "Compares the icon cache with game-icons.net and downloads any icons that are new or missing.  This is the only time Aurora Theme Manager looks for updates after the first launch.");

            iconGrid.GridBack = PanelBack;
            iconGrid.IconChosen += icon => ChooseIcon(icon);

            btnIconReplace.Click += (s, e) => ReplaceSelectedIcon();
            btnIconCustom.Click += (s, e) => ChooseCustomIcon();
            tips.SetToolTip(btnIconCustom, "Put an .svg file of your own in the chosen box.  Update then replaces the selected Aurora icon with it.");
            btnIconRestore.Click += (s, e) => RestoreSelectedIcon();
            btnIconRestoreAll.Click += (s, e) => RestoreAllIcons();
            tips.SetToolTip(picChosen, "Chosen: click an icon in the grid");

            tips.SetToolTip(lblAuroraIcons, "Aurora's toolbar icons.  A * marks one that has been replaced.");
            lblIconLicense.MouseMove += (s, e) => lblIconLicense.Cursor = IconLicenseUrlAt(e.Location) != null ? Cursors.Hand : Cursors.Default;
            lblIconLicense.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    OpenUrl(IconLicenseUrlAt(e.Location));
            };
            lblIconLicense.Text = IconLicenseText;

            LoadAuroraIcons();
            UpdateIconButtons();

            allIcons = IconCatalog.TryLoadCachedIndex();
            ApplyIconFilter();
            syncHandler = () => RunOnUi(OnIconSyncChanged);
            IconCatalog.SyncChanged += syncHandler;
            Disposed += (s, e) => IconCatalog.SyncChanged -= syncHandler;

            if (FirstLaunch)
            {
                FirstLaunch = false;
                SaveSettings();
                Log.Info("Icons: first launch, filling the icon cache");
                lblIconCache.Text = "Checking game-icons.net for icons...";
                IconCatalog.StartBackgroundSync(false, message => Log.Info(message));
            }
            else
            {
                lblIconCache.Text = IconCatalog.Syncing ? IconCatalog.SyncStatus : IconCatalog.CacheSummary();
            }
        }

        #endregion

        #region Aurora's icons

        private static string IconNameFromPath(string path)
        {
            return Path.GetFileNameWithoutExtension(path);
        }

        private void LoadAuroraIcons()
        {
            lvAuroraIcons.Items.Clear();
            auroraIconImages.Images.Clear();

            string dir = IconCatalog.IconsDirectory();
            if (!Directory.Exists(dir))
            {
                Log.Error("The Icons folder was not found at " + dir + ".");
                return;
            }

            string backups = IconCatalog.BackupDirectory();
            string[] files = Directory.GetFiles(dir, "*.svg").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (string file in files)
            {
                string name = IconNameFromPath(file);
                bool replaced = File.Exists(Path.Combine(backups, name + ".svg"));
                var item = new ListViewItem(replaced ? name + " *" : name) { Tag = file };

                Bitmap thumb = null;
                try { thumb = IconCatalog.Render(File.ReadAllText(file, Encoding.UTF8), AuroraIconSize); } catch { }
                if (thumb != null)
                {
                    thumb = OnDark(thumb, AuroraIconSize);
                    auroraIconImages.Images.Add(thumb);
                    item.ImageIndex = auroraIconImages.Images.Count - 1;
                }
                lvAuroraIcons.Items.Add(item);
            }

            preview.Invalidate();
        }

        private static Bitmap OnDark(Bitmap icon, int size)
        {
            var bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(PanelBack);
                g.DrawImage(icon, 0, 0, size, size);
            }
            icon.Dispose();
            return bmp;
        }

        private string SelectedIconPath()
        {
            return lvAuroraIcons.SelectedItems.Count == 0 ? null : (string)lvAuroraIcons.SelectedItems[0].Tag;
        }

        private void UpdateIconButtons()
        {
            string path = SelectedIconPath();
            bool haveIcon = path != null;
            string name = haveIcon ? IconNameFromPath(path) : null;

            btnIconReplace.Enabled = haveIcon && (chosen != null || customSvg != null);
            btnIconCustom.Enabled = true;
            btnIconRestore.Enabled = haveIcon && File.Exists(Path.Combine(IconCatalog.BackupDirectory(), name + ".svg"));
            btnIconRestoreAll.Enabled = Directory.Exists(IconCatalog.BackupDirectory())
                && Directory.GetFiles(IconCatalog.BackupDirectory(), "*.svg").Length > 0;

            tips.SetToolTip(picCurrent, haveIcon ? "Current: " + name : "Current: pick an Aurora icon on the left");
            Image old = picCurrent.Image;
            picCurrent.Image = null;
            if (old != null) old.Dispose();
            if (haveIcon)
            {
                try
                {
                    Bitmap bmp = IconCatalog.Render(File.ReadAllText(path, Encoding.UTF8), 64);
                    if (bmp != null) picCurrent.Image = OnDark(bmp, 64);
                }
                catch { }
            }
        }

        #endregion

        #region Browsing

        private void ApplyIconFilter()
        {
            List<GameIcon> list = IconCatalog.Filter(allIcons, txtIconSearch.Text);
            iconGrid.SetItems(list);
        }
        private void OnIconSyncChanged()
        {
            lblIconCache.Text = IconCatalog.Syncing || IconCatalog.SyncStatus.Length > 0 ? IconCatalog.SyncStatus : IconCatalog.CacheSummary();
            btnIconCheck.Enabled = !IconCatalog.Syncing;

            List<GameIcon> latest = IconCatalog.TryLoadCachedIndex();
            if (latest != null && (allIcons == null || latest.Count != allIcons.Count))
            {
                allIcons = latest;
                ApplyIconFilter();
            }
            iconGrid.RetryMissing();
        }
        private void CheckForNewIcons()
        {
            btnIconCheck.Enabled = false;
            lblIconCache.Text = "Checking for updated icons...";
            IconCatalog.StartBackgroundSync(true, message => Log.Info(message));
        }

        private void ChooseIcon(GameIcon icon)
        {
            chosen = icon;
            customSvg = null;
            customFile = null;
            tips.SetToolTip(picChosen, "Chosen: " + icon.Title + " by " + icon.Author);

            Image old = picChosen.Image;
            Bitmap thumb = iconGrid.TryGetThumb(icon);
            picChosen.Image = thumb != null ? new Bitmap(thumb) : null;
            if (old != null) old.Dispose();
            UpdateIconButtons();
        }

        private void ShowIconError(string message)
        {
            Log.Error(message);
            MessageBox.Show(this, message, "Icons", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private string IconLicenseUrlAt(Point point)
        {
            string text = lblIconLicense.Text;
            if (text != IconLicenseText)
                return null;

            try
            {
                Size client = lblIconLicense.ClientSize;
                using (Graphics g = lblIconLicense.CreateGraphics())
                using (var format = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near })
                {
                    var ranges = new CharacterRange[IconLicenseLinks.Length];
                    for (int i = 0; i < ranges.Length; i++)
                    {
                        string words = IconLicenseLinks[i].Key;
                        ranges[i] = new CharacterRange(IconLicenseText.IndexOf(words, StringComparison.Ordinal), words.Length);
                    }
                    format.SetMeasurableCharacterRanges(ranges);

                    var layout = new RectangleF(0, 0, client.Width, 10000);
                    float height = g.MeasureString(text, lblIconLicense.Font, layout.Size, format).Height;
                    Region[] regions = g.MeasureCharacterRanges(text, lblIconLicense.Font, layout, format);
                    string found = null;
                    for (int i = 0; i < regions.Length; i++)
                    {
                        regions[i].Translate(0, client.Height - height);
                        if (found == null && regions[i].IsVisible(point, g))
                            found = IconLicenseLinks[i].Value;
                        regions[i].Dispose();
                    }
                    return found;
                }
            }
            catch
            {
                return null;
            }
        }

        private void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;
            try { System.Diagnostics.Process.Start(url); }
            catch (Exception ex)
            {
                ShowIconError("Could not open " + url + ": " + ex.Message);
            }
        }

        private void RunOnUi(Action action)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(action);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        #endregion

        #region Replacing and restoring

        private void ReplaceSelectedIcon()
        {
            string path = SelectedIconPath();
            if (path == null)
                return;

            if (customSvg != null)
            {
                byte[] bytes = customSvg;
                ReplaceIcon(path, () => bytes, customFile);
                return;
            }
            if (chosen == null)
                return;

            GameIcon icon = chosen;
            ReplaceIcon(path, () => new UTF8Encoding(false).GetBytes(IconCatalog.GetSvg(icon)), icon.Key);
        }

        private void ChooseCustomIcon()
        {
            string file;
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select Custom Icon";
                dialog.Filter = "SVG images (*.svg)|*.svg|All files (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                file = dialog.FileName;
            }

            byte[] bytes;
            Bitmap preview;
            try
            {
                bytes = File.ReadAllBytes(file);
                XDocument doc;
                using (var stream = new MemoryStream(bytes))
                    doc = XDocument.Load(stream);
                if (doc.Root == null || doc.Root.Name.LocalName != "svg")
                    throw new InvalidDataException("it is not an SVG image");
                preview = IconCatalog.Render(Encoding.UTF8.GetString(bytes), 64);
            }
            catch (Exception ex)
            {
                ShowIconError("Could not use " + Path.GetFileName(file) + ": " + ex.Message);
                return;
            }

            chosen = null;
            customSvg = bytes;
            customFile = file;
            tips.SetToolTip(picChosen, "Chosen: " + Path.GetFileName(file));
            Image old = picChosen.Image;
            picChosen.Image = preview != null ? OnDark(preview, 64) : null;
            if (old != null) old.Dispose();
            UpdateIconButtons();
        }

        private void ReplaceIcon(string path, Func<byte[]> getSvg, string source)
        {
            string name = IconNameFromPath(path);

            btnIconReplace.Enabled = false;
            btnIconCustom.Enabled = false;

            ThreadPool.QueueUserWorkItem(state =>
            {
                string error = null;
                try
                {
                    byte[] svg = getSvg();

                    string backup = Path.Combine(IconCatalog.BackupDirectory(), name + ".svg");
                    if (!File.Exists(backup))
                        File.Copy(path, backup);

                    string temp = path + ".atm.tmp";
                    File.WriteAllBytes(temp, svg);
                    File.Copy(temp, path, true);
                    File.Delete(temp);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                RunOnUi(() =>
                {
                    if (error != null)
                    {
                        ShowIconError("Could not replace " + name + ".svg: " + error);
                    }
                    else
                    {
                        Log.Info("Replaced icon " + name + ".svg with " + source);
                        LoadAuroraIcons();
                        ReselectIcon(name);
                    }
                    UpdateIconButtons();
                });
            });
        }

        private void ReselectIcon(string name)
        {
            foreach (ListViewItem item in lvAuroraIcons.Items)
            {
                if (IconNameFromPath((string)item.Tag) == name)
                {
                    item.Selected = true;
                    item.EnsureVisible();
                    break;
                }
            }
        }

        private bool RestoreIcon(string name)
        {
            string backup = Path.Combine(IconCatalog.BackupDirectory(), name + ".svg");
            if (!File.Exists(backup))
                return false;

            string target = Path.Combine(IconCatalog.IconsDirectory(), name + ".svg");
            File.Copy(backup, target, true);
            File.Delete(backup);
            return true;
        }

        private void RestoreSelectedIcon()
        {
            string path = SelectedIconPath();
            if (path == null) return;
            string name = IconNameFromPath(path);

            try
            {
                if (RestoreIcon(name))
                {
                    Log.Info("Restored icon " + name + ".svg");
                    LoadAuroraIcons();
                    ReselectIcon(name);
                }
            }
            catch (Exception ex)
            {
                ShowIconError("Could not restore " + name + ".svg: " + ex.Message);
            }
            UpdateIconButtons();
        }

        private void RestoreAllIcons()
        {
            if (MessageBox.Show(this, "Put every replaced icon back to its original?", "Restore All Icons",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            int restored = 0;
            try
            {
                foreach (string backup in Directory.GetFiles(IconCatalog.BackupDirectory(), "*.svg"))
                    if (RestoreIcon(Path.GetFileNameWithoutExtension(backup)))
                        restored++;
            }
            catch (Exception ex)
            {
                ShowIconError("Could not restore every icon: " + ex.Message);
            }

            Log.Info("Restored " + restored + " icons");
            LoadAuroraIcons();
            UpdateIconButtons();
        }

        #endregion
    }
}
