using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace AuroraThemeManager
{
    public partial class Settings
    {
        #region Window colors from the theme

        // The designer's colors, each with the role it plays in the window.
        private static readonly Color DesignBackground = Color.Black;
        private static readonly Color DesignText = Color.White;
        private static readonly Color DesignPanel = Color.FromArgb(24, 24, 24);
        private static readonly Color DesignButton = Color.FromArgb(48, 48, 48);

        private sealed class DesignColors
        {
            public Color Back, Fore, Border;
        }

        // Each control's designer colors, kept so a theme is always applied to them and never to the
        // colors the previous theme left.
        private readonly ConditionalWeakTable<Control, DesignColors> designColors = new ConditionalWeakTable<Control, DesignColors>();

        // The background and text the window was last colored with, so an edit to some other color
        // does not color it again.
        private int? themedBack, themedText;

        /// <summary>The theme's Standard Text color, which the window's text is drawn in.</summary>
        private Color ThemeText
        {
            get { return config.ColorOr("StandardTextColour", DesignText); }
        }

        /// <summary>
        /// Colors this window like the theme being edited: the Background color behind everything, the
        /// Standard Text color for text and button borders, and the darker panels and button faces as
        /// shades of the background, a step and two steps away from it (lighter on a dark background,
        /// darker on a light one).  Called when the window opens, after a theme is loaded, and after
        /// each change, so it follows the Background and Standard Text swatches as they are edited.
        ///
        /// The color swatches and the theme preview show the theme's own colors and are left alone, as
        /// are the icon list and grid and the icon boxes, whose white icons are drawn on a fixed dark
        /// backing.  Other colors the designer gave (grey notes, warnings) keep their meaning.
        /// </summary>
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

                if (control.HasChildren)
                    ThemeChildren(control, map);
            }
        }

        private bool KeepsOwnColors(Control control)
        {
            var button = control as Button;
            if (button != null && button.Tag is AuroraConfig.ColorEntry)
                return true;
            return control == preview || control is ThemePreview || control is IconGrid
                || control == lvAuroraIcons || control == picCurrent || control == picChosen;
        }

        // A color moved toward white on a dark background, or toward black on a light one.
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
