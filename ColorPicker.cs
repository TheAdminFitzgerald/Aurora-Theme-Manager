using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace AuroraThemeManager
{
    internal sealed class ColorPicker : ColorDialog
    {
        private const int WM_INITDIALOG = 0x0110;
        private const int CDM_SETDEFAULTFOCUS = 0x0400 + 0x51;
        private const int EM_SETSEL = 0x00B1;
        private const int RedEditId = 706;

        public string Title { get; set; }

        protected override IntPtr HookProc(IntPtr hWnd, int msg, IntPtr wparam, IntPtr lparam)
        {
            if (msg == CDM_SETDEFAULTFOCUS)
            {
                FocusRed(hWnd);
                return IntPtr.Zero;
            }

            IntPtr result = base.HookProc(hWnd, msg, wparam, lparam);
            if (msg == WM_INITDIALOG)
            {
                if (!string.IsNullOrEmpty(Title))
                    SetWindowText(hWnd, Title);
                SetWindowPos(hWnd, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                CentreSampleCaption(hWnd);
                FocusRed(hWnd);
            }
            return result;
        }

        private const int SampleId = 709;
        private const int ColorCaptionId = 730;
        private const int SolidCaptionId = 731;
        private const int DefineCustomId = 719;
        private const int HueCaptionId = 723;
        private const int LumCaptionId = 725;

        /// <summary>
        /// The dialog labels the colour sample with two narrow captions, "Color" right-aligned and
        /// "|Solid" beside it, which run off the left of the sample and squeeze together.  They are
        /// joined into one caption centred under the sample, as wide as the space between the Define
        /// Custom Colors button and the Hue and Lum labels allows.
        /// </summary>
        private static void CentreSampleCaption(IntPtr dialog)
        {
            IntPtr sample = GetDlgItem(dialog, SampleId);
            IntPtr color = GetDlgItem(dialog, ColorCaptionId);
            IntPtr solid = GetDlgItem(dialog, SolidCaptionId);
            if (sample == IntPtr.Zero || color == IntPtr.Zero || solid == IntPtr.Zero)
                return;

            RECT sampleRect = ChildRect(dialog, sample);
            RECT captionRect = ChildRect(dialog, color);
            int centre = (sampleRect.Left + sampleRect.Right) / 2;
            int left = sampleRect.Left - 6;
            int right = sampleRect.Right + 6;
            IntPtr define = GetDlgItem(dialog, DefineCustomId);
            if (define != IntPtr.Zero)
                left = Math.Max(left, ChildRect(dialog, define).Right + 2);
            foreach (int id in new[] { HueCaptionId, LumCaptionId })
            {
                IntPtr label = GetDlgItem(dialog, id);
                if (label != IntPtr.Zero)
                    right = Math.Min(right, ChildRect(dialog, label).Left - 2);
            }
            int half = Math.Min(centre - left, right - centre);
            if (half <= 0)
                return;

            string first = WindowText(color).Trim();
            string second = WindowText(solid).Trim().TrimStart('|').Trim();
            SetWindowText(color, first + " | " + second);
            int style = GetWindowLong(color, GWL_STYLE);
            SetWindowLong(color, GWL_STYLE, (style & ~SS_TYPEMASK) | SS_CENTER);
            SetWindowPos(color, IntPtr.Zero, centre - half, captionRect.Top, half * 2, captionRect.Bottom - captionRect.Top, SWP_NOZORDER);
            ShowWindow(solid, SW_HIDE);
            InvalidateRect(color, IntPtr.Zero, true);
        }

        private static RECT ChildRect(IntPtr dialog, IntPtr child)
        {
            RECT rect;
            GetWindowRect(child, out rect);
            MapWindowPoints(IntPtr.Zero, dialog, ref rect, 2);
            return rect;
        }

        private static string WindowText(IntPtr window)
        {
            var text = new StringBuilder(256);
            GetWindowText(window, text, text.Capacity);
            return text.ToString();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        private const int GWL_STYLE = -16;
        private const int SS_TYPEMASK = 0x1F;
        private const int SS_CENTER = 0x1;
        private const uint SWP_NOZORDER = 0x0004;
        private const int SW_HIDE = 0;

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern int MapWindowPoints(IntPtr from, IntPtr to, ref RECT rect, int points);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int index, int value);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int command);

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr rect, bool erase);

        private static void FocusRed(IntPtr dialog)
        {
            IntPtr red = GetDlgItem(dialog, RedEditId);
            if (red == IntPtr.Zero)
                return;
            SetFocus(red);
            SendMessage(red, EM_SETSEL, IntPtr.Zero, new IntPtr(-1));
            PostMessage(red, EM_SETSEL, IntPtr.Zero, new IntPtr(-1));
        }

        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDlgItem(IntPtr hDlg, int nIDDlgItem);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
