using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace AuroraThemeManager
{
    internal static class Program
    {
        internal const string AppName = "Aurora Theme Manager";

        [STAThread]
        private static int Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string auroraPath = Path.Combine(ToolDirectory, "Aurora.exe");
            if (!File.Exists(auroraPath))
            {
                MessageBox.Show(
                    "ATM.exe must be in the same folder as Aurora.exe.\n\n"
                    + "There is no Aurora.exe in " + ToolDirectory + ".",
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            Log.Info("Starting " + Assembly.GetExecutingAssembly().GetName().Version);

            try
            {
                CreateDataFolders();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not create the ATM folder in " + ToolDirectory + ".\n\n" + ex.Message,
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            string configPath = Path.Combine(ToolDirectory, AuroraConfig.FileName);
            if (!File.Exists(configPath))
            {
                try
                {
                    AuroraConfig.CreateDefault(configPath);
                    Log.Info("No " + AuroraConfig.FileName + " in " + ToolDirectory + "; created it from the " + AuroraDefaults.DefaultName + " theme");
                }
                catch (Exception ex)
                {
                    Log.Error("Could not create " + configPath + ": " + ex);
                    MessageBox.Show("There is no " + AuroraConfig.FileName + " in " + ToolDirectory + ", and it could not be created.\n\n" + ex.Message,
                        AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
            }

            try
            {
                Settings.LoadSettings();
                using (var form = new Settings(AuroraConfig.Load()))
                    Application.Run(form);
                return 0;
            }
            catch (Exception ex)
            {
                Log.Error("Unhandled: " + ex);
                MessageBox.Show("Error: " + ex.Message + "\n\n" + ex.StackTrace, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        internal static string ToolDirectory
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
        }

        /// <summary>
        /// Everything ATM keeps lives in an ATM folder beside the exe, so only ATM.exe has to be shipped.
        /// The folders are made on first run: Presets for saved themes, IconBackups for the original
        /// toolbar icons and IconCache for downloaded game-icons.net icons.
        /// </summary>
        private static void CreateDataFolders()
        {
            bool firstRun = !Directory.Exists(Path.Combine(ToolDirectory, "ATM", "Presets"));
            Settings.GetPresetsDirectory();
            IconCatalog.BackupDirectory();
            IconCatalog.CacheDirectory();
            if (!firstRun)
                return;
            Log.Info("First run: created " + DataDirectory);
            BackUpExistingConfig();
        }

        /// <summary>
        /// On first run, an AuroraConfig.xml that is not Aurora's default is copied into Presets as
        /// "Backup - " and the time, so the player's own colors are kept as a theme before ATM changes
        /// anything.  The time is written like ISO 8601 but with dashes for the colons, which Windows file
        /// names cannot hold, and an underscore for the T.
        /// </summary>
        private static void BackUpExistingConfig()
        {
            string configPath = Path.Combine(ToolDirectory, AuroraConfig.FileName);
            try
            {
                if (!File.Exists(configPath) || AuroraConfig.MatchesDefault(configPath))
                    return;
                string name = "Backup - " + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture);
                string target = Settings.PresetPath(name);
                File.Copy(configPath, target, false);
                Log.Info("First run: " + AuroraConfig.FileName + " differs from the default; copied it to " + target);
            }
            catch (Exception ex)
            {
                Log.Error("First run: could not back up " + configPath + ": " + ex.Message);
            }
        }

        internal static string DataDirectory
        {
            get
            {
                string dir = Path.Combine(ToolDirectory, "ATM");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }
    }

    internal static class Log
    {
        private static readonly object Gate = new object();
        private static bool started;

        internal static void Info(string message)
        {
            Write("INFO ", message);
        }

        internal static void Error(string message)
        {
            Write("ERROR", message);
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    string path = Path.Combine(Program.DataDirectory, "ATM.log");
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + level + " " + message + Environment.NewLine;
                    if (started)
                        File.AppendAllText(path, line, Encoding.UTF8);
                    else
                        File.WriteAllText(path, line, Encoding.UTF8);
                    started = true;
                }
            }
            catch
            {
            }
        }
    }
}
