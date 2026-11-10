using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AuroraThemeManager
{
    internal sealed class GameIcon
    {
        public string Author;
        public string Slug;

        public string Title
        {
            get
            {
                string text = Slug.Replace('-', ' ');
                return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
            }
        }

        public string Key { get { return Author + "/" + Slug; } }
        public string PageUrl { get { return "https://game-icons.net/1x1/" + Author + "/" + Slug + ".html"; } }

        public string SvgUrl { get { return "https://game-icons.net/icons/ffffff/transparent/1x1/" + Author + "/" + Slug + ".svg"; } }

        public string RawUrl { get { return "https://raw.githubusercontent.com/game-icons/icons/master/" + Author + "/" + Slug + ".svg"; } }
    }

    internal static class IconCatalog
    {
        private const string TreeApiUrl = "https://api.github.com/repos/game-icons/icons/git/trees/master?recursive=1";
        private static readonly TimeSpan IndexMaxAge = TimeSpan.FromDays(1);
        private static readonly Regex PathPattern = new Regex("\"path\"\\s*:\\s*\"(?<a>[A-Za-z0-9_\\-]+)/(?<s>[A-Za-z0-9_\\-]+)\\.svg\"", RegexOptions.Compiled);

        private static readonly object Gate = new object();
        private static List<GameIcon> index;

        #region Folders

        internal static string CacheDirectory()
        {
            string dir = Path.Combine(Program.DataDirectory, "IconCache");
            Directory.CreateDirectory(dir);
            return dir;
        }

        internal static string BackupDirectory()
        {
            string dir = Path.Combine(Program.DataDirectory, "IconBackups");
            Directory.CreateDirectory(dir);
            return dir;
        }

        internal static string IconsDirectory()
        {
            return Path.Combine(AuroraConfig.GameDirectory(), "Icons");
        }

        #endregion

        #region Index and search

        internal static List<GameIcon> LoadIndex(bool forceRefresh = false)
        {
            lock (Gate)
            {
                if (index != null && !forceRefresh)
                    return index;

                string cachePath = Path.Combine(CacheDirectory(), "index.txt");
                bool haveCache = File.Exists(cachePath);
                bool fresh = haveCache && DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath) < IndexMaxAge;

                if (haveCache && fresh && !forceRefresh)
                {
                    index = ReadIndexFile(cachePath);
                    if (index.Count > 0)
                        return index;
                }

                try
                {
                    string json = Encoding.UTF8.GetString(Download(TreeApiUrl));
                    List<GameIcon> parsed = ParseTree(json);
                    if (parsed.Count == 0)
                        throw new InvalidDataException("The icon list from GitHub had no icons in it.");

                    File.WriteAllLines(cachePath, parsed.Select(i => i.Key).ToArray());
                    index = parsed;
                    return index;
                }
                catch
                {
                    if (haveCache)
                    {
                        index = ReadIndexFile(cachePath);
                        if (index.Count > 0)
                            return index;
                    }
                    throw;
                }
            }
        }

        internal static List<GameIcon> ParseTree(string json)
        {
            var result = new List<GameIcon>();
            var seen = new HashSet<string>();
            foreach (Match match in PathPattern.Matches(json))
            {
                var icon = new GameIcon { Author = match.Groups["a"].Value, Slug = match.Groups["s"].Value };
                if (seen.Add(icon.Key))
                    result.Add(icon);
            }
            return result;
        }

        private static List<GameIcon> ReadIndexFile(string path)
        {
            var result = new List<GameIcon>();
            foreach (string line in File.ReadAllLines(path))
            {
                int slash = line.IndexOf('/');
                if (slash > 0 && slash < line.Length - 1)
                    result.Add(new GameIcon { Author = line.Substring(0, slash), Slug = line.Substring(slash + 1) });
            }
            return result;
        }

        internal static List<GameIcon> Search(List<GameIcon> all, string query)
        {
            string[] words = (query ?? "").ToLowerInvariant()
                .Split(new[] { ' ', ',', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return new List<GameIcon>();

            return all
                .Where(icon => words.All(w => icon.Slug.Contains(w) || icon.Author.Contains(w)))
                .OrderBy(icon => icon.Slug.StartsWith(words[0], StringComparison.Ordinal) ? 0
                    : icon.Slug.Split('-').Contains(words[0]) ? 1 : 2)
                .ThenBy(icon => icon.Slug, StringComparer.Ordinal)
                .ToList();
        }

        #endregion

        #region Downloading

        internal static string GetSvg(GameIcon icon)
        {
            string cachePath = Path.Combine(CacheDirectory(), icon.Author + "__" + icon.Slug + ".svg");
            if (File.Exists(cachePath))
                return File.ReadAllText(cachePath, Encoding.UTF8);

            string svg;
            try
            {
                svg = Encoding.UTF8.GetString(Download(icon.SvgUrl));
            }
            catch
            {
                svg = Encoding.UTF8.GetString(Download(icon.RawUrl));
            }

            svg = Normalize(svg);
            if (svg.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidDataException("The download for " + icon.Key + " was not an SVG file.");

            WriteAtomic(cachePath, new UTF8Encoding(false).GetBytes(svg));
            return svg;
        }

        internal static string Normalize(string svg)
        {
            svg = Regex.Replace(svg,
                "<path\\b[^>]*\\bd=\"M0 0h512v512H0z\"[^>]*?(/>|></path>|>)",
                "<path d=\"M0 0h512v512H0z\" fill=\"#00001b\" fill-opacity=\"0\"></path>",
                RegexOptions.IgnoreCase);
            svg = Regex.Replace(svg, "fill=\"#fff\"", "fill=\"#ffffff\"", RegexOptions.IgnoreCase);
            return svg;
        }

        private static byte[] Download(string url)
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "AuroraThemeManager/1.0";
            request.Timeout = 20000;
            request.ReadWriteTimeout = 20000;
            request.Accept = "*/*";

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                return buffer.ToArray();
            }
        }

        #endregion

        #region Cache and sync

        internal static event Action SyncChanged;
        internal static volatile string SyncStatus = "";
        internal static volatile bool Syncing;

        private static readonly object SyncGate = new object();
        private static DateTime lastRaise = DateTime.MinValue;

        private static void SetStatus(string text, bool force = false)
        {
            SyncStatus = text;
            DateTime now = DateTime.UtcNow;
            if (!force && (now - lastRaise).TotalMilliseconds < 400)
                return;
            lastRaise = now;
            try { Action handler = SyncChanged; if (handler != null) handler(); } catch { }
        }

        private static string SvgCachePath(GameIcon icon)
        {
            return Path.Combine(CacheDirectory(), icon.Author + "__" + icon.Slug + ".svg");
        }

        private static HashSet<string> CachedKeys()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(CacheDirectory(), "*.svg"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                int split = name.IndexOf("__", StringComparison.Ordinal);
                if (split > 0 && split < name.Length - 2)
                    keys.Add(name.Substring(0, split) + "/" + name.Substring(split + 2));
            }
            return keys;
        }

        internal static string CacheSummary()
        {
            try
            {
                int cached = CachedKeys().Count;
                List<GameIcon> list = TryLoadCachedIndex();
                if (cached == 0)
                    return "No icons cached yet.";
                if (list != null && list.Count > 0)
                    return cached + " of " + list.Count + " icons cached.";
                return cached + " icons cached.";
            }
            catch
            {
                return "";
            }
        }

        private static void WriteAtomic(string path, byte[] data)
        {
            string temp = path + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            File.WriteAllBytes(temp, data);
            try
            {
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch
            {
                try { File.Delete(temp); } catch { }
                throw;
            }
        }

        internal static List<GameIcon> TryLoadCachedIndex()
        {
            lock (Gate)
            {
                if (index != null && index.Count > 0)
                    return index;
                string cachePath = Path.Combine(CacheDirectory(), "index.txt");
                if (!File.Exists(cachePath))
                    return null;
                List<GameIcon> list = ReadIndexFile(cachePath);
                if (list.Count == 0)
                    return null;
                index = list;
                return index;
            }
        }

        internal static List<GameIcon> Filter(List<GameIcon> all, string query)
        {
            if (all == null)
                return new List<GameIcon>();
            if (string.IsNullOrWhiteSpace(query))
                return all.OrderBy(i => i.Slug, StringComparer.OrdinalIgnoreCase).ToList();
            return Search(all, query);
        }

        internal static void StartBackgroundSync(bool forceIndexRefresh, Action<string> log)
        {
            lock (SyncGate)
            {
                if (Syncing)
                    return;
                Syncing = true;
            }

            var thread = new Thread(() =>
            {
                try
                {
                    RunSync(forceIndexRefresh, log);
                }
                catch (Exception e)
                {
                    SetStatus("The icon cache could not be updated: " + e.Message, true);
                    if (log != null) log("Icons: cache update failed: " + e.Message);
                }
                finally
                {
                    Syncing = false;
                    SetStatus(SyncStatus, true);
                }
            });
            thread.IsBackground = true;
            thread.Priority = ThreadPriority.BelowNormal;
            thread.Name = "Icon cache";
            thread.Start();
        }

        private static void RunSync(bool forceIndexRefresh, Action<string> log)
        {
            SetStatus("Checking game-icons.net for icons...", true);
            List<GameIcon> all = LoadIndex(forceIndexRefresh);
            SetStatus(SyncStatus, true);

            HashSet<string> have = CachedKeys();
            List<GameIcon> missing = all.Where(i => !have.Contains(i.Key)).ToList();
            if (missing.Count == 0)
            {
                SetStatus("All " + all.Count + " icons are cached.", true);
                return;
            }

            if (log != null) log("Icons: " + missing.Count + " of " + all.Count + " icons are not cached yet");

            if (missing.Count > 200)
            {
                try
                {
                    DownloadArchive(missing, all.Count, log);
                }
                catch (Exception e)
                {
                    if (log != null) log("Icons: the archive download failed, falling back to single icons: " + e.Message);
                }
                have = CachedKeys();
                missing = all.Where(i => !have.Contains(i.Key)).ToList();
            }

            if (missing.Count > 0)
            {
                int done = 0;
                int failures = 0;
                int total = all.Count;
                int cachedAtStart = total - missing.Count;
                System.Threading.Tasks.Parallel.ForEach(missing,
                    new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 },
                    (icon, state) =>
                    {
                        if (Thread.VolatileRead(ref failures) > 25)
                        {
                            state.Stop();
                            return;
                        }
                        try
                        {
                            GetSvg(icon);
                            int n = Interlocked.Increment(ref done);
                            SetStatus("Downloading new icons: " + (cachedAtStart + n) + " of " + total);
                        }
                        catch
                        {
                            Interlocked.Increment(ref failures);
                        }
                    });
            }

            have = CachedKeys();
            int stillMissing = all.Count(i => !have.Contains(i.Key));
            SetStatus(stillMissing == 0
                ? "All " + all.Count + " icons are cached."
                : have.Count + " of " + all.Count + " icons are cached.  Press Check for updated icons to try the rest again.", true);
        }

        private const string ArchiveUrl = "https://github.com/game-icons/icons/archive/refs/heads/master.zip";

        private static void DownloadArchive(List<GameIcon> missing, int total, Action<string> log)
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }

            string zipPath = Path.Combine(CacheDirectory(), "archive.zip.tmp");
            try
            {
                SetStatus("Downloading the icon set (first run only)...", true);
                var request = (HttpWebRequest)WebRequest.Create(ArchiveUrl);
                request.UserAgent = "AuroraThemeManager/1.0";
                request.Timeout = 60000;
                request.ReadWriteTimeout = 60000;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var input = response.GetResponseStream())
                using (var output = File.Create(zipPath))
                {
                    long length = response.ContentLength;
                    var buffer = new byte[81920];
                    long read = 0;
                    int n;
                    while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, n);
                        read += n;
                        SetStatus("Downloading the icon set: " + (read / 1048576) + (length > 0 ? " of " + (length / 1048576) : "") + " MB");
                    }
                }

                var wanted = new HashSet<string>(missing.Select(i => i.Key), StringComparer.Ordinal);
                int extracted = 0;
                int have = total - missing.Count;
                using (var zip = new System.IO.Compression.ZipArchive(File.OpenRead(zipPath), System.IO.Compression.ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        Match m = ArchiveEntry.Match(entry.FullName);
                        if (!m.Success)
                            continue;
                        string key = m.Groups["a"].Value + "/" + m.Groups["s"].Value;
                        if (!wanted.Contains(key))
                            continue;

                        string svg;
                        using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                            svg = Normalize(reader.ReadToEnd());
                        if (svg.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        string path = Path.Combine(CacheDirectory(), m.Groups["a"].Value + "__" + m.Groups["s"].Value + ".svg");
                        WriteAtomic(path, new UTF8Encoding(false).GetBytes(svg));
                        extracted++;
                        SetStatus("Unpacking icons: " + (have + extracted) + " of " + total);
                    }
                }
                if (log != null) log("Icons: unpacked " + extracted + " icons from the archive");
            }
            finally
            {
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
            }
        }

        private static readonly Regex ArchiveEntry = new Regex("^[^/]+/(?<a>[A-Za-z0-9_\\-]+)/(?<s>[A-Za-z0-9_\\-]+)\\.svg$", RegexOptions.Compiled);

        internal static Bitmap GetThumb(GameIcon icon, int size)
        {
            string dir = Path.Combine(CacheDirectory(), "thumbs");
            Directory.CreateDirectory(dir);
            string png = Path.Combine(dir, icon.Author + "__" + icon.Slug + "_" + size + ".png");

            if (File.Exists(png))
            {
                try
                {
                    using (var stream = File.OpenRead(png))
                    using (var image = Image.FromStream(stream))
                        return new Bitmap(image);
                }
                catch { }
            }

            string svgPath = SvgCachePath(icon);
            if (!File.Exists(svgPath))
                return null;

            Bitmap bmp = Render(File.ReadAllText(svgPath, Encoding.UTF8), size);
            if (bmp == null)
                return null;

            try
            {
                string temp = png + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
                bmp.Save(temp, System.Drawing.Imaging.ImageFormat.Png);
                if (File.Exists(png)) File.Delete(png);
                File.Move(temp, png);
            }
            catch { }
            return bmp;
        }

        #endregion

        #region Rendering

        private static MethodInfo openMethod;
        private static MethodInfo drawMethod;
        private static bool rendererResolved;

        internal static Action<string> Log;
        private static readonly HashSet<string> reported = new HashSet<string>();

        private static void Report(string message)
        {
            try
            {
                if (Log != null && reported.Add(message))
                    Log("Icons: " + message);
            }
            catch { }
        }

        private static readonly object RenderGate = new object();

        internal static Bitmap Render(string svg, int size)
        {
            return Render(svg, size, size);
        }

        internal static Bitmap Render(string svg, int width, int height)
        {
            lock (RenderGate)
                return RenderLocked(svg, width, height);
        }

        private static Bitmap RenderLocked(string svg, int width, int height)
        {
            try
            {
                if (!rendererResolved)
                    ResolveRenderer();
                if (openMethod == null || drawMethod == null)
                    return null;

                object document;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg)))
                    document = openMethod.Invoke(null, new object[] { stream });

                try
                {
                    var bmp = (Bitmap)drawMethod.Invoke(document, new object[] { width, height });
                    if (bmp != null)
                        return bmp;
                }
                catch (Exception e)
                {
                    Report("Draw(int, int) failed: " + Unwrap(e) + "; trying a sized bitmap");
                }

                Type type = document.GetType();
                var result = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                MethodInfo drawInto = type.GetMethod("Draw", new[] { typeof(Bitmap) });
                if (drawInto == null)
                {
                    result.Dispose();
                    return null;
                }
                drawInto.Invoke(document, new object[] { result });
                return result;
            }
            catch (Exception e)
            {
                Report("render failed: " + Unwrap(e));
                return null;
            }
        }

        private static string Unwrap(Exception e)
        {
            while (e.InnerException != null)
                e = e.InnerException;
            return e.GetType().Name + ": " + e.Message;
        }

        private static bool resolverInstalled;

        private static void InstallAssemblyResolver()
        {
            if (resolverInstalled)
                return;
            resolverInstalled = true;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string name = new AssemblyName(args.Name).Name;
                Assembly loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == name);
                if (loaded != null)
                    return loaded;
                string path = Path.Combine(AuroraConfig.GameDirectory(), name + ".dll");
                if (!File.Exists(path))
                    return null;
                try
                {
                    return Assembly.UnsafeLoadFrom(path);
                }
                catch (Exception e)
                {
                    Report("could not load " + path + ": " + Unwrap(e));
                    return null;
                }
            };
        }

        private static void ResolveRenderer()
        {
            rendererResolved = true;
            try
            {
                InstallAssemblyResolver();
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Svg");
                if (assembly == null)
                {
                    try
                    {
                        assembly = Assembly.Load(new AssemblyName("Svg"));
                    }
                    catch (Exception e)
                    {
                        Report("Svg.dll did not load from the application folder: " + Unwrap(e));
                    }
                }
                if (assembly == null)
                {
                    string path = Path.Combine(AuroraConfig.GameDirectory(), "Svg.dll");
                    Report("loading " + path + " (exists: " + File.Exists(path) + ")");
                    assembly = Assembly.UnsafeLoadFrom(path);
                }

                Type type = assembly.GetType("Svg.SvgDocument");
                if (type == null)
                {
                    Report("Svg.SvgDocument not found in " + assembly.FullName);
                    return;
                }

                MethodInfo generic = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Open" && m.IsGenericMethodDefinition
                        && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(Stream));
                if (generic != null)
                    openMethod = generic.MakeGenericMethod(type);
                else
                    openMethod = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "Open" && !m.IsGenericMethod
                            && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(Stream));
                drawMethod = type.GetMethod("Draw", new[] { typeof(int), typeof(int) });
                Report("Svg.dll " + assembly.GetName().Version + ", Open found: " + (openMethod != null) + ", Draw found: " + (drawMethod != null));
            }
            catch (Exception e)
            {
                Report("could not load Svg.dll: " + Unwrap(e));
                openMethod = null;
                drawMethod = null;
            }
        }

        #endregion
    }
}
