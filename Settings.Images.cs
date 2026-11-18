using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace AuroraThemeManager
{
    public partial class Settings
    {
        #region Picture tabs

        // Very wide images get shorter thumbnails, as width is capped.
        private const int ThumbHeight = 96;
        private const int MinThumbWidth = 64;
        private const int MaxThumbWidth = 200;

        private sealed class ImageTab
        {
            public ImageFolder Folder;
            public TabPage Page;
            public ThumbGrid Grid;
            public Button Add, AddFolder, Delete, Randomize, PlayerDefault, Backup, Restore;
            public bool Loaded;
            public int Generation;

            // Reload the tab once outside changes settle.
            public FileSystemWatcher Watcher;
            public System.Windows.Forms.Timer Settle;

            // FolderState as last shown.
            public string Shown;
        }

        private readonly List<ImageTab> imageTabs = new List<ImageTab>();

        private void InitImageTabs()
        {
            foreach (ImageFolder folder in ImageFolder.All)
                imageTabs.Add(BuildImageTab(folder));

            // Placeholders use the theme as it is when each is made.
            ImageFolder.Look = () => new ImageFolder.PlaceholderLook
            {
                Back = config.ColorOr("Background", DesignBackground),
                Text = config.ColorOr("TextEditColour", ThemeText),
                Font = IsFontInstalled(config.FontFamily) ? config.FontFamily : ResolveFont(config.FontFamily),
            };

            // Check required images once the window is up.
            Shown += (s, e) => BeginInvoke((Action)CheckNamedPictures);

            // Load lazily so startup stays fast; revisiting also catches changes the watcher missed.
            tabMain.SelectedIndexChanged += (s, e) =>
            {
                ImageTab tab = imageTabs.FirstOrDefault(t => t.Page == tabMain.SelectedTab);
                if (tab == null)
                    return;
                if (!tab.Loaded)
                    LoadImageTab(tab);
                else
                    ReloadIfChanged(tab);
            };
            Disposed += (s, e) =>
            {
                foreach (ImageTab tab in imageTabs)
                {
                    tab.Watcher?.Dispose();
                    tab.Settle?.Dispose();
                }
            };
        }

        /// <summary>Reloads the tab once a burst of outside changes stops.</summary>
        private void WatchFolder(ImageTab tab)
        {
            tab.Settle = new System.Windows.Forms.Timer { Interval = 700 };
            tab.Settle.Tick += (s, e) =>
            {
                tab.Settle.Stop();
                ReloadIfChanged(tab);
            };
            try
            {
                if (!Directory.Exists(tab.Folder.Directory))
                    return;
                tab.Watcher = new FileSystemWatcher(tab.Folder.Directory)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                };
                FileSystemEventHandler changed = (s, e) => RunOnUi(() => { tab.Settle.Stop(); tab.Settle.Start(); });
                tab.Watcher.Created += changed;
                tab.Watcher.Deleted += changed;
                tab.Watcher.Changed += changed;
                tab.Watcher.Renamed += (s, e) => changed(s, e);
                tab.Watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Log.Error(tab.Folder.Title + ": could not watch " + tab.Folder.Directory + " for changes: " + ex.Message);
            }
        }

        // Name/size/time signature used to detect changes.
        private static string FolderState(ImageFolder folder)
        {
            try
            {
                return string.Join("|", folder.Images().Select(f =>
                {
                    var info = new FileInfo(f);
                    return info.Name + ":" + info.Length + ":" + info.LastWriteTimeUtc.Ticks;
                }));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>At launch, fills missing required images and logs them.</summary>
        private void CheckNamedPictures()
        {
            var report = new List<string>();
            foreach (ImageFolder folder in ImageFolder.All)
            {
                List<string> made = FillMissing(folder);
                if (made.Count > 0)
                    report.Add(folder.Folder + ": " + Describe(made));
            }
            if (report.Count == 0)
                return;
            foreach (ImageTab tab in imageTabs.Where(t => t.Loaded))
                ReloadIfChanged(tab);
            WarnMissing(string.Join("\n", report));
        }

        /// <summary>Fills missing required names. Skipped if the saves can't be read (e.g. Aurora has the DB open).</summary>
        private static List<string> FillMissing(ImageFolder folder)
        {
            try
            {
                var refilled = new List<string>();
                var placeholders = new List<string>();
                folder.FillMissing(SavedPictures.Read(folder), new Random(), refilled, placeholders);
                return refilled.Concat(placeholders.Select(n => n + " (placeholder)")).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(folder.Title + ": could not check for missing images: " + ex.Message);
                return new List<string>();
            }
        }

        private static string Lines(List<string> names)
        {
            return string.Join("\n", names);
        }

        private static string Describe(List<string> names)
        {
            return string.Join(", ", names);
        }

        // Log only: placeholders explain themselves in the tooltip.
        private static void WarnMissing(string message)
        {
            Log.Error("Missing images filled: " + message.Replace("\n", "; "));
        }

        /// <summary>Reloads if the folder no longer matches the tab, keeping the selection.</summary>
        private void ReloadIfChanged(ImageTab tab)
        {
            if (IsDisposed)
                return;
            if (!tab.Loaded)
            {
                // Not shown yet, but a required image may have gone.
                List<string> gone = FillMissing(tab.Folder);
                if (gone.Count > 0)
                    WarnMissing(tab.Folder.Folder + ": " + Describe(gone));
                return;
            }
            string state = FolderState(tab.Folder);
            if (state != null && state == tab.Shown)
                return;
            List<string> selected = tab.Grid.SelectedFiles.Select(Path.GetFileName).ToList();

            // An outside change may have removed a required image.
            List<string> made = FillMissing(tab.Folder);

            LoadImageTab(tab);
            if (selected.Count > 0)
                SelectImages(tab, selected);
            if (made.Count > 0)
                WarnMissing(tab.Folder.Folder + ": " + Describe(made));
        }

        private ImageTab BuildImageTab(ImageFolder folder)
        {
            var tab = new ImageTab { Folder = folder };

            tab.Page = new TabPage(folder.Title)
            {
                BackColor = DesignBackground,
                ForeColor = DesignText,
                Padding = new Padding(6),
            };

            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                BackColor = DesignBackground,
                Padding = new Padding(0, 0, 0, 6),
            };

            tab.Add = BarButton("Add Image...", 110);
            tab.AddFolder = BarButton("Add Folder...", 110);
            tab.Randomize = BarButton("Randomize", 110);
            tab.Backup = BarButton("Backup", 80);
            tab.Restore = BarButton("Restore", 80);
            tab.PlayerDefault = BarButton("Set Player Default", 130);
            tab.Delete = BarButton("Delete", 80);
            bar.Controls.Add(tab.Add);
            bar.Controls.Add(tab.AddFolder);
            bar.Controls.Add(tab.Delete);
            if (folder.CanRandomize)
                bar.Controls.Add(tab.Randomize);
            if (folder.PlayerDefault != null)
                bar.Controls.Add(tab.PlayerDefault);
            bar.Controls.Add(tab.Backup);
            bar.Controls.Add(tab.Restore);

            tab.Grid = new ThumbGrid
            {
                Dock = DockStyle.Fill,
                BackColor = DesignPanel,
                ForeColor = DesignText,
                TabStop = false,
                IsMarked = folder.IsPlayerDefault,
                TipFor = f => Placeholders.Is(f)
                    ? Path.GetFileName(f) + " (placeholder)\nA saved game uses this name but its image was missing.\nAdd images or Randomize to replace it."
                    : folder.IsPlayerDefault(f)
                        ? Path.GetFileName(f) + " (player default)"
                        : Path.GetFileName(f),
            };

            // Add Fill before Top so the grid takes the remaining space.
            tab.Page.Controls.Add(tab.Grid);
            tab.Page.Controls.Add(bar);
            tabMain.TabPages.Add(tab.Page);
            WatchFolder(tab);

            tab.Add.Click += (s, e) => AddImages(tab);
            tab.AddFolder.Click += (s, e) => AddImageFolder(tab);
            tab.Randomize.Click += (s, e) => RandomizeImages(tab);
            tab.Backup.Click += (s, e) => BackupImages(tab);
            tab.Restore.Click += (s, e) => RestoreImages(tab);
            tab.PlayerDefault.Click += (s, e) => SetPlayerDefault(tab);
            tab.Grid.SelectionChanged += () => UpdatePlayerDefaultButton(tab);
            tab.Delete.Click += (s, e) => DeleteImages(tab);
            tab.Grid.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete && e.Modifiers == Keys.None && tab.Grid.SelectedFiles.Count > 0)
                {
                    e.Handled = true;
                    DeleteImages(tab);
                }
            };
            UpdatePlayerDefaultButton(tab);

            tips.SetToolTip(tab.Add, "Add an image" + TypeNote(folder) + ".");
            tips.SetToolTip(tab.AddFolder, "Add every image in a folder" + TypeNote(folder) + ".");
            tips.SetToolTip(tab.Randomize, "Shuffle the images between file names.");
            tips.SetToolTip(tab.Backup, "Copy the images to " + folder.BackupDirectory + ".");
            tips.SetToolTip(tab.Restore, "Replace the images with the backup.");
            tips.SetToolTip(tab.Delete, "Delete the selected images (Delete key).");
            if (folder.PlayerDefault != null)
                tips.SetToolTip(tab.PlayerDefault, "Use the selected image for the player race in new games.");

            return tab;
        }

        private static string TypeNote(ImageFolder folder)
        {
            return folder.ShippedExtension != null ? ", as " + folder.ShippedExtension : "";
        }

        private static Button BarButton(string text, int width)
        {
            return new Button
            {
                Text = text,
                Size = new Size(width, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = DesignButton,
                ForeColor = DesignText,
                UseVisualStyleBackColor = false,
                Margin = new Padding(0, 3, 6, 3),
            };
        }

        #endregion

        #region Thumbnails

        private void LoadImageTab(ImageTab tab)
        {
            tab.Loaded = true;
            int generation = ++tab.Generation;

            // Normalize names to lowercase.
            try
            {
                tab.Folder.LowercaseNames();
            }
            catch (Exception ex)
            {
                Log.Error(tab.Folder.Title + ": could not make the image names lowercase: " + ex.Message);
            }

            // Player default first.
            List<string> files = tab.Folder.Images().OrderBy(f => tab.Folder.IsPlayerDefault(f) ? 0 : 1).ToList();
            tab.Shown = FolderState(tab.Folder);
            tab.Grid.SetFiles(files);
            UpdatePlayerDefaultButton(tab);

            if (!Directory.Exists(tab.Folder.Directory))
            {
                Log.Error("The " + tab.Folder.Folder + " folder was not found at " + tab.Folder.Directory + ".");
                return;
            }

            // Worker: measure for the cell size, then build thumbnails in batches. SizeWith folders are
            // measured together; Columns caps the width to fit.
            List<string> measured = tab.Folder.SizeWith == null ? files
                : ImageFolder.All.Where(f => tab.Folder.SizeWith.Contains(f.Folder, StringComparer.OrdinalIgnoreCase))
                    .SelectMany(f => f.Images()).ToList();
            int maxWidth = MaxThumbWidth;
            if (tab.Folder.Columns > 0)
                maxWidth = Math.Min(maxWidth, tab.Grid.MaxCellWidthFor(tab.Folder.Columns));

            ThreadPool.QueueUserWorkItem(state =>
            {
                Size cellSize = tab.Folder.FixedCell.IsEmpty ? CellSizeFor(measured, maxWidth) : tab.Folder.FixedCell;
                RunOnUi(() =>
                {
                    if (generation == tab.Generation)
                        tab.Grid.CellSize = cellSize;
                });

                string cache = tab.Folder.CacheDirectory;
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var batch = new List<KeyValuePair<int, Bitmap>>();
                for (int i = 0; i < files.Count; i++)
                {
                    if (generation != tab.Generation)
                        return;
                    Bitmap thumb = CachedThumb(files[i], cellSize, cache, used);
                    if (thumb != null)
                        batch.Add(new KeyValuePair<int, Bitmap>(i, thumb));
                    if (batch.Count >= 24 || i == files.Count - 1)
                    {
                        var ready = batch;
                        batch = new List<KeyValuePair<int, Bitmap>>();
                        RunOnUi(() => AddThumbs(tab, generation, ready));
                    }
                }
                PruneThumbCache(cache, used);
            });
        }

        private static void AddThumbs(ImageTab tab, int generation, List<KeyValuePair<int, Bitmap>> ready)
        {
            foreach (var pair in ready)
            {
                if (generation == tab.Generation)
                    tab.Grid.SetThumb(pair.Key, pair.Value);
                else
                    pair.Value.Dispose();
            }
        }

        /// <summary>Cell shaped like the median aspect ratio so most thumbnails fill it. Reads headers only.</summary>
        private static Size CellSizeFor(List<string> files, int maxWidth)
        {
            var aspects = new List<double>();
            foreach (string file in files)
            {
                try
                {
                    using (var stream = File.OpenRead(file))
                    using (Image image = Image.FromStream(stream, false, false))
                        if (image.Height > 0)
                            aspects.Add((double)image.Width / image.Height);
                }
                catch
                {
                }
            }
            double aspect = aspects.Count == 0 ? 1.0 : aspects.OrderBy(x => x).ElementAt(aspects.Count / 2);
            int width = (int)Math.Round(ThumbHeight * aspect);
            width = Math.Max(Math.Min(MinThumbWidth, maxWidth), Math.Min(maxWidth, width));
            int height = Math.Max(16, Math.Min(ThumbHeight, (int)Math.Round(width / aspect)));
            return new Size(width, height);
        }

        /// <summary>
        /// Thumbnail from ATM\Cache\(folder), made on first use. Keyed by content hash and cell size,
        /// not file name, since Randomize/Set Player Default/Delete move images between names.
        /// </summary>
        private static Bitmap CachedThumb(string file, Size cellSize, string cache, HashSet<string> used)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(file);
            }
            catch (Exception ex)
            {
                Log.Error("Could not read " + file + ": " + ex.Message);
                return null;
            }

            string name;
            using (var md5 = System.Security.Cryptography.MD5.Create())
                name = BitConverter.ToString(md5.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant()
                    + "_" + cellSize.Width + "x" + cellSize.Height + ".png";
            used.Add(name);
            string path = Path.Combine(cache, name);

            if (File.Exists(path))
            {
                try
                {
                    using (var stream = new MemoryStream(File.ReadAllBytes(path)))
                    using (Image image = Image.FromStream(stream))
                        return new Bitmap(image);
                }
                catch
                {
                    // Damaged; remade below.
                }
            }

            Bitmap thumb = MakeThumb(bytes, file, cellSize);
            if (thumb == null)
                return null;
            try
            {
                Directory.CreateDirectory(cache);
                string temp = path + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
                thumb.Save(temp, System.Drawing.Imaging.ImageFormat.Png);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception ex)
            {
                Log.Error("Could not cache the thumbnail of " + file + ": " + ex.Message);
            }
            return thumb;
        }

        /// <summary>Deletes cached thumbnails nothing uses now (deleted images, old cell sizes).</summary>
        private static void PruneThumbCache(string cache, HashSet<string> used)
        {
            try
            {
                if (!Directory.Exists(cache))
                    return;
                foreach (string file in Directory.GetFiles(cache))
                    if (!used.Contains(Path.GetFileName(file)))
                        File.Delete(file);
            }
            catch (Exception ex)
            {
                Log.Error("Could not tidy " + cache + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Aspect-preserving thumbnail on a clear background. Decoded from bytes so the file isn't
        /// left locked (which would block renames).
        /// </summary>
        private static Bitmap MakeThumb(byte[] bytes, string file, Size cellSize)
        {
            try
            {
                using (var stream = new MemoryStream(bytes))
                using (Image image = Image.FromStream(stream))
                {
                    float scale = Math.Min((float)cellSize.Width / image.Width, (float)cellSize.Height / image.Height);
                    int w = Math.Max(1, (int)Math.Round(image.Width * scale));
                    int h = Math.Max(1, (int)Math.Round(image.Height * scale));
                    var thumb = new Bitmap(w, h);
                    using (Graphics g = Graphics.FromImage(thumb))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(image, 0, 0, w, h);
                    }
                    return thumb;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not read " + file + ": " + ex.Message);
                return null;
            }
        }

        #endregion

        #region Adding, randomizing

        private void AddImages(ImageTab tab)
        {
            string[] files;
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Add Image to " + tab.Folder.Folder;
                dialog.Filter = "Images (*.bmp;*.png;*.jpg;*.jpeg;*.gif;*.tif;*.tiff)|*.bmp;*.png;*.jpg;*.jpeg;*.gif;*.tif;*.tiff|All files (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                dialog.InitialDirectory = ExtraStart(tab.Folder);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                files = dialog.FileNames;
            }
            ImportImages(tab, files);
        }

        private void AddImageFolder(ImageTab tab)
        {
            string dir;
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Add every image in this folder to " + tab.Folder.Folder + ".";
                dialog.ShowNewFolderButton = false;
                dialog.SelectedPath = ExtraStart(tab.Folder);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                dir = dialog.SelectedPath;
            }

            if (string.Equals(Path.GetFullPath(dir).TrimEnd('\\', '/'), Path.GetFullPath(tab.Folder.Directory).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                ShowImageError(tab, "That is the " + tab.Folder.Folder + " folder itself.");
                return;
            }

            string[] files = Directory.GetFiles(dir).Where(ImageFolder.IsImage)
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0)
            {
                ShowImageError(tab, "No images found.");
                return;
            }
            ImportImages(tab, files);
        }

        // The folder's Extra Graphics folder (created if missing), else ATM's folder.
        private static string ExtraStart(ImageFolder folder)
        {
            try
            {
                Directory.CreateDirectory(folder.ExtraDirectory);
                return folder.ExtraDirectory;
            }
            catch
            {
                return Program.DataDirectory;
            }
        }

        private void ImportImages(ImageTab tab, string[] files)
        {
            ImageFolder folder = tab.Folder;
            var failures = new List<string>();

            List<string> added;
            Cursor = Cursors.WaitCursor;
            try
            {
                added = folder.Import(files, failures);
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            // New images can replace placeholders.
            int replaced = ReplacePlaceholders(tab);

            LoadImageTab(tab);
            SelectImages(tab, added);
            if (failures.Count > 0)
                ShowImageError(tab, "Not added:\n\n" + Lines(failures));
        }

        private static void SelectImages(ImageTab tab, List<string> names)
        {
            tab.Grid.Select(names);
        }

        private void RandomizeImages(ImageTab tab)
        {
            ImageFolder folder = tab.Folder;

            // Saved names decide what may be converted, so don't shuffle without them.
            HashSet<string> saved;
            try
            {
                saved = SavedPictures.Read(folder);
            }
            catch (Exception ex)
            {
                Log.Error(folder.Title + ": could not read the saved games: " + ex);
                ShowImageError(tab, "Can't read AuroraDB.db.  Close Aurora and try again.");
                return;
            }

            string question = "Shuffle the " + folder.Title.ToLowerInvariant() + " images?  Saved games will show the new images.";
            if (MessageBox.Show(this, question, "Randomize " + folder.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            int changed = 0, converted = 0;
            Cursor = Cursors.WaitCursor;
            try
            {
                ReplacePlaceholders(tab);
                changed = folder.Randomize(new Random(), saved, out converted);
            }
            catch (Exception ex)
            {
                Log.Error(folder.Title + ": randomize failed: " + ex);
                ShowImageError(tab, "Shuffle stopped: " + ex.Message);
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            LoadImageTab(tab);
        }

        private static void UpdatePlayerDefaultButton(ImageTab tab)
        {
            tab.Delete.Enabled = tab.Grid.SelectedFiles.Count > 0;
            if (tab.Folder.PlayerDefault == null)
                return;
            List<string> picked = tab.Grid.SelectedFiles;
            tab.PlayerDefault.Enabled = picked.Count == 1 && tab.Folder.CanBePlayerDefault(picked[0]);
        }

        private void SetPlayerDefault(ImageTab tab)
        {
            List<string> picked = tab.Grid.SelectedFiles;
            if (picked.Count != 1)
                return;
            string file = picked[0];
            string name = Path.GetFileName(file);

            // Only used when there is no default yet (rename, not swap).
            HashSet<string> saved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                saved = SavedPictures.Read(tab.Folder);
            }
            catch (Exception ex)
            {
                Log.Error(tab.Folder.Title + ": could not read the saved games before setting the player default: " + ex.Message);
            }

            string refilled;
            try
            {
                refilled = tab.Folder.SetPlayerDefault(file, saved);
            }
            catch (Exception ex)
            {
                ShowImageError(tab, "Could not make " + name + " the player default: " + ex.Message);
                return;
            }

            LoadImageTab(tab);
            SelectImages(tab, new List<string> { tab.Folder.PlayerDefault });

        }

        private void DeleteImages(ImageTab tab)
        {
            ImageFolder folder = tab.Folder;
            List<string> picked = tab.Grid.SelectedFiles;
            if (picked.Count == 0)
                return;

            // Warn before deleting if saved names can't be read.
            HashSet<string> saved;
            try
            {
                saved = SavedPictures.Read(folder);
            }
            catch (Exception ex)
            {
                Log.Error(folder.Title + ": could not read the saved games: " + ex);
                string warning = "Can't read AuroraDB.db, so images your saved games use can't be protected.  Delete anyway?";
                if (MessageBox.Show(this, warning, "Delete " + folder.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;
                saved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            // Required images are refilled afterwards, so they aren't called out here.
            string question = "Delete " + (picked.Count == 1 ? Path.GetFileName(picked[0]) : picked.Count + " images") + "?";
            if (MessageBox.Show(this, question, "Delete " + folder.Title, MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            var refilled = new List<string>();
            var placeholders = new List<string>();
            int deleted = 0;
            Cursor = Cursors.WaitCursor;
            try
            {
                deleted = folder.Delete(picked, saved, new Random(), refilled, placeholders);
            }
            catch (Exception ex)
            {
                ShowImageError(tab, "Delete stopped: " + ex.Message);
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            LoadImageTab(tab);
        }

        /// <summary>
        /// Replaces placeholders where spares exist. Skipped if the saves can't be read, as a used
        /// image could otherwise be renamed. Returns the count replaced.
        /// </summary>
        private static int ReplacePlaceholders(ImageTab tab)
        {
            HashSet<string> saved;
            try
            {
                saved = SavedPictures.Read(tab.Folder);
            }
            catch (Exception ex)
            {
                Log.Error(tab.Folder.Title + ": placeholders left as they are, as the saved games could not be read: " + ex.Message);
                return 0;
            }
            try
            {
                int remaining;
                return tab.Folder.ReplacePlaceholders(saved, out remaining);
            }
            catch (Exception ex)
            {
                Log.Error(tab.Folder.Title + ": could not replace placeholders: " + ex.Message);
                return 0;
            }
        }

        private void BackupImages(ImageTab tab)
        {
            ImageFolder folder = tab.Folder;
            if (folder.HasBackup
                && MessageBox.Show(this, "Overwrite the backup from " + folder.BackupTime.ToString("g") + "?", "Backup " + folder.Title,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            Cursor = Cursors.WaitCursor;
            try
            {
                folder.Backup();
            }
            catch (Exception ex)
            {
                Log.Error(folder.Title + ": backup failed: " + ex);
                ShowImageError(tab, "Backup failed: " + ex.Message);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void RestoreImages(ImageTab tab)
        {
            ImageFolder folder = tab.Folder;
            if (!folder.HasBackup)
            {
                ShowImageError(tab, "No backup.");
                return;
            }

            // Needed to fill names the backup lacks.
            HashSet<string> saved;
            try
            {
                saved = SavedPictures.Read(folder);
            }
            catch (Exception ex)
            {
                Log.Error(folder.Title + ": could not read the saved games: " + ex);
                ShowImageError(tab, "Can't read AuroraDB.db.  Close Aurora and try again.");
                return;
            }

            if (MessageBox.Show(this, "Replace the images with the backup from " + folder.BackupTime.ToString("g") + "?", "Restore " + folder.Title,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            Cursor = Cursors.WaitCursor;
            try
            {
                folder.Restore(saved, new Random(), new List<string>(), new List<string>());
            }
            catch (Exception ex)
            {
                Log.Error(folder.Title + ": restore failed: " + ex);
                ShowImageError(tab, "Restore stopped: " + ex.Message);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
            LoadImageTab(tab);
        }

        private void ShowImageInfo(ImageTab tab, string message)
        {
            Log.Info(tab.Folder.Title + ": " + message.Replace("\n", " "));
            MessageBox.Show(this, message, tab.Folder.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowImageError(ImageTab tab, string message)
        {
            Log.Error(tab.Folder.Title + ": " + message);
            MessageBox.Show(this, message, tab.Folder.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        #endregion
    }
}
