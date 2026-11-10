using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace AuroraThemeManager
{
    public sealed class IconGrid : Control
    {
        internal const int IconSize = 48;
        private const int Cell = 58;
        private const int MaxThumbs = 1500;

        private readonly VScrollBar bar = new VScrollBar { Dock = DockStyle.Right };
        private readonly ToolTip tip = new ToolTip();
        private readonly Dictionary<string, Bitmap> thumbs = new Dictionary<string, Bitmap>();
        private readonly HashSet<string> queued = new HashSet<string>();
        private readonly HashSet<string> misses = new HashSet<string>();
        private readonly Stack<GameIcon> pending = new Stack<GameIcon>();
        private readonly object gate = new object();

        private List<GameIcon> items = new List<GameIcon>();
        private Thread[] workers;
        private volatile bool stopping;
        private GameIcon selected;
        private int hot = -1;

        internal Color GridBack = Color.FromArgb(24, 24, 24);
        internal Color SelectedBack = Color.FromArgb(60, 70, 90);
        internal Color HotBack = Color.FromArgb(40, 44, 52);

        internal event Action<GameIcon> IconChosen;

        public IconGrid()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            bar.ValueChanged += (s, e) => OnScrolled();
            Controls.Add(bar);
            BackColor = GridBack;
        }

        private int Columns { get { return Math.Max(1, (ClientSize.Width - bar.Width) / Cell); } }
        private int VisibleRows { get { return Math.Max(1, ClientSize.Height / Cell); } }
        private int TotalRows { get { return (items.Count + Columns - 1) / Columns; } }

        private int CellWidth { get { return Math.Max(Cell, (ClientSize.Width - bar.Width) / Columns); } }
        private int CellHeight { get { return Math.Max(Cell, ClientSize.Height / VisibleRows); } }

        private int TopRow { get { return bar.Value; } }

        internal int Count { get { return items.Count; } }

        internal void SetItems(List<GameIcon> list)
        {
            items = list ?? new List<GameIcon>();
            hot = -1;
            ClearPending();
            UpdateScrollBar();
            bar.Value = 0;
            Invalidate();
        }

        internal void RetryMissing()
        {
            lock (gate)
            {
                foreach (string key in misses)
                    queued.Remove(key);
                misses.Clear();
            }
            Invalidate();
        }

        internal Bitmap TryGetThumb(GameIcon icon)
        {
            Bitmap bmp;
            return icon != null && thumbs.TryGetValue(icon.Key, out bmp) ? bmp : null;
        }

        private int MaxTopRow { get { return Math.Max(0, TotalRows - VisibleRows); } }

        private void UpdateScrollBar()
        {
            int rows = TotalRows;
            bar.Minimum = 0;
            bar.SmallChange = 1;
            bar.LargeChange = VisibleRows;
            bar.Maximum = Math.Max(0, rows - 1);
            bar.Enabled = rows > VisibleRows;
            if (bar.Value > MaxTopRow)
                bar.Value = MaxTopRow;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollBar();
        }

        private void OnScrolled()
        {
            ClearPending();
            Invalidate();
        }

        private void ClearPending()
        {
            lock (gate)
            {
                foreach (GameIcon icon in pending)
                    queued.Remove(icon.Key);
                pending.Clear();
            }
        }

        private int IndexAt(Point p)
        {
            if (p.X < 0 || p.Y < 0 || p.X >= Columns * CellWidth || p.Y >= VisibleRows * CellHeight)
                return -1;
            int index = (TopRow + p.Y / CellHeight) * Columns + p.X / CellWidth;
            return index >= 0 && index < items.Count ? index : -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(GridBack);

            int cols = Columns;
            int cellWidth = CellWidth, cellHeight = CellHeight;
            int firstRow = TopRow;
            int lastRow = TopRow + VisibleRows - 1;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            for (int row = firstRow; row <= lastRow; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int index = row * cols + col;
                    if (index >= items.Count)
                        return;

                    GameIcon icon = items[index];
                    var cell = new Rectangle(col * cellWidth, (row - firstRow) * cellHeight, cellWidth, cellHeight);
                    if (selected != null && selected.Key == icon.Key)
                        using (var brush = new SolidBrush(SelectedBack))
                            g.FillRectangle(brush, cell);
                    else if (index == hot)
                        using (var brush = new SolidBrush(HotBack))
                            g.FillRectangle(brush, cell);

                    Bitmap bmp;
                    if (thumbs.TryGetValue(icon.Key, out bmp))
                        g.DrawImage(bmp, cell.X + (cellWidth - IconSize) / 2, cell.Y + (cellHeight - IconSize) / 2, IconSize, IconSize);
                    else
                        Request(icon);
                }
            }
        }

        private void Request(GameIcon icon)
        {
            lock (gate)
            {
                if (!queued.Add(icon.Key))
                    return;
                pending.Push(icon);
                EnsureWorkers();
                Monitor.Pulse(gate);
            }
        }

        private void EnsureWorkers()
        {
            if (workers != null)
                return;
            workers = new Thread[2];
            for (int i = 0; i < workers.Length; i++)
            {
                workers[i] = new Thread(Work) { IsBackground = true, Name = "Icon thumbnails" };
                workers[i].Start();
            }
        }

        private void Work()
        {
            while (true)
            {
                GameIcon icon;
                lock (gate)
                {
                    while (pending.Count == 0 && !stopping)
                        Monitor.Wait(gate);
                    if (stopping)
                        return;
                    icon = pending.Pop();
                }

                Bitmap bmp = null;
                try { bmp = IconCatalog.GetThumb(icon, IconSize); } catch { }

                if (bmp == null)
                {
                    lock (gate) { misses.Add(icon.Key); }
                    continue;
                }

                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        if (IsDisposed || stopping)
                        {
                            bmp.Dispose();
                            return;
                        }
                        if (thumbs.Count >= MaxThumbs)
                        {
                            foreach (Bitmap old in thumbs.Values)
                                old.Dispose();
                            thumbs.Clear();
                        }
                        Bitmap previous;
                        if (thumbs.TryGetValue(icon.Key, out previous))
                            previous.Dispose();
                        thumbs[icon.Key] = bmp;
                        lock (gate) { queued.Remove(icon.Key); }
                        Invalidate();
                    }));
                }
                catch
                {
                    bmp.Dispose();
                    return;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int index = IndexAt(e.Location);
            if (index == hot)
                return;
            hot = index;
            tip.SetToolTip(this, index < 0 ? "" : items[index].Title + " by " + items[index].Author);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot != -1)
            {
                hot = -1;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int index = IndexAt(e.Location);
            if (index < 0)
                return;
            selected = items[index];
            Invalidate();
            Action<GameIcon> handler = IconChosen;
            if (handler != null)
                handler(selected);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int value = bar.Value - Math.Sign(e.Delta) * 2;
            bar.Value = Math.Max(0, Math.Min(MaxTopRow, value));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (gate)
                {
                    stopping = true;
                    Monitor.PulseAll(gate);
                }
                foreach (Bitmap bmp in thumbs.Values)
                    bmp.Dispose();
                thumbs.Clear();
                tip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
