using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AuroraThemeManager
{
    /// <summary>
    /// Scrolling grid of same-size, caption-less thumbnails spread evenly across the width.
    /// Click, Ctrl+click and Shift+click select.
    /// </summary>
    public sealed class ThumbGrid : Control
    {
        /// <summary>Minimum gap between cells; leftover width is shared out evenly. Outlines are drawn in it.</summary>
        internal const int Gap = 3;

        // How far outlines reach outside a cell.
        private const int OutlineWidth = 2;

        private readonly VScrollBar bar = new VScrollBar { Dock = DockStyle.Right };
        private readonly ToolTip tip = new ToolTip();

        private List<string> files = new List<string>();
        private Bitmap[] thumbs = new Bitmap[0];
        private readonly HashSet<int> selected = new HashSet<int>();
        private int anchor = -1;

        // Keyboard position: where arrows move from and the far end of a Shift range.
        private int current = -1;
        private int hot = -1;
        private Size cell = new Size(96, 96);

        internal Color SelectedColor = Color.FromArgb(90, 150, 230);
        internal Color HotColor = Color.FromArgb(90, 96, 110);
        internal Color MarkedColor = Color.FromArgb(230, 180, 40);

        /// <summary>Pictures with their own outline (the player default).</summary>
        internal Func<string, bool> IsMarked = f => false;

        internal Func<string, string> TipFor = f => System.IO.Path.GetFileName(f);

        internal event Action SelectionChanged;

        public ThumbGrid()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            bar.ValueChanged += (s, e) => Invalidate();
            Controls.Add(bar);
        }

        #region Contents

        /// <summary>Thumbnail size, excluding the gap.</summary>
        internal Size CellSize
        {
            get { return cell; }
            set
            {
                cell = new Size(Math.Max(16, value.Width), Math.Max(16, value.Height));
                UpdateScrollBar();
                Invalidate();
            }
        }

        internal IReadOnlyList<string> Files { get { return files; } }

        internal void SetFiles(List<string> list)
        {
            foreach (Bitmap old in thumbs)
                old?.Dispose();
            files = list ?? new List<string>();
            thumbs = new Bitmap[files.Count];
            selected.Clear();
            anchor = hot = current = -1;
            bar.Value = 0;
            UpdateScrollBar();
            Invalidate();
            SelectionChanged?.Invoke();
        }

        /// <summary>The grid takes ownership of the bitmap.</summary>
        internal void SetThumb(int index, Bitmap thumb)
        {
            if (index < 0 || index >= thumbs.Length)
            {
                thumb?.Dispose();
                return;
            }
            thumbs[index]?.Dispose();
            thumbs[index] = thumb;
            if (IsVisible(index))
                Invalidate(CellBounds(index));
        }

        internal List<string> SelectedFiles
        {
            get { return selected.OrderBy(i => i).Select(i => files[i]).ToList(); }
        }

        /// <summary>Selects by file name and scrolls the last into view.</summary>
        internal void Select(IEnumerable<string> names)
        {
            var wanted = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            selected.Clear();
            for (int i = 0; i < files.Count; i++)
                if (wanted.Contains(System.IO.Path.GetFileName(files[i])))
                    selected.Add(i);
            if (selected.Count > 0)
            {
                anchor = current = selected.Max();
                EnsureVisible(anchor);
            }
            Invalidate();
            SelectionChanged?.Invoke();
        }

        #endregion

        #region Layout

        private int Usable { get { return Math.Max(0, ClientSize.Width - bar.Width); } }

        /// <summary>Widest cell that still fits this many columns at the minimum gap.</summary>
        internal int MaxCellWidthFor(int columns)
        {
            int usable = Usable > 0 ? Usable : ClientSize.Width - SystemInformation.VerticalScrollBarWidth;
            return Math.Max(16, (usable - (columns + 1) * Gap) / Math.Max(1, columns));
        }
        private int Columns { get { return Math.Max(1, (Usable - Gap) / (cell.Width + Gap)); } }

        /// <summary>Gap between columns and at the sides.</summary>
        private float Spacing
        {
            get
            {
                int columns = Columns;
                return Math.Max(Gap, (Usable - columns * cell.Width) / (float)(columns + 1));
            }
        }

        private float Pitch { get { return cell.Width + Spacing; } }
        private float RowPitch { get { return cell.Height + Spacing; } }
        private int Rows { get { return (files.Count + Columns - 1) / Columns; } }
        private int VisibleRows { get { return Math.Max(1, (int)((ClientSize.Height - Spacing) / RowPitch)); } }

        private Rectangle CellBounds(int index)
        {
            int row = index / Columns - bar.Value;
            int column = index % Columns;
            float spacing = Spacing;
            return new Rectangle((int)Math.Round(spacing + column * Pitch), (int)Math.Round(spacing + row * RowPitch), cell.Width, cell.Height);
        }

        private bool IsVisible(int index)
        {
            int row = index / Columns;
            return row >= bar.Value && row <= bar.Value + VisibleRows;
        }

        private int IndexAt(Point point)
        {
            if (point.X >= Usable)
                return -1;
            float spacing = Spacing;
            int column = (int)Math.Floor((point.X - spacing) / Pitch);
            int row = (int)Math.Floor((point.Y - spacing) / RowPitch);
            if (column < 0 || column >= Columns || row < 0)
                return -1;
            int index = (row + bar.Value) * Columns + column;
            return index < files.Count && CellBounds(index).Contains(point) ? index : -1;
        }

        private void UpdateScrollBar()
        {
            int hidden = Math.Max(0, Rows - VisibleRows);
            bar.Minimum = 0;
            bar.LargeChange = VisibleRows;
            bar.SmallChange = 1;
            bar.Maximum = hidden + VisibleRows - 1;
            bar.Enabled = hidden > 0;
            if (bar.Value > hidden)
                bar.Value = hidden;
        }

        private void EnsureVisible(int index)
        {
            int row = index / Columns;
            int top = bar.Value;
            if (row < top)
                top = row;
            else if (row >= top + VisibleRows)
                top = row - VisibleRows + 1;
            bar.Value = Math.Max(0, Math.Min(top, Math.Max(0, Rows - VisibleRows)));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollBar();
        }

        #endregion

        #region Drawing

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (files.Count == 0)
                return;

            int first = bar.Value * Columns;
            int last = Math.Min(files.Count - 1, (bar.Value + VisibleRows + 1) * Columns - 1);
            for (int i = first; i <= last; i++)
            {
                Rectangle r = CellBounds(i);
                // Outlines sit outside the cell, so test the inflated rect or a gap-only repaint wipes them.
                Rectangle withOutline = r;
                withOutline.Inflate(OutlineWidth, OutlineWidth);
                if (!withOutline.IntersectsWith(e.ClipRectangle))
                    continue;

                Bitmap thumb = thumbs[i];
                if (thumb != null)
                    e.Graphics.DrawImage(thumb, r.X + (r.Width - thumb.Width) / 2, r.Y + (r.Height - thumb.Height) / 2, thumb.Width, thumb.Height);

                Color? outline = selected.Contains(i) ? SelectedColor
                    : IsMarked(files[i]) ? MarkedColor
                    : i == hot ? HotColor
                    : (Color?)null;
                if (outline != null)
                {
                    // In the gap, so the picture isn't covered.
                    using (var pen = new Pen(outline.Value, 2f))
                        e.Graphics.DrawRectangle(pen, r.X - 1, r.Y - 1, r.Width + 1, r.Height + 1);
                }
                if (IsMarked(files[i]) && selected.Contains(i))
                {
                    using (var pen = new Pen(MarkedColor, 1f))
                        e.Graphics.DrawRectangle(pen, r.X + 1, r.Y + 1, r.Width - 3, r.Height - 3);
                }
            }
        }

        #endregion

        #region Mouse

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int index = IndexAt(e.Location);
            if (index == hot)
                return;
            int old = hot;
            hot = index;
            if (old >= 0 && old < files.Count) Invalidate(Inflate(CellBounds(old)));
            if (hot >= 0) Invalidate(Inflate(CellBounds(hot)));
            tip.SetToolTip(this, hot >= 0 ? TipFor(files[hot]) : null);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot >= 0 && hot < files.Count)
                Invalidate(Inflate(CellBounds(hot)));
            hot = -1;
            tip.SetToolTip(this, null);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button != MouseButtons.Left)
                return;

            int index = IndexAt(e.Location);
            Keys modifiers = ModifierKeys;
            if (index < 0)
            {
                if ((modifiers & (Keys.Control | Keys.Shift)) == 0)
                    selected.Clear();
            }
            else if ((modifiers & Keys.Shift) != 0 && anchor >= 0)
            {
                if ((modifiers & Keys.Control) == 0)
                    selected.Clear();
                for (int i = Math.Min(anchor, index); i <= Math.Max(anchor, index); i++)
                    selected.Add(i);
                current = index;
            }
            else if ((modifiers & Keys.Control) != 0)
            {
                if (!selected.Remove(index))
                    selected.Add(index);
                anchor = current = index;
            }
            else
            {
                selected.Clear();
                selected.Add(index);
                anchor = current = index;
            }
            Invalidate();
            SelectionChanged?.Invoke();
        }

        #endregion

        #region Keyboard

        // Keep navigation keys in the grid instead of moving focus.
        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.Home:
                case Keys.End:
                case Keys.PageUp:
                case Keys.PageDown:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        /// <summary>Ctrl+A, arrows, Page Up/Down, Home/End; Shift extends from the anchor.</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || files.Count == 0)
                return;

            if (e.Control && e.KeyCode == Keys.A)
            {
                selected.Clear();
                for (int i = 0; i < files.Count; i++)
                    selected.Add(i);
                if (anchor < 0) anchor = 0;
                if (current < 0) current = files.Count - 1;
                e.Handled = true;
                e.SuppressKeyPress = true;
                Invalidate();
                SelectionChanged?.Invoke();
                return;
            }

            int from = current >= 0 ? current : (anchor >= 0 ? anchor : -1);
            int columns = Columns, page = Math.Max(1, VisibleRows) * columns;
            int to;
            switch (e.KeyCode)
            {
                case Keys.Left: to = from < 0 ? 0 : from - 1; break;
                case Keys.Right: to = from < 0 ? 0 : from + 1; break;
                case Keys.Up: to = from < 0 ? 0 : from - columns; break;
                case Keys.Down: to = from < 0 ? 0 : from + columns; break;
                case Keys.PageUp: to = from < 0 ? 0 : from - page; break;
                case Keys.PageDown: to = from < 0 ? 0 : from + page; break;
                case Keys.Home: to = 0; break;
                case Keys.End: to = files.Count - 1; break;
                default: return;
            }
            to = Math.Max(0, Math.Min(files.Count - 1, to));
            e.Handled = true;

            if (e.Shift && anchor >= 0)
            {
                selected.Clear();
                for (int i = Math.Min(anchor, to); i <= Math.Max(anchor, to); i++)
                    selected.Add(i);
            }
            else
            {
                selected.Clear();
                selected.Add(to);
                anchor = to;
            }
            current = to;
            EnsureVisible(to);
            Invalidate();
            SelectionChanged?.Invoke();
        }

        #endregion

        #region Mouse wheel

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int rows = Math.Max(1, SystemInformation.MouseWheelScrollLines / 2);
            int target = bar.Value - Math.Sign(e.Delta) * rows;
            bar.Value = Math.Max(0, Math.Min(target, Math.Max(0, Rows - VisibleRows)));
        }

        private Rectangle Inflate(Rectangle r)
        {
            int grow = (int)Math.Ceiling(Spacing);
            r.Inflate(grow, grow);
            return r;
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Bitmap thumb in thumbs)
                    thumb?.Dispose();
                tip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
