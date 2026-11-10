using System;
using System.Drawing;
using System.Windows.Forms;

namespace AuroraThemeManager
{
    internal sealed class PreviewForm : Form
    {
        private readonly ThemePreview preview;
        private readonly TabControl tabs;

        internal PreviewForm(ThemePreview preview)
        {
            this.preview = preview;
            Text = Program.AppName + " - Preview  (hover for details, click to change a color)";
            BackColor = Color.Black;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.Manual;

            tabs = new TabControl { Dock = DockStyle.Fill };
            AddPage("Game Windows", PreviewMode.GameWindows);
            AddPage("Tactical Map", PreviewMode.TacticalMap);
            AddPage("Galactic Map", PreviewMode.GalacticMap);
            tabs.SelectedIndexChanged += (s, e) => ShowSelected();
            Shown += (s, e) => ShowSelected();
            Controls.Add(tabs);

            preview.Dock = DockStyle.Fill;
            ShowSelected();

            Load += (s, e) => AppIcon.ApplyTo(this);
            FormClosing += (s, e) =>
            {
                if (preview.Parent != null)
                    preview.Parent.Controls.Remove(preview);
            };
        }

        private void AddPage(string title, PreviewMode mode)
        {
            tabs.TabPages.Add(new TabPage(title) { Tag = mode, BackColor = Color.Black, Padding = Padding.Empty });
        }

        private void ShowSelected()
        {
            TabPage page = tabs.SelectedTab;
            if (page == null)
                return;
            var mode = (PreviewMode)page.Tag;
            preview.Mode = mode;
            if (preview.Parent != page)
            {
                if (preview.Parent != null)
                    preview.Parent.Controls.Remove(preview);
                page.Controls.Add(preview);
            }

            preview.Dock = DockStyle.Fill;
        }
    }
}
