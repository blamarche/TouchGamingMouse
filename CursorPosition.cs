using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TouchGamingMouse
{
    static class CursorPosition
    {
        private static PointInter lastNonZeroPos;

        [StructLayout(LayoutKind.Sequential)]
        public struct PointInter
        {
            public int X;
            public int Y;
            public static explicit operator Point(PointInter point) => new Point(point.X, point.Y);
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out PointInter lpPoint);

        // For your convenience
        public static PointInter Pos()
        {
            PointInter lpPoint;
            GetCursorPos(out lpPoint);
            return lpPoint;
        }

        private static System.Windows.Threading.DispatcherTimer posTimer = new System.Windows.Threading.DispatcherTimer();
        
        private static void posTimer_Tick(object sender, EventArgs e)
        {
            var p = Pos();
            if (p.X == 0 && p.Y == 0)
                return;
            
            if (Mouse.DirectlyOver != null) //HACK: Trying to work around windows randomly ignoring WS_EX_NOACTIVATE
            {
                return;
            }
            
            lastNonZeroPos.X = p.X;
            lastNonZeroPos.Y = p.Y;
        }

        public static void StartPosWatcher(int delayMs)
        {
            posTimer.Tick += posTimer_Tick;
            posTimer.Interval = new TimeSpan(0, 0, 0, 0, delayMs);
            posTimer.Start();
            posTimer_Tick(null,null);
        }

        public static void StopPosWatcher()
        {
            posTimer.Tick -= posTimer_Tick;
            posTimer.Stop();
        }

        public static PointInter GetLastPos()
        {
            return lastNonZeroPos;
        }

        public static void MoveCursorToLastGood()
        {
            MoveCursorTo(lastNonZeroPos.X, lastNonZeroPos.Y);            
        }

        public static void MoveCursorTo(double unscaled_x, double unscaled_y)
        {
            //NOTE: we must have 'false' set in dpi-aware app.manifest for this calculation to work. there has got to be a better way
            lastNonZeroPos.X = (int)unscaled_x;
            lastNonZeroPos.Y = (int)unscaled_y;
            var x = 65535.0f * unscaled_x / System.Windows.SystemParameters.PrimaryScreenWidth;
            var y = 65535.0f * unscaled_y / System.Windows.SystemParameters.PrimaryScreenHeight;
            InputHelper.SendMouse((uint)(InputHelper.MouseEventF.MOUSEEVENTF_ABSOLUTE | InputHelper.MouseEventF.MOUSEEVENTF_MOVE), 0, (int)x, (int)y);
        }
    }
}
