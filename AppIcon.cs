using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AuroraThemeManager
{
    internal static class AppIcon
    {
        private const string ResourceName = "AuroraThemeManager.Resources.Icons.aurora-4x-icon.ico";

        private static readonly object gate = new object();
        private static bool loaded;
        private static Icon icon;

        internal static Icon Get()
        {
            lock (gate)
            {
                if (!loaded)
                {
                    loaded = true;
                    try
                    {
                        using (Stream s = typeof(AppIcon).Assembly.GetManifestResourceStream(ResourceName))
                        {
                            if (s != null)
                                icon = new Icon(s);
                            else
                                Log.Error("Window icon: " + ResourceName + " is not embedded");
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("Window icon: " + e.Message);
                    }
                }
                return icon;
            }
        }

        internal static bool ApplyTo(Form form)
        {
            Icon i = Get();
            if (i == null)
                return false;
            form.Icon = i;
            form.ShowIcon = true;
            return true;
        }
    }
}
