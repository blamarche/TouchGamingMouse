using System;

namespace TouchGamingMouse
{
    class MouseDisabler
    {
        private int delay;
        private static System.Windows.Threading.DispatcherTimer delayTimer;
        private DisableTouchConversionToMouse mouseDisabler = null;

        public MouseDisabler(int delayMs)
        {
            delay = delayMs;
            delayTimer = new System.Windows.Threading.DispatcherTimer();
            delayTimer.Interval = new TimeSpan(0, 0, 0, 0, delay);
            delayTimer.Tick += timer_Tick;
        }
        
        public void EnableMouse(bool delayed=true)
        {
            if (delayTimer.IsEnabled)
            {
                delayTimer.Stop();
            }

            CursorPosition.MoveCursorToLastGood();

            if (delayed && delay!=0)
            {
                delayTimer.Start();
            }
            else
            {
                _enableMouse();
            }
        }
        
        public void DisableMouse()
        {
            CursorPosition.MoveCursorToLastGood();
            if (delayTimer.IsEnabled)
                delayTimer.Stop();
            if (mouseDisabler == null)
                mouseDisabler = new DisableTouchConversionToMouse();
            
        }

        private void timer_Tick(object sender, EventArgs e)
        {            
            _enableMouse();
            delayTimer.Stop();
        }

        private void _enableMouse()
        {
            if (mouseDisabler != null)
            {                
                mouseDisabler.Dispose();
                mouseDisabler = null;
            }
        }
    }
}
