using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace AuroraThemeManager
{
    public partial class Settings
    {
        #region Window colors from the theme

        private static readonly Color DesignBackground = Color.Black;
        private static readonly Color DesignText = Color.White;
        private static readonly Color DesignPanel = Color.FromArgb(24, 24, 24);
        private static readonly Color DesignButton = Color.FromArgb(48, 48, 48);

        private sealed class DesignColors
        {
            public Color Back, Fore, Border;
        }

        private readonly ConditionalWeakTable<Control, DesignColors> designColors = new ConditionalWeakTable<Control, DesignColors>();

        private int? themedBack, themedText;

        private Color ThemeText
        {
            get { return config.ColorOr("StandardTextColour", DesignText); }
        }

        private void ApplyWindowTheme()
        {
            Color back = config.ColorOr("Background", DesignBackground);
            Color text = ThemeText;
            if (themedBack == back.ToArgb() && themedText == text.ToArgb())
                return;
            themedBack = back.ToArgb();
            themedText = text.ToArgb();
            var map = new Dictionary<int, Color>
            {
                { DesignBackground.ToArgb(), back },
                { DesignText.ToArgb(), text },
                { DesignPanel.ToArgb(), Shade(back, 0.09f) },
                { DesignButton.ToArgb(), Shade(back, 0.19f) },
            };

            SuspendLayout();
            try
            {
                BackColor = back;
                ForeColor = text;
                ThemeChildren(this, map);
            }
            finally
            {
                ResumeLayout(false);
            }
            Invalidate(true);
        }

        private void ThemeChildren(Control parent, Dictionary<int, Color> map)
        {
            foreach (Control control in parent.Controls)
            {
                if (KeepsOwnColors(control))
                    continue;

                DesignColors design;
                if (!designColors.TryGetValue(control, out design))
                {
                    var button = control as ButtonBase;
                    design = new DesignColors
                    {
                        Back = control.BackColor,
                        Fore = control.ForeColor,
                        Border = button != null ? button.FlatAppearance.BorderColor : Color.Empty,
                    };
                    designColors.Add(control, design);
                }

                Color color;
                if (map.TryGetValue(design.Back.ToArgb(), out color))
                    control.BackColor = color;
                if (map.TryGetValue(design.Fore.ToArgb(), out color))
                    control.ForeColor = color;
                var flat = control as ButtonBase;
                if (flat != null && !design.Border.IsEmpty && map.TryGetValue(design.Border.ToArgb(), out color))
                    flat.FlatAppearance.BorderColor = color;

                var spinner = control as UpDownBase;
                if (spinner != null)
                {
                    spinner.Paint -= FillSpinnerBackground;
                    spinner.Paint += FillSpinnerBackground;
                }

                if (control.HasChildren)
                    ThemeChildren(control, map);
            }
        }

        private static void FillSpinnerBackground(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(((Control)sender).BackColor);
        }

        private bool KeepsOwnColors(Control control)
        {
            var button = control as Button;
            if (button != null && button.Tag is AuroraConfig.ColorEntry)
                return true;
            return control == preview || control is ThemePreview || control is IconGrid
                || control == lvAuroraIcons || control == picCurrent || control == picChosen;
        }

        private static Color Shade(Color color, float amount)
        {
            int brightness = (color.R * 299 + color.G * 587 + color.B * 114) / 1000;
            Color toward = brightness < 128 ? Color.White : Color.Black;
            return Color.FromArgb(
                (int)(color.R + (toward.R - color.R) * amount),
                (int)(color.G + (toward.G - color.G) * amount),
                (int)(color.B + (toward.B - color.B) * amount));
        }

        #endregion
    }
}
