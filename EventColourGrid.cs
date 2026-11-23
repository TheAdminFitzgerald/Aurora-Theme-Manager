using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AuroraThemeManager
{
    /// <summary>
    /// Event types in a group box per category, alphabetically down four columns, each drawn in its own
    /// colours.  Click, Ctrl+Click and Shift+Click select; Ctrl+A selects all; Esc clears.
    /// </summary>
    [ToolboxItem(true)]
    [Designer(typeof(EventColourGridDesigner))]
    public sealed class EventColourGrid : Control
    {
        private const int Columns = 4;
        private const int Gap = 6;          // between columns and boxes, and around the edge
        private const int RowGap = 2;
        private const int BoxPad = 6;       // inside each box, left, right and bottom

        private readonly VScrollBar bar = new VScrollBar { Dock = DockStyle.Right, TabStop = false };
        private readonly Canvas canvas = new Canvas();

        private List<EventType> items = new List<EventType>();
        private Func<int, EventColour> colourOf = id => null;

        private readonly List<GroupBox> boxes = new List<GroupBox>();
        private readonly List<EventLabel> labels = new List<EventLabel>();   // in the order shown

        private readonly HashSet<int> selected = new HashSet<int>();
        private EventLabel hotLabel;    // the one label under the mouse, outlined
        private int anchorId = -1;
        private int rowHeight = 20;

        internal Color HotColor = Color.FromArgb(90, 150, 230);
        internal Color SelectionColor = Color.FromArgb(255, 200, 60);

        /// <summary>The selected events changed.</summary>
        internal event Action SelectionChanged;

        /// <summary>An event was right-clicked.</summary>
        internal event Action<EventType, Point> CellMenu;

        /// <summary>A category box was right-clicked.</summary>
        internal event Action<EventCategory, List<EventType>, Point> HeadingMenu;

        public EventColourGrid()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            bar.ValueChanged += (s, e) => canvas.Top = -bar.Value;
            Controls.Add(canvas);
            Controls.Add(bar);
            canvas.MouseUp += (s, e) => BackgroundClicked(e);
            canvas.MouseMove += (s, e) => SetHot(null);
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                ShowDesignSample();
        }

        /// <summary>Fills the grid with sample data in the Visual Studio designer.</summary>
        private void ShowDesignSample()
        {
            try
            {
                EventColourSet sample = EventColourSet.DesignSample();
                SetItems(EventColourSet.BuiltInTypeList(), id => sample.Get(id));
            }
            catch
            {
            }
        }

        /// <summary>The event types shown, and where their colours come from.</summary>
        internal void SetItems(List<EventType> list, Func<int, EventColour> colours)
        {
            items = list ?? new List<EventType>();
            colourOf = colours ?? (id => null);
            selected.IntersectWith(items.Select(t => t.Id));
            Build();
            bar.Value = 0;
        }

        /// <summary>Redraws every event, after its colours change.</summary>
        internal void RefreshColours()
        {
            foreach (EventLabel label in labels)
                label.Invalidate();
        }

        #region Building and layout

        private void Build()
        {
            canvas.SuspendLayout();
            try
            {
                hotLabel = null;
                foreach (GroupBox box in boxes)
                    box.Dispose();
                boxes.Clear();
                labels.Clear();
                canvas.Controls.Clear();

                rowHeight = Math.Max(18, TextRenderer.MeasureText("Ag", Font).Height + 4);
                IEnumerable<IGrouping<EventCategory, EventType>> groups = items
                    .GroupBy(t => t.Category)
                    .OrderBy(g => g.Key.Name, StringComparer.OrdinalIgnoreCase);
                foreach (IGrouping<EventCategory, EventType> group in groups)
                {
                    List<EventType> types = group.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    var box = new GroupBox
                    {
                        Text = group.Key.Name,
                        Tag = group.Key,
                        ForeColor = ForeColor,
                        BackColor = BackColor,
                        Font = Font,
                    };
                    EventCategory category = group.Key;
                    box.MouseMove += (s, e) => SetHot(null);
                    box.MouseUp += (s, e) =>
                    {
                        if (e.Button == MouseButtons.Right)
                            HeadingMenu?.Invoke(category, types, PointToClient(box.PointToScreen(e.Location)));
                        else
                            BackgroundClicked(e);
                    };
                    foreach (EventType type in types)
                    {
                        var label = new EventLabel(this, type);
                        box.Controls.Add(label);
                        labels.Add(label);
                    }
                    boxes.Add(box);
                    canvas.Controls.Add(box);
                }
            }
            finally
            {
                canvas.ResumeLayout(false);
            }
            LayoutBoxes();
        }

        private int BoxHeight(GroupBox box)
        {
            return TitleHeight + box.Controls.Count * (rowHeight + RowGap) - RowGap + BoxPad;
        }

        private int TitleHeight { get { return Font.Height + 6; } }

        /// <summary>Splits the boxes, in order, into four columns of balanced height.</summary>
        private void LayoutBoxes()
        {
            int width = ClientSize.Width - bar.Width;
            int columnWidth = Math.Max(80, (width - Gap * (Columns + 1)) / Columns);
            int[] heights = boxes.Select(b => BoxHeight(b) + Gap).ToArray();
            int[] starts = Partition(heights, Columns);

            canvas.SuspendLayout();
            int tallest = 0;
            for (int col = 0; col < Columns; col++)
            {
                int end = col + 1 < Columns ? starts[col + 1] : boxes.Count;
                int y = Gap;
                for (int i = starts[col]; i < end; i++)
                {
                    GroupBox box = boxes[i];
                    box.SetBounds(Gap + col * (columnWidth + Gap), y, columnWidth, BoxHeight(box));
                    int labelY = TitleHeight;
                    foreach (Control label in box.Controls)
                    {
                        label.SetBounds(BoxPad, labelY, columnWidth - BoxPad * 2, rowHeight);
                        labelY += rowHeight + RowGap;
                    }
                    y += heights[i];
                }
                tallest = Math.Max(tallest, y);
            }
            canvas.SetBounds(0, -bar.Value, width, Math.Max(tallest + Gap, ClientSize.Height));
            canvas.ResumeLayout(false);
            UpdateScrollBar(tallest + Gap);
        }

        // Start index of each part, minimising the tallest part.
        private static int[] Partition(int[] heights, int parts)
        {
            int n = heights.Length;
            var starts = new int[parts];
            if (n == 0)
                return starts;
            var prefix = new int[n + 1];
            for (int i = 0; i < n; i++)
                prefix[i + 1] = prefix[i] + heights[i];

            // best[k, i]: the smallest tallest part splitting the first i boxes into k parts.
            var best = new int[parts + 1, n + 1];
            var split = new int[parts + 1, n + 1];
            for (int i = 0; i <= n; i++)
                best[1, i] = prefix[i];
            for (int k = 2; k <= parts; k++)
            {
                for (int i = 0; i <= n; i++)
                {
                    best[k, i] = int.MaxValue;
                    for (int j = 0; j <= i; j++)
                    {
                        int cost = Math.Max(best[k - 1, j], prefix[i] - prefix[j]);
                        if (cost < best[k, i])
                        {
                            best[k, i] = cost;
                            split[k, i] = j;
                        }
                    }
                }
            }
            int at = n;
            for (int k = parts; k >= 2; k--)
            {
                at = split[k, at];
                starts[k - 1] = at;
            }
            return starts;
        }

        private void UpdateScrollBar(int contentHeight)
        {
            int page = Math.Max(1, ClientSize.Height);
            int hidden = Math.Max(0, contentHeight - page);
            bar.Minimum = 0;
            bar.LargeChange = page;
            bar.SmallChange = rowHeight + RowGap;
            bar.Maximum = hidden + page - 1;
            bar.Enabled = hidden > 0;
            if (bar.Value > hidden)
                bar.Value = hidden;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutBoxes();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            Build();
        }

        // Pass theme colours on to the boxes.
        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            canvas.BackColor = BackColor;
            foreach (GroupBox box in boxes)
                box.BackColor = BackColor;
        }

        protected override void OnForeColorChanged(EventArgs e)
        {
            base.OnForeColorChanged(e);
            foreach (GroupBox box in boxes)
                box.ForeColor = ForeColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
        }

        #endregion

        #region Selection

        /// <summary>The selected event types, in the order shown.</summary>
        internal List<EventType> SelectedTypes
        {
            get { return labels.Where(l => selected.Contains(l.Type.Id)).Select(l => l.Type).ToList(); }
        }

        internal bool IsSelected(EventType type)
        {
            return selected.Contains(type.Id);
        }

        /// <summary>Replaces the selection with these event types (or adds them).</summary>
        internal void Select(IEnumerable<EventType> types, bool add)
        {
            if (!add)
                selected.Clear();
            foreach (EventType type in types)
                selected.Add(type.Id);
            SelectionDone();
        }

        internal void ClearSelection()
        {
            if (selected.Count == 0)
                return;
            selected.Clear();
            anchorId = -1;
            SelectionDone();
        }

        private void SelectionDone()
        {
            RefreshColours();
            SelectionChanged?.Invoke();
        }

        private void ClickSelect(EventType type, bool ctrl, bool shift)
        {
            int from = labels.FindIndex(l => l.Type.Id == anchorId);
            if (shift && from >= 0)
            {
                int to = labels.FindIndex(l => l.Type.Id == type.Id);
                if (!ctrl)
                    selected.Clear();
                for (int i = Math.Min(from, to); i <= Math.Max(from, to); i++)
                    selected.Add(labels[i].Type.Id);
            }
            else if (ctrl)
            {
                if (!selected.Remove(type.Id))
                    selected.Add(type.Id);
                anchorId = type.Id;
            }
            else
            {
                selected.Clear();
                selected.Add(type.Id);
                anchorId = type.Id;
            }
            SelectionDone();
        }

        #endregion

        #region Mouse and keyboard

        private void LabelClicked(EventLabel label, MouseEventArgs e)
        {
            Focus();
            bool ctrl = (ModifierKeys & Keys.Control) != 0, shift = (ModifierKeys & Keys.Shift) != 0;
            if (e.Button == MouseButtons.Left)
                ClickSelect(label.Type, ctrl, shift);
            else if (e.Button == MouseButtons.Right)
            {
                if (!selected.Contains(label.Type.Id))
                    ClickSelect(label.Type, false, false);
                CellMenu?.Invoke(label.Type, PointToClient(label.PointToScreen(e.Location)));
            }
        }

        // Only one label is outlined at a time.
        private void SetHot(EventLabel label)
        {
            if (label == hotLabel)
                return;
            EventLabel old = hotLabel;
            hotLabel = label;
            if (old != null && !old.IsDisposed)
                old.Invalidate();
            if (label != null)
                label.Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            SetHot(null);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            SetHot(null);
        }

        private void BackgroundClicked(MouseEventArgs e)
        {
            Focus();
            if (e.Button == MouseButtons.Left && (ModifierKeys & (Keys.Control | Keys.Shift)) == 0)
                ClearSelection();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.A && e.Control)
            {
                Select(labels.Select(l => l.Type), false);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                ClearSelection();
                e.Handled = true;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollByWheel(e.Delta);
        }

        /// <summary>Scrolls as one turn of the mouse wheel does (positive delta scrolls up).</summary>
        internal void ScrollByWheel(int delta)
        {
            if (!bar.Enabled || delta == 0)
                return;
            int step = Math.Max(1, SystemInformation.MouseWheelScrollLines) * (rowHeight + RowGap);
            int max = Math.Max(0, bar.Maximum - bar.LargeChange + 1);
            bar.Value = Math.Max(0, Math.Min(max, bar.Value - Math.Sign(delta) * step));
        }

        /// <summary>Whether a point on the screen is over the grid's scroll bar.</summary>
        internal bool IsOverScrollBar(Point screen)
        {
            return bar.Visible && bar.RectangleToScreen(bar.ClientRectangle).Contains(screen);
        }

        #endregion

        /// <summary>Holds the boxes; moved by the scroll bar.</summary>
        private sealed class Canvas : Panel
        {
            public Canvas()
            {
                DoubleBuffered = true;
            }
        }

        /// <summary>One event type, drawn in its own colours.</summary>
        private sealed class EventLabel : Control
        {
            private readonly EventColourGrid grid;
            internal readonly EventType Type;

            public EventLabel(EventColourGrid grid, EventType type)
            {
                this.grid = grid;
                Type = type;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                SetStyle(ControlStyles.Selectable, false);
                Cursor = Cursors.Hand;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                EventColour colour = grid.colourOf(Type.Id);
                Color text = colour != null ? colour.Text : grid.ForeColor;
                Color back = colour != null ? colour.Back : grid.BackColor;
                g.Clear(back);
                TextRenderer.DrawText(g, Type.Name, grid.Font, new Rectangle(4, 0, Width - 6, Height), text, back,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                if (grid.selected.Contains(Type.Id))
                {
                    using (var pen = new Pen(grid.SelectionColor, 2))
                        g.DrawRectangle(pen, 1, 1, Width - 2, Height - 2);
                }
                else if (grid.hotLabel == this)
                {
                    using (var pen = new Pen(grid.HotColor))
                        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                grid.SetHot(this);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                grid.SetHot(this);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                if (grid.hotLabel == this)
                    grid.SetHot(null);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (ClientRectangle.Contains(e.Location))
                    grid.LabelClicked(this, e);
            }
        }
    }

    /// <summary>Lets the grid scroll in the Visual Studio designer (scroll bar and mouse wheel).</summary>
    public sealed class EventColourGridDesigner : System.Windows.Forms.Design.ControlDesigner
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        protected override bool GetHitTest(Point point)
        {
            var grid = Control as EventColourGrid;
            return grid != null && grid.IsOverScrollBar(point);
        }

        protected override void WndProc(ref Message m)
        {
            var grid = Control as EventColourGrid;
            if (m.Msg == WM_MOUSEWHEEL && grid != null)
            {
                int delta = (short)(((long)m.WParam >> 16) & 0xFFFF);
                grid.ScrollByWheel(delta);
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }
    }
}
