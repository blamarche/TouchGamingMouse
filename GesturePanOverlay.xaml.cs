using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TouchGamingMouse
{
    /// <summary>
    /// Interaction logic for GesturePanOverlay.xaml
    /// </summary>
    public partial class GesturePanOverlay : Window
    {
        //TODO: make a base class for this logic
        public GesturePanOverlay()
        {
            InitializeComponent();
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new WindowInteropHelper(this);
            uint origStyle = WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW; //no taskbar icon or anything

            SetWindowLong(helper.Handle, GWL_EXSTYLE, origStyle);

        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    }

   
}
