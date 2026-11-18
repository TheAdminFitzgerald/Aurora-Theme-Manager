using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AuroraThemeManager
{
    /// <summary>
    /// One of Aurora's image folders. Imports are converted to the type shipped there (.bmp Races,
    /// .jpg Flags, .png Ships/Stations) so they behave like the originals; Medals keep theirs.
    /// </summary>
    internal sealed class ImageFolder
    {
        public string Title;

        /// <summary>Folder name inside the Aurora folder, and inside ATM\Backups.</summary>
        public string Folder;

        /// <summary>Prefix and digit count used when the folder has no numbered images yet.</summary>
        public string DefaultPrefix;
        public int DefaultDigits;

        /// <summary>Type imports are converted to; null (Medals) keeps each import's own type.</summary>
        public string ShippedExtension;

        /// <summary>Type every image should be; Medals ship as .png.</summary>
        public string Format
        {
            get { return ShippedExtension ?? ".png"; }
        }

        public bool CanRandomize;

        /// <summary>
        /// File new games give the player race (race327.bmp, flag0000.jpg), or null. Changed by
        /// swapping which image has this name.
        /// </summary>
        public string PlayerDefault;

        /// <summary>Names new games always use (player defaults, spoiler races); kept even if no save uses them.</summary>
        public string[] FixedNames = new string[0];

        /// <summary>Folders measured together so their tabs share a thumbnail size; null = this folder alone.</summary>
        public string[] SizeWith;

        /// <summary>Columns the tab always fits, narrowing thumbnails if needed; 0 = no fixed count.</summary>
        public int Columns;

        /// <summary>Fixed thumbnail size, or empty to size from the images.</summary>
        public System.Drawing.Size FixedCell;

        /// <summary>Table.Column pairs holding this folder's file names in saved games.</summary>
        public string[] SavedColumns = new string[0];

        internal static readonly ImageFolder[] All =
        {
            new ImageFolder
            {
                Title = "Flags", Folder = "Flags", DefaultPrefix = "flag", DefaultDigits = 4, ShippedExtension = ".jpg", CanRandomize = true,
                PlayerDefault = "flag0000.jpg", FixedNames = new[] { "flag0000.jpg" }, Columns = 4,
                SavedColumns = new[] { "FCT_Race.FlagPic" },
            },
            new ImageFolder
            {
                Title = "Medals", Folder = "Medals", DefaultPrefix = "", DefaultDigits = 0, CanRandomize = true,
                // Medal ribbons are 105x30; shown at native size.
                FixedCell = new System.Drawing.Size(105, 30),
                SavedColumns = new[] { "FCT_RaceMedals.MedalFileName" },
            },
            new ImageFolder
            {
                Title = "Races", Folder = "Races", DefaultPrefix = "race", DefaultDigits = 3, ShippedExtension = ".bmp", CanRandomize = true,
                PlayerDefault = "race327.bmp",
                FixedNames = new[] { "race327.bmp", "race087.bmp", "race156.bmp", "race356.bmp", "race028.bmp" },
                SavedColumns = new[] { "FCT_Species.RacePic" },
            },
            new ImageFolder
            {
                Title = "Ships", Folder = "ShipIcons", DefaultPrefix = "ship", DefaultDigits = 3, ShippedExtension = ".png", CanRandomize = true,
                SizeWith = new[] { "ShipIcons", "StationIcons" },
                SavedColumns = new[] { "FCT_Race.HullPic" },
            },
            new ImageFolder
            {
                Title = "Stations", Folder = "StationIcons", DefaultPrefix = "spacestation", DefaultDigits = 3, ShippedExtension = ".png", CanRandomize = true,
                SizeWith = new[] { "ShipIcons", "StationIcons" },
                SavedColumns = new[] { "FCT_Race.SpaceStationPic" },
            },
        };

        /// <summary>Types System.Drawing can open.</summary>
        internal static readonly string[] ImageExtensions = { ".bmp", ".png", ".jpg", ".jpeg", ".gif", ".tif", ".tiff" };

        internal static bool IsImage(string path)
        {
            return ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
        }

        public string Directory
        {
            get { return Path.Combine(AuroraConfig.GameDirectory(), Folder); }
        }

        public string BackupDirectory
        {
            get { return Path.Combine(ImageBackups.Root, Folder); }
        }

        /// <summary>ATM\Cache\(folder): thumbnails.</summary>
        public string CacheDirectory
        {
            get { return Path.Combine(CacheFolders.Root, Folder); }
        }

        /// <summary>ATM\Extra Graphics\(folder): players' own images; Add Image/Folder start here.</summary>
        public string ExtraDirectory
        {
            get { return Path.Combine(Program.DataDirectory, "Extra Graphics", Folder); }
        }

        public List<string> Images()
        {
            if (!System.IO.Directory.Exists(Directory))
                return new List<string>();
            return System.IO.Directory.GetFiles(Directory)
                .Where(IsImage)
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        #region Naming

        // Windows paths are case-insensitive; ATM writes every name in lowercase for consistency.
        private static string Lower(string path)
        {
            return Path.Combine(Path.GetDirectoryName(path), Path.GetFileName(path).ToLowerInvariant());
        }

        /// <summary>
        /// Lowercases image names, skipping any whose lowercase name already exists (case-sensitive
        /// file systems). Returns the count renamed.
        /// </summary>
        public int LowercaseNames()
        {
            if (!System.IO.Directory.Exists(Directory))
                return 0;
            var names = new HashSet<string>(System.IO.Directory.GetFileSystemEntries(Directory).Select(Path.GetFileName), StringComparer.Ordinal);
            int renamed = 0;
            foreach (string file in Images())
            {
                string name = Path.GetFileName(file);
                string lower = name.ToLowerInvariant();
                if (name == lower)
                    continue;
                if (names.Contains(lower))
                {
                    Log.Error(Title + ": left " + name + " as it is, as " + lower + " already exists");
                    continue;
                }
                try
                {
                    File.Move(file, Path.Combine(Directory, lower));
                    names.Remove(name);
                    names.Add(lower);
                    renamed++;
                }
                catch (Exception ex)
                {
                    Log.Error(Title + ": could not rename " + name + " to " + lower + ": " + ex.Message);
                }
            }
            if (renamed > 0)
                Log.Info(Title + ": renamed " + renamed + " images to lowercase names");
            return renamed;
        }

        private static readonly Regex Numbered = new Regex(@"^(?<prefix>.*?)(?<number>\d+)$", RegexOptions.Compiled);

        /// <summary>Prefix and digit count, e.g. race and 3 for race001.</summary>
        internal sealed class Naming
        {
            public string Prefix;
            public int Digits;

            public string Name(int number, string extension)
            {
                return (Prefix + (Digits > 0 ? number.ToString("D" + Digits) : number.ToString()) + extension).ToLowerInvariant();
            }
        }

        /// <summary>
        /// Naming from existing numbered images: majority prefix, and the most common padded width
        /// (at least DefaultDigits). Defaults if there are none.
        /// </summary>
        public Naming DetectNaming()
        {
            var numbered = Images()
                .Select(f => Numbered.Match(Path.GetFileNameWithoutExtension(f)))
                .Where(m => m.Success)
                .ToList();

            var group = numbered
                .GroupBy(m => m.Groups["prefix"].Value.ToLowerInvariant())
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key == DefaultPrefix.ToLowerInvariant())
                .FirstOrDefault();
            if (group == null)
                return new Naming { Prefix = DefaultPrefix, Digits = DefaultDigits };

            string prefix = group.Key;

            List<string> numbers = group.Select(m => m.Groups["number"].Value).ToList();
            bool padded = numbers.Any(n => n.Length > 1 && n[0] == '0');
            int digits = padded
                ? numbers.GroupBy(n => n.Length).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First().Key
                : 0;

            // Only race100+ shows no padding, but new images must still be race001, not race1.
            return new Naming { Prefix = prefix, Digits = Math.Max(digits, DefaultDigits) };
        }

        /// <summary>Numbers used with this prefix by any file of any type, so new images never collide.</summary>
        private HashSet<int> UsedNumbers(Naming naming)
        {
            var used = new HashSet<int>();
            if (!System.IO.Directory.Exists(Directory))
                return used;
            foreach (string file in System.IO.Directory.GetFiles(Directory))
            {
                Match m = Numbered.Match(Path.GetFileNameWithoutExtension(file));
                int number;
                if (m.Success && string.Equals(m.Groups["prefix"].Value, naming.Prefix, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(m.Groups["number"].Value, out number))
                    used.Add(number);
            }
            return used;
        }

        #endregion

        #region Importing

        /// <summary>
        /// Copies images in at the lowest free numbers, converting to ShippedExtension where set.
        /// Returns the new names; failures get the reason.
        /// </summary>
        public List<string> Import(IEnumerable<string> sources, List<string> failures)
        {
            System.IO.Directory.CreateDirectory(Directory);
            Naming naming = DetectNaming();
            HashSet<int> used = UsedNumbers(naming);
            var added = new List<string>();
            int next = 1;

            foreach (string source in sources)
            {
                try
                {
                    string sourceExtension = Path.GetExtension(source);
                    string extension = ShippedExtension ?? sourceExtension;

                    // Decode first so an unreadable file fails before anything is written.
                    using (var stream = new MemoryStream(File.ReadAllBytes(source)))
                    using (Image image = Image.FromStream(stream))
                    {
                        while (used.Contains(next))
                            next++;
                        string target = Path.Combine(Directory, naming.Name(next, extension));
                        if (File.Exists(target))
                            throw new IOException(Path.GetFileName(target) + " already exists");

                        bool converted = !SameType(image, extension);
                        if (converted)
                            Save(image, target, extension);
                        else
                            File.Copy(source, target, false);

                        used.Add(next);
                        added.Add(Path.GetFileName(target));
                        Log.Info(Title + ": " + (converted ? "converted " : "imported ") + source + " as " + Path.GetFileName(target));
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(Path.GetFileName(source) + ": " + ex.Message);
                    Log.Error(Title + ": could not import " + source + ": " + ex.Message);
                }
            }
            return added;
        }

        /// <summary>Whether the data is already the extension's type (.jpeg copies as .jpg without recompressing).</summary>
        private static bool SameType(Image image, string extension)
        {
            ImageFormat format = image.RawFormat;
            switch (extension.ToLowerInvariant())
            {
                case ".bmp": return format.Equals(ImageFormat.Bmp);
                case ".jpg":
                case ".jpeg": return format.Equals(ImageFormat.Jpeg);
                case ".png": return format.Equals(ImageFormat.Png);
                case ".gif": return format.Equals(ImageFormat.Gif);
                case ".tif":
                case ".tiff": return format.Equals(ImageFormat.Tiff);
                default: return true;
            }
        }

        private const long JpegQuality = 92;

        /// <summary>
        /// Saves as the extension's type. BMP/JPEG lack transparency, so flatten onto black (like the
        /// stock images' dark backgrounds) instead of exposing whatever transparent pixels hold.
        /// </summary>
        private static void Save(Image image, string target, string extension)
        {
            string type = extension.ToLowerInvariant();
            if (type == ".png")
            {
                image.Save(target, ImageFormat.Png);
                return;
            }

            using (var flat = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb))
            {
                flat.SetResolution(image.HorizontalResolution, image.VerticalResolution);
                using (Graphics g = Graphics.FromImage(flat))
                {
                    g.Clear(Color.Black);
                    g.DrawImage(image, 0, 0, image.Width, image.Height);
                }

                if (type == ".jpg" || type == ".jpeg")
                {
                    ImageCodecInfo jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    using (var parameters = new EncoderParameters(1))
                    {
                        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, JpegQuality);
                        flat.Save(target, jpeg, parameters);
                    }
                }
                else
                {
                    flat.Save(target, ImageFormat.Bmp);
                }
            }
        }

        #endregion

        #region Player default

        public bool IsPlayerDefault(string file)
        {
            return PlayerDefault != null && string.Equals(Path.GetFileName(file), PlayerDefault, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Same type as the default name and not already the default.</summary>
        public bool CanBePlayerDefault(string file)
        {
            return PlayerDefault != null && !IsPlayerDefault(file)
                && string.Equals(Path.GetExtension(file), Path.GetExtension(PlayerDefault), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Swaps the image's name with the default name so no name is lost. With no default file yet it
        /// is renamed, and a required old name is taken by a same-type spare.
        /// Returns that rename as "old -> new", or null.
        /// </summary>
        public string SetPlayerDefault(string file, ICollection<string> saved)
        {
            if (!CanBePlayerDefault(file))
                throw new InvalidOperationException(Path.GetFileName(file) + " cannot be the player default.");

            string target = Path.Combine(Directory, PlayerDefault);
            if (!File.Exists(target))
            {
                string name = Path.GetFileName(file);
                string spare = IsRequired(file, saved) ? Spares(new[] { file }, saved).FirstOrDefault(f =>
                    string.Equals(Path.GetExtension(f), Path.GetExtension(file), StringComparison.OrdinalIgnoreCase)) : null;

                File.Move(file, target);
                Log.Info(Title + ": renamed " + name + " to " + PlayerDefault + ", the player default");
                if (spare == null)
                    return null;
                File.Move(spare, Lower(file));
                Log.Info(Title + ": renamed " + Path.GetFileName(spare) + " to " + Path.GetFileName(Lower(file)) + ", which Aurora may ask for");
                return Path.GetFileName(spare) + " -> " + Path.GetFileName(Lower(file));
            }

            string temp = Path.Combine(Directory, "~atm-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.Move(target, temp);
            try
            {
                File.Move(file, target);
            }
            catch
            {
                File.Move(temp, target);
                throw;
            }
            File.Move(temp, Lower(file));
            Log.Info(Title + ": swapped " + Path.GetFileName(file) + " with " + PlayerDefault + ", the player default");
            return null;
        }

        /// <summary>Unused, non-placeholder images not in <paramref name="excluded"/>, highest numbered first.</summary>
        private List<string> Spares(IEnumerable<string> excluded, ICollection<string> saved)
        {
            var skip = new HashSet<string>(excluded, StringComparer.OrdinalIgnoreCase);
            Naming naming = DetectNaming();
            return Images()
                .Where(f => !skip.Contains(f) && !IsRequired(f, saved) && !Placeholders.Is(f))
                .OrderByDescending(f => NumberOf(f, naming))
                .ThenByDescending(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        #endregion

        #region Deleting

        /// <summary>Whether Aurora may ask for this name: a fixed name or one a saved game uses.</summary>
        public bool IsRequired(string file, ICollection<string> saved)
        {
            string name = Path.GetFileName(file);
            return FixedNames.Contains(name, StringComparer.OrdinalIgnoreCase)
                || (saved != null && saved.Contains(name));
        }

        /// <summary>
        /// Deletes the images, refilling required names from random spares or placeholders.
        /// Returns the count deleted.
        /// </summary>
        public int Delete(IEnumerable<string> files, ICollection<string> saved, Random random, List<string> refilled, List<string> placeholders)
        {
            var needed = new List<string>();
            int deleted = 0;
            foreach (string file in files.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                File.Delete(file);
                deleted++;
                Log.Info(Title + ": deleted " + Path.GetFileName(file));
                if (IsRequired(file, saved))
                    needed.Add(Path.GetFileName(file).ToLowerInvariant());
            }
            Refill(needed, saved, random, refilled, placeholders);
            return deleted;
        }

        /// <summary>Gives each missing name a random same-type spare, or a placeholder if none is left.</summary>
        private void Refill(List<string> names, ICollection<string> saved, Random random, List<string> refilled, List<string> placeholders)
        {
            if (names.Count == 0)
                return;
            List<string> spares = Spares(new string[0], saved);
            Size size = Size.Empty;
            foreach (string name in names)
            {
                string target = Path.Combine(Directory, name.ToLowerInvariant());
                try
                {
                    if (File.Exists(target))
                        continue;
                    List<string> sameType = spares.Where(f => SameExtension(Path.GetExtension(f), Path.GetExtension(name))).ToList();
                    if (sameType.Count > 0)
                    {
                        string spare = sameType[random.Next(sameType.Count)];
                        spares.Remove(spare);
                        File.Move(spare, target);
                        refilled.Add(Path.GetFileName(spare) + " -> " + Path.GetFileName(target));
                        Log.Info(Title + ": renamed " + Path.GetFileName(spare) + " to " + Path.GetFileName(target) + ", which Aurora may ask for");
                    }
                    else
                    {
                        if (size.IsEmpty)
                            size = PlaceholderSize();
                        MakePlaceholder(target, size);
                        placeholders.Add(Path.GetFileName(target));
                        Log.Info(Title + ": made a placeholder for " + Path.GetFileName(target) + ", as no unused image of its type is left");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(Title + ": could not refill " + name + ": " + ex.Message);
                }
            }
        }

        private void MakePlaceholder(string target, Size size)
        {
            using (Bitmap image = Placeholder(size))
                Save(image, target, Path.GetExtension(target));
            Placeholders.Remember(target);
        }

        private static int NumberOf(string file, Naming naming)
        {
            Match m = Numbered.Match(Path.GetFileNameWithoutExtension(file));
            int number;
            return m.Success && string.Equals(m.Groups["prefix"].Value, naming.Prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(m.Groups["number"].Value, out number) ? number : -1;
        }

        #endregion

        #region Backup and restore

        /// <summary>Whether ATM\Backups\(folder) holds any images.</summary>
        public bool HasBackup
        {
            get
            {
                return System.IO.Directory.Exists(BackupDirectory)
                    && System.IO.Directory.GetFiles(BackupDirectory).Any(IsImage);
            }
        }

        /// <summary>The backup folder's time, which Backup sets.</summary>
        public DateTime BackupTime
        {
            get { return System.IO.Directory.GetLastWriteTime(BackupDirectory); }
        }

        /// <summary>
        /// Replaces ATM\Backups\(folder) with a full copy, built alongside and swapped in when done so a
        /// failure leaves the old backup intact. Returns files copied.
        /// </summary>
        public int Backup()
        {
            string partial = BackupDirectory + ".partial";
            string old = BackupDirectory + ".old";
            if (System.IO.Directory.Exists(partial))
                System.IO.Directory.Delete(partial, true);
            int copied = ImageBackups.CopyTree(Directory, partial);
            if (System.IO.Directory.Exists(old))
                System.IO.Directory.Delete(old, true);
            if (System.IO.Directory.Exists(BackupDirectory))
                System.IO.Directory.Move(BackupDirectory, old);
            System.IO.Directory.Move(partial, BackupDirectory);
            if (System.IO.Directory.Exists(old))
                System.IO.Directory.Delete(old, true);
            // Stamp with the backup time, not the images' last change.
            System.IO.Directory.SetLastWriteTime(BackupDirectory, DateTime.Now);
            Log.Info(Title + ": backed up " + copied + " files to " + BackupDirectory);
            return copied;
        }

        /// <summary>
        /// Replaces the images with the backup's (lowercased; subfolders untouched), then fills required
        /// names it lacks. Returns the count restored.
        /// </summary>
        public int Restore(ICollection<string> saved, Random random, List<string> refilled, List<string> placeholders)
        {
            if (!HasBackup)
                throw new InvalidOperationException("there is no backup");
            List<string> backup = System.IO.Directory.GetFiles(BackupDirectory).Where(IsImage).ToList();
            foreach (string file in Images())
                File.Delete(file);
            int restored = 0;
            foreach (string file in backup)
            {
                File.Copy(file, Path.Combine(Directory, Path.GetFileName(file).ToLowerInvariant()), true);
                restored++;
            }
            Log.Info(Title + ": restored " + restored + " images from " + BackupDirectory);
            FillMissing(saved, random, refilled, placeholders);
            return restored;
        }

        #endregion

        #region Missing images

        /// <summary>Every name Aurora may ask for: fixed names plus saved-game names.</summary>
        public HashSet<string> NamesAuroraUses(ICollection<string> saved)
        {
            var names = new HashSet<string>(FixedNames, StringComparer.OrdinalIgnoreCase);
            if (saved != null)
                names.UnionWith(saved.Where(n => !string.IsNullOrWhiteSpace(n)));
            return names;
        }

        /// <summary>Most common image size; FixedCell or 256x256 when there are none.</summary>
        private Size PlaceholderSize()
        {
            if (FixedCell != Size.Empty && !Images().Any())
                return FixedCell;
            var sizes = new List<Size>();
            foreach (string file in Images())
            {
                try
                {
                    using (var stream = File.OpenRead(file))
                    using (Image image = Image.FromStream(stream, false, false))
                        sizes.Add(image.Size);
                }
                catch
                {
                }
            }
            return sizes.Count == 0 ? new Size(256, 256)
                : sizes.GroupBy(z => z).OrderByDescending(g => g.Count()).First().Key;
        }

        private const string PlaceholderText = "PLACEHOLDER";

        internal sealed class PlaceholderLook
        {
            public Color Back = Color.Black;
            public Color Text = Color.White;
            public string Font = "Microsoft Sans Serif";
        }

        /// <summary>Read per placeholder; Settings points it at the current theme.</summary>
        internal static Func<PlaceholderLook> Look = () => new PlaceholderLook();

        /// <summary>PLACEHOLDER drawn along the bottom-left to top-right diagonal, as large as fits.</summary>
        private static Bitmap Placeholder(Size size)
        {
            PlaceholderLook look = Look() ?? new PlaceholderLook();
            var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var brush = new SolidBrush(Color.FromArgb(255, look.Text)))
            {
                g.Clear(Color.FromArgb(255, look.Back));
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                FontFamily family;
                try { family = new FontFamily(look.Font); }
                catch { family = FontFamily.GenericSansSerif; }

                using (family)
                {
                    // Rotated w x h text spans w cos + h sin across, w sin + h cos down; shrink to fit 90%.
                    double angle = Math.Atan2(size.Height, size.Width);
                    float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
                    float em = Math.Max(size.Width, size.Height) * 0.3f;
                    Font font = null;
                    try
                    {
                        while (true)
                        {
                            font?.Dispose();
                            font = new Font(family, Math.Max(3f, em), FontStyle.Bold, GraphicsUnit.Pixel);
                            SizeF measured = g.MeasureString(PlaceholderText, font);
                            float across = measured.Width * cos + measured.Height * sin;
                            float down = measured.Width * sin + measured.Height * cos;
                            if (em <= 3f || (across <= size.Width * 0.9f && down <= size.Height * 0.9f))
                                break;
                            em *= 0.9f;
                        }
                        g.TranslateTransform(size.Width / 2f, size.Height / 2f);
                        g.RotateTransform((float)(-angle * 180.0 / Math.PI));
                        g.DrawString(PlaceholderText, font, brush, new RectangleF(-size.Width, -size.Height, size.Width * 2f, size.Height * 2f), format);
                    }
                    finally
                    {
                        font?.Dispose();
                    }
                }
            }
            return bitmap;
        }

        /// <summary>Fills every required name the folder lacks (see Refill).</summary>
        public void FillMissing(ICollection<string> saved, Random random, List<string> refilled, List<string> placeholders)
        {
            if (!System.IO.Directory.Exists(Directory))
                return;
            var present = new HashSet<string>(System.IO.Directory.GetFiles(Directory).Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);
            List<string> missing = NamesAuroraUses(saved)
                .Where(n => !present.Contains(n) && IsImage(n))
                .Select(n => n.ToLowerInvariant())
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            Refill(missing, saved, random, refilled, placeholders);
        }

        #endregion

        #region Replacing placeholders

        /// <summary>
        /// Swaps placeholders for unused same-type images, highest numbered first. Returns the count
        /// replaced; <paramref name="remaining"/> gets those left.
        /// </summary>
        public int ReplacePlaceholders(ICollection<string> saved, out int remaining)
        {
            List<string> placeholders = Images().Where(Placeholders.Is).ToList();
            remaining = placeholders.Count;
            if (placeholders.Count == 0)
                return 0;

            List<string> spares = Spares(placeholders, saved);
            int replaced = 0;
            // Required names first, so they get the real images.
            foreach (string placeholder in placeholders.OrderByDescending(f => IsRequired(f, saved)).ThenBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string spare = spares.FirstOrDefault(f => string.Equals(Path.GetExtension(f), Path.GetExtension(placeholder), StringComparison.OrdinalIgnoreCase));
                if (spare == null)
                    continue;
                spares.Remove(spare);
                try
                {
                    File.Delete(placeholder);
                    File.Move(spare, Lower(placeholder));
                    replaced++;
                    remaining--;
                    Log.Info(Title + ": deleted the placeholder " + Path.GetFileName(placeholder)
                        + " and renamed " + Path.GetFileName(spare) + " to take its name");
                }
                catch (Exception ex)
                {
                    Log.Error(Title + ": could not replace the placeholder " + Path.GetFileName(placeholder) + ": " + ex.Message);
                }
            }
            return replaced;
        }

        #endregion

        #region Randomizing

        private static bool SameExtension(string a, string b)
        {
            string x = a.ToLowerInvariant(), y = b.ToLowerInvariant();
            if (x == ".jpeg") x = ".jpg";
            if (y == ".jpeg") y = ".jpg";
            return x == y;
        }

        /// <summary>
        /// Converts images not in Format, except required names (which must stay exact). Keeps the name
        /// with the new extension, or the next free number if taken. Returns the count converted.
        /// </summary>
        public int ConvertToFormat(ICollection<string> saved)
        {
            Naming naming = DetectNaming();
            HashSet<int> used = UsedNumbers(naming);
            var present = new HashSet<string>(System.IO.Directory.GetFiles(Directory).Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);
            int converted = 0;
            foreach (string file in Images())
            {
                if (SameExtension(Path.GetExtension(file), Format) || IsRequired(file, saved) || Placeholders.Is(file))
                    continue;

                string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant() + Format;
                if (present.Contains(name))
                {
                    int next = 1;
                    while (used.Contains(next))
                        next++;
                    used.Add(next);
                    name = naming.Name(next, Format);
                }
                string target = Path.Combine(Directory, name);
                try
                {
                    using (var stream = new MemoryStream(File.ReadAllBytes(file)))
                    using (Image image = Image.FromStream(stream))
                        Save(image, target, Format);
                    File.Delete(file);
                    present.Remove(Path.GetFileName(file));
                    present.Add(name);
                    converted++;
                    Log.Info(Title + ": converted " + Path.GetFileName(file) + " to " + name);
                }
                catch (Exception ex)
                {
                    Log.Error(Title + ": could not convert " + Path.GetFileName(file) + " to " + Format + ": " + ex.Message);
                    try { if (File.Exists(target) && File.Exists(file)) File.Delete(target); } catch { }
                }
            }
            return converted;
        }

        /// <summary>
        /// Shuffles images so every name holds a different one. Unprotected wrong-type images are
        /// converted first; an image landing on a required name of another type is converted to it.
        /// The player default and placeholders stay put. Returns files changed.
        /// </summary>
        public int Randomize(Random random, ICollection<string> saved, out int converted)
        {
            converted = ConvertToFormat(saved);

            List<string> names = Images().Where(f => !IsPlayerDefault(f) && !Placeholders.Is(f)).ToList();
            if (names.Count < 2)
                return 0;

            // Sattolo's shuffle: a single cycle, so no file keeps its image.
            int[] from = Enumerable.Range(0, names.Count).ToArray();
            for (int i = from.Length - 1; i > 0; i--)
            {
                int j = random.Next(i);
                int swap = from[i];
                from[i] = from[j];
                from[j] = swap;
            }
            SendWrongTypesHome(names, from, random);

            // Move all aside (extension kept to know the type), then place. On failure, leftovers go
            // back under the empty names so every name exists again.
            string token = Guid.NewGuid().ToString("N");
            var temps = names.Select((n, i) => Path.Combine(Directory, "~atm-" + token + "-" + i + Path.GetExtension(n).ToLowerInvariant() + ".tmp")).ToList();
            List<string> targets = names.Select(Lower).ToList();
            var placed = new bool[names.Count];
            int movedAside = 0, changed = 0;
            try
            {
                for (; movedAside < names.Count; movedAside++)
                    File.Move(names[movedAside], temps[movedAside]);
                for (int i = 0; i < names.Count; i++)
                {
                    string source = temps[from[i]];
                    string sourceType = Path.GetExtension(names[from[i]]);
                    string targetType = Path.GetExtension(targets[i]);
                    if (SameExtension(sourceType, targetType))
                    {
                        File.Move(source, targets[i]);
                    }
                    else
                    {
                        using (var stream = new MemoryStream(File.ReadAllBytes(source)))
                        using (Image image = Image.FromStream(stream))
                            Save(image, targets[i], targetType);
                        File.Delete(source);
                        converted++;
                        Log.Info(Title + ": converted the image for " + Path.GetFileName(targets[i]) + " from " + sourceType + ", as that name is in use");
                    }
                    placed[from[i]] = true;
                    changed++;
                }
            }
            catch
            {
                var waiting = Enumerable.Range(0, movedAside).Where(i => !placed[i] && File.Exists(temps[i])).ToList();
                var empty = targets.Where(n => !File.Exists(n)).ToList();
                foreach (int i in waiting.ToList())
                {
                    if (empty.Contains(targets[i]) && TryMove(temps[i], targets[i]))
                    {
                        empty.Remove(targets[i]);
                        waiting.Remove(i);
                    }
                }
                for (int k = 0; k < waiting.Count && k < empty.Count; k++)
                    TryMove(temps[waiting[k]], empty[k]);
                throw;
            }
            Log.Info(Title + ": randomized " + changed + " images, converting " + converted);
            return changed;
        }

        /// <summary>
        /// Re-routes images leaving a wrong-type (required) name onto Format names, so they convert
        /// back instead of landing on another required name. Leftovers keep their type and are logged.
        /// </summary>
        private void SendWrongTypesHome(List<string> names, int[] from, Random random)
        {
            Func<int, bool> wrong = i => !SameExtension(Path.GetExtension(names[i]), Format);
            for (int i = 0; i < from.Length; i++)
            {
                if (!wrong(i) || !wrong(from[i]))
                    continue;

                // Right-type name with a right-type source, where swapping still moves every image.
                List<int> candidates = Enumerable.Range(0, from.Length)
                    .Where(j => j != i && !wrong(j) && !wrong(from[j]) && from[j] != i && from[i] != j)
                    .ToList();
                if (candidates.Count == 0)
                {
                    Log.Info(Title + ": " + Path.GetFileName(names[from[i]]) + "'s image keeps its type, as there is no "
                        + Format + " name left for it");
                    continue;
                }
                int k = candidates[random.Next(candidates.Count)];
                int swap = from[i];
                from[i] = from[k];
                from[k] = swap;
            }
        }

        private bool TryMove(string from, string to)
        {
            try
            {
                File.Move(from, to);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(Title + ": could not put " + from + " back as " + to + ": " + ex.Message);
                return false;
            }
        }

        #endregion
    }

    /// <summary>Placeholders ATM made, by content hash in ATM\Placeholders.txt, so renames don't lose them.</summary>
    internal static class Placeholders
    {
        private static HashSet<string> hashes;

        private static string FilePath
        {
            get { return Path.Combine(Program.DataDirectory, "Placeholders.txt"); }
        }

        private static HashSet<string> Hashes
        {
            get
            {
                if (hashes == null)
                {
                    hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    try
                    {
                        if (File.Exists(FilePath))
                            foreach (string line in File.ReadAllLines(FilePath))
                                if (line.Trim().Length > 0 && !line.StartsWith("#"))
                                    hashes.Add(line.Trim());
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Could not read " + FilePath + ": " + ex.Message);
                    }
                }
                return hashes;
            }
        }

        private static string Hash(string file)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var stream = File.OpenRead(file))
                return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        internal static void Remember(string file)
        {
            try
            {
                if (Hashes.Add(Hash(file)))
                    File.WriteAllLines(FilePath, new[] { "# Placeholders made by ATM, by MD5 of their contents" }
                        .Concat(Hashes.OrderBy(h => h)).ToArray());
            }
            catch (Exception ex)
            {
                Log.Error("Could not note the placeholder " + file + ": " + ex.Message);
            }
        }

        internal static bool Is(string file)
        {
            if (Hashes.Count == 0)
                return false;
            try
            {
                return Hashes.Contains(Hash(file));
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>ATM\Cache: game-icons.net icons in Cache\Icons, folder thumbnails in Cache\(folder).</summary>
    internal static class CacheFolders
    {
        internal static string Root
        {
            get { return Path.Combine(Program.DataDirectory, "Cache"); }
        }

        private static string Icons
        {
            get { return Path.Combine(Root, "Icons"); }
        }

        /// <summary>
        /// Moves older icon caches (ATM\IconCache, loose files in Cache) into Cache\Icons without
        /// replacing anything; duplicates are deleted.
        /// </summary>
        internal static void Migrate()
        {
            try
            {
                Directory.CreateDirectory(Root);

                string old = Path.Combine(Program.DataDirectory, "IconCache");
                if (Directory.Exists(old))
                {
                    if (!Directory.Exists(Icons))
                    {
                        Directory.Move(old, Icons);
                        Log.Info("Cache: moved " + old + " to " + Icons);
                    }
                    else
                    {
                        MoveContents(old, Icons);
                        DeleteIfEmpty(old);
                    }
                }

                // Anything in Cache's root but the image folders' subfolders is an old icon file.
                var keep = new HashSet<string>(ImageFolder.All.Select(f => f.Folder).Concat(new[] { "Icons" }), StringComparer.OrdinalIgnoreCase);
                bool stray = Directory.GetFiles(Root).Length > 0
                    || Directory.GetDirectories(Root).Any(d => !keep.Contains(Path.GetFileName(d)));
                if (stray)
                {
                    Directory.CreateDirectory(Icons);
                    foreach (string file in Directory.GetFiles(Root))
                        MoveFile(file, Path.Combine(Icons, Path.GetFileName(file)));
                    foreach (string dir in Directory.GetDirectories(Root).Where(d => !keep.Contains(Path.GetFileName(d))))
                    {
                        string target = Path.Combine(Icons, Path.GetFileName(dir));
                        if (!Directory.Exists(target))
                            Directory.Move(dir, target);
                        else
                        {
                            MoveContents(dir, target);
                            DeleteIfEmpty(dir);
                        }
                    }
                    Log.Info("Cache: moved icon files from " + Root + " into " + Icons);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Cache: could not move the old icon cache into " + Icons + ": " + ex.Message);
            }
        }

        private static void MoveContents(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from))
                MoveFile(file, Path.Combine(to, Path.GetFileName(file)));
            foreach (string dir in Directory.GetDirectories(from))
            {
                MoveContents(dir, Path.Combine(to, Path.GetFileName(dir)));
                DeleteIfEmpty(dir);
            }
        }

        private static void MoveFile(string from, string to)
        {
            if (File.Exists(to))
                File.Delete(from);
            else
                File.Move(from, to);
        }

        private static void DeleteIfEmpty(string dir)
        {
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
    }

    /// <summary>Image names saved games use, read-only from AuroraDB.db via the game's System.Data.SQLite.dll.</summary>
    internal static class SavedPictures
    {
        /// <summary>All values of the folder's SavedColumns. Throws if the database can't be read.</summary>
        internal static HashSet<string> Read(ImageFolder folder)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (folder.SavedColumns.Length == 0)
                return names;

            string dbPath = Path.Combine(AuroraConfig.GameDirectory(), "AuroraDB.db");
            if (!File.Exists(dbPath))
                return names;

            Assembly sqlite = Assembly.UnsafeLoadFrom(Path.Combine(AuroraConfig.GameDirectory(), "System.Data.SQLite.dll"));
            Type connectionType = sqlite.GetType("System.Data.SQLite.SQLiteConnection", true);
            using (var connection = (DbConnection)Activator.CreateInstance(connectionType, "Data Source=" + dbPath + ";Read Only=True;"))
            {
                connection.Open();
                foreach (string column in folder.SavedColumns)
                {
                    string[] parts = column.Split('.');
                    using (DbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT DISTINCT " + parts[1] + " FROM " + parts[0];
                        using (DbDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (reader.IsDBNull(0))
                                    continue;
                                string value = reader.GetValue(0).ToString().Trim();
                                if (value.Length > 0)
                                    names.Add(Path.GetFileName(value).ToLowerInvariant());
                            }
                        }
                    }
                }
            }
            return names;
        }
    }

    /// <summary>ATM\Backups: first-run copy of each image folder, plus replaced toolbar icons (Backups\Icons).</summary>
    internal static class ImageBackups
    {
        internal static string Root
        {
            get { return Path.Combine(Program.DataDirectory, "Backups"); }
        }

        /// <summary>Backs up folders that have no backup yet, and migrates the old ATM\IconBackups.</summary>
        internal static void Create()
        {
            MoveOldIconBackups();

            foreach (ImageFolder folder in ImageFolder.All)
            {
                try { System.IO.Directory.CreateDirectory(folder.ExtraDirectory); }
                catch (Exception ex) { Log.Error("Could not create " + folder.ExtraDirectory + ": " + ex.Message); }
            }

            foreach (ImageFolder folder in ImageFolder.All)
            {
                try
                {
                    if (System.IO.Directory.Exists(folder.BackupDirectory) || !System.IO.Directory.Exists(folder.Directory))
                        continue;
                    // Copy under a temp name and rename when done, so an interrupted copy isn't taken as complete.
                    string partial = folder.BackupDirectory + ".partial";
                    if (System.IO.Directory.Exists(partial))
                        System.IO.Directory.Delete(partial, true);
                    int copied = CopyTree(folder.Directory, partial);
                    System.IO.Directory.Move(partial, folder.BackupDirectory);
                    Log.Info("Backups: copied " + copied + " files from " + folder.Directory + " to " + folder.BackupDirectory);
                }
                catch (Exception ex)
                {
                    Log.Error("Backups: could not back up " + folder.Directory + ": " + ex.Message);
                }
            }
        }

        private static void MoveOldIconBackups()
        {
            string old = Path.Combine(Program.DataDirectory, "IconBackups");
            if (!System.IO.Directory.Exists(old))
                return;

            string target = IconCatalog.BackupDirectory();
            int moved = 0;
            try
            {
                foreach (string file in System.IO.Directory.GetFiles(old))
                {
                    string destination = Path.Combine(target, Path.GetFileName(file));
                    // One already there is the older original; keep it.
                    if (File.Exists(destination))
                        File.Delete(file);
                    else
                        File.Move(file, destination);
                    moved++;
                }
                if (!System.IO.Directory.EnumerateFileSystemEntries(old).Any())
                    System.IO.Directory.Delete(old);
                Log.Info("Backups: moved " + moved + " icon backups from " + old + " to " + target);
            }
            catch (Exception ex)
            {
                Log.Error("Backups: could not move " + old + " to " + target + ": " + ex.Message);
            }
        }

        /// <summary>ATM\Backups\Originals.zip: the image folders as ATM first found them.</summary>
        internal static string OriginalsZip
        {
            get { return Path.Combine(Root, "Originals.zip"); }
        }

        /// <summary>
        /// Zips every image folder once, on a background thread so startup isn't delayed. Written to a
        /// temp name and renamed when done, so an interrupted zip is just remade next time.
        /// </summary>
        internal static void CreateOriginalsZip()
        {
            string zipPath = OriginalsZip;
            if (File.Exists(zipPath))
                return;

            var thread = new System.Threading.Thread(() =>
            {
                string partial = zipPath + ".partial";
                try
                {
                    System.IO.Directory.CreateDirectory(Root);
                    if (File.Exists(partial))
                        File.Delete(partial);
                    int added = 0;
                    using (var stream = new FileStream(partial, FileMode.CreateNew))
                    using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
                    {
                        foreach (ImageFolder folder in ImageFolder.All)
                        {
                            if (!System.IO.Directory.Exists(folder.Directory))
                                continue;
                            foreach (string file in System.IO.Directory.GetFiles(folder.Directory, "*", SearchOption.AllDirectories))
                            {
                                string relative = file.Substring(folder.Directory.Length).TrimStart('\\', '/').Replace('\\', '/');
                                // PNG/JPEG are already compressed.
                                string type = Path.GetExtension(file).ToLowerInvariant();
                                var level = type == ".png" || type == ".jpg" || type == ".jpeg"
                                    ? System.IO.Compression.CompressionLevel.NoCompression
                                    : System.IO.Compression.CompressionLevel.Optimal;
                                System.IO.Compression.ZipArchiveEntry entry = zip.CreateEntry(folder.Folder + "/" + relative, level);
                                entry.LastWriteTime = File.GetLastWriteTime(file);
                                using (Stream source = File.OpenRead(file))
                                using (Stream target = entry.Open())
                                    source.CopyTo(target);
                                added++;
                            }
                        }
                    }
                    File.Move(partial, zipPath);
                    Log.Info("Backups: made " + zipPath + " with " + added + " files");
                }
                catch (Exception ex)
                {
                    Log.Error("Backups: could not make " + zipPath + ": " + ex.Message);
                    try { if (File.Exists(partial)) File.Delete(partial); } catch { }
                }
            });
            thread.IsBackground = true;
            thread.Priority = System.Threading.ThreadPriority.BelowNormal;
            thread.Name = "Originals.zip";
            thread.Start();
        }

        internal static int CopyTree(string source, string target)
        {
            int copied = 0;
            System.IO.Directory.CreateDirectory(target);
            foreach (string file in System.IO.Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), false);
                copied++;
            }
            foreach (string dir in System.IO.Directory.GetDirectories(source))
                copied += CopyTree(dir, Path.Combine(target, Path.GetFileName(dir)));
            return copied;
        }
    }
}
