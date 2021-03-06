﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace TouchGamingMouse
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const int CURSOR_WATCHER_FRAMEDELAY = 8; //8 = 125hz, 4= 250hz etc
        const int MOUSE_DISABLER_DELAY = 10;

        const int GESTURE_TIMER_DELAY = 4;
        const double GESTURE_AREA_SCALE = 0.15;
        const double GESTURE_AREA_END_SCALE = 0.17;
        const int GESTURE_MAX_HISTORY = 10;
        const double GESTURE_DIVERT_AREA_SCALE = 0.1;
        const double GESTURE_ZIG_CARDINAL_AREA_SCALE = 0.075;
        const int GESTURE_TIMEOUT_MS = 1500;
        const int GESTURE_CIRCLE_MARGIN = 3; //min distance from center to count as being in a new quadrant

        private WindowInteropHelper helper;
        private int origStyle;
        private MouseDisabler mouseDisabler;
        private GestureRuntimeProps gestureProps;

        private Brush gridColorOpaque = new SolidColorBrush(Color.FromArgb(0x01, 0, 0, 0));
        private Brush gridColorTrans = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

        private Brush buttonColor = new SolidColorBrush(Color.FromRgb(31, 31, 31));
        private Brush buttonColorOn = new SolidColorBrush(Color.FromRgb(0, 90, 0));
        private Brush buttonForeground = new SolidColorBrush(Color.FromRgb(185, 185, 185));

        private Dictionary<string, Button> toggledKeys = new Dictionary<string, Button>();

        private GridConfig Config;
        private LaunchOptions Options = new LaunchOptions { ConfigFile = "", ForceNoAhk = false };
        private System.Diagnostics.Process ahkProc;

        private List<Button> mouseButtons = new List<Button>();
        private Button interceptButton;
        private int interceptMode = 0; //0: Move only, 1: left click, 2: middle click, 3: right click 
        
        public struct LaunchOptions
        {
            public string ConfigFile { get; set; }
            public bool ForceNoAhk { get; set; }
            public bool WriteConfig { get; set; }
            public bool SkipAhkCheck { get; set; }
        }
        
        public struct GridConfig
        {
            public string Name { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public double FontSize { get; set; }
            public float Opacity { get; set; }
            public bool UseAutohotkey { get; set; }
            public bool UseGestures { get; set; }
            public bool SendGestureMouseUp { get; set; }
            public bool ShowGestureOverlay { get; set; }

            public double GestureOverlayOpacity { get; set; }
            public double GestureOverlayPanOpacity { get; set; }
            public bool MouseInterceptMode { get; set; }
            public string AutohotkeyFile { get; set; }
            public Dictionary<string, ButtonConfig> Buttons { get; set; }
            public Dictionary<string, GestureConfig> Gestures { get; set; } //keys allowed: ZigUp, ZigDown, ZigLeft, ZigRight
        }

        public struct ButtonConfig
        {
            public string Content { get; set; }
            public int Column { get; set; }
            public int Row { get; set; }
            public int ColSpan { get; set; }
            public int RowSpan { get; set; }
            public string Type { get; set; } //KeyPress, KeyPress2, KeyToggle, HideShow, LMouse, RMouse, MMouse, HMouse, ScrollUp, ScrollDown
            public string TypeParam { get; set; } //DIK_SPACE, LEFTSHIFT etc
            public float TypeFlag { get; set; } //for KeyPress types: 0 = no mod, 1 = ctrl, 2 = alt, 3 = shift
            public int RepeatDelay { get; set; } //if > 0, tells scrollup/down to repeat when held down
        }

        public struct GestureConfig
        {
            public string TypeParam { get; set; } // DIK_SPACE, DIK_W
        }

        //constructor and form bindings
        public MainWindow()
        {
            InitializeComponent();
            ParseOptions();

            mouseDisabler = new MouseDisabler(MOUSE_DISABLER_DELAY);
            PreventTouchToMousePromotion.Register(this);

            Stylus.SetIsPressAndHoldEnabled(this, false);
            Stylus.SetIsTapFeedbackEnabled(this, false);
            Stylus.SetIsTouchFeedbackEnabled(this, false);
            Stylus.SetIsFlicksEnabled(this, false);

            CursorPosition.StartPosWatcher(CURSOR_WATCHER_FRAMEDELAY); 

            this.Topmost = true;
            this.WindowStyle = WindowStyle.None;
            this.WindowState = WindowState.Maximized;
            this.Focusable = false;

            InitializeGrid();

            //check if write config.json file and quit
            if (Options.WriteConfig)
            {
                var json = JsonSerializer.Serialize<GridConfig>(Config, new JsonSerializerOptions { WriteIndented=true });
                File.WriteAllText("config.json", json);
                Application.Current.Shutdown();
                return;
            }

            Application.Current.Exit += Application_Exit;

            //check for autohotkey
            if (!Options.SkipAhkCheck && Config.UseAutohotkey)
            {
                if (!Utils.IsAutohotkeyAssociated(Config.AutohotkeyFile))
                {
                    MessageBox.Show("TouchGamingMouse could not detect AutoHotkey on your system (.ahk files were not associated). Please install it for full functionality by visiting: https://www.autohotkey.com/. To prevent this message in the future, run TouchGamingMouse with the --skipahkcheck option.", "Warning");
                }
            }
            if (!Options.ForceNoAhk && Config.UseAutohotkey && File.Exists(Config.AutohotkeyFile))
            {
                ahkProc = System.Diagnostics.Process.Start(Config.AutohotkeyFile);
            }

            if (Config.Opacity>0)
            {
                this.Opacity = Config.Opacity;
            }

            if (Config.UseGestures)
            {
                gestureProps = new GestureRuntimeProps();
                gestureProps.CursorHistory = new List<CursorPosition.PointInter>();
                gestureProps.QuadrantHistory = new List<GestureQuadrant>();
                gestureProps.StartArea = CursorPosition.GetScreenCenter(GESTURE_AREA_SCALE, true);
                gestureProps.EndArea = CursorPosition.GetScreenCenter(GESTURE_AREA_END_SCALE, true);
                gestureProps.DivertTolerance = (int)((double)gestureProps.StartArea.Width * GESTURE_DIVERT_AREA_SCALE);
                gestureProps.ZigCardinalSize = (int)((double)gestureProps.StartArea.Width * GESTURE_ZIG_CARDINAL_AREA_SCALE);
                gestureProps.Timer = new DispatcherTimer();
                gestureProps.Timer.Interval = TimeSpan.FromMilliseconds(GESTURE_TIMER_DELAY);
                gestureProps.Timer.Tick += GestureTimer_Tick;
                gestureProps.Timer.Start();

                //pan zones
                int third = (int)((double)gestureProps.StartArea.Width * 0.333333334);
                var sx = gestureProps.StartArea.X;
                var sy = gestureProps.StartArea.Y;
                gestureProps.ZigZones = new System.Drawing.Rectangle[(int)GestureZigZone.ZIG_ZONE_MAX];
                gestureProps.ZigZones[(int)GestureZigZone.TopLeft] = new System.Drawing.Rectangle(sx, sy, third, third);
                gestureProps.ZigZones[(int)GestureZigZone.TopMiddle] = new System.Drawing.Rectangle(sx + third, sy, third, third);
                gestureProps.ZigZones[(int)GestureZigZone.TopRight] = new System.Drawing.Rectangle(sx + third + third, sy, third, third);
                gestureProps.ZigZones[(int)GestureZigZone.MiddleLeft] = new System.Drawing.Rectangle(sx, sy+third, third, third);
                gestureProps.ZigZones[(int)GestureZigZone.MiddleMiddle] = new System.Drawing.Rectangle(sx + third, sy+third, third, third);
                gestureProps.ZigZones[(int)GestureZigZone.MiddleRight] = new System.Drawing.Rectangle(sx + third + third, sy+third, third, third);
                gestureProps.ZigZones[(int)GestureZigZone.BottomLeft] = new System.Drawing.Rectangle(sx, sy+third+third, third, third);
                gestureProps.ZigZones[(int)GestureZigZone.BottomMiddle] = new System.Drawing.Rectangle(sx + third, sy + third + third, third, third);
                gestureProps.ZigZones[(int)GestureZigZone.BottomRight] = new System.Drawing.Rectangle(sx + third + third, sy + third + third, third, third);
                gestureProps.CurrentZigZone = -1;

                //init key states
                gestureProps.ZigKeyStates = new Dictionary<string, GestureKeyState>();
                gestureProps.ZigKeyStates.Add("ZigLeft", GestureKeyState.Up);
                gestureProps.ZigKeyStates.Add("ZigUp", GestureKeyState.Up);
                gestureProps.ZigKeyStates.Add("ZigRight", GestureKeyState.Up);
                gestureProps.ZigKeyStates.Add("ZigDown", GestureKeyState.Up);

                //init zone overlays
                gestureProps.GestureOverlay = new GestureOverlay(); //red center dot
                gestureProps.GestureOverlay.Hide();
                gestureProps.GestureOverlay.Top = (gestureProps.StartArea.Y + gestureProps.StartArea.Height / 2) - gestureProps.GestureOverlay.Height / 2;
                gestureProps.GestureOverlay.Left = (gestureProps.StartArea.X + gestureProps.StartArea.Width / 2) - gestureProps.GestureOverlay.Width / 2;
                gestureProps.GesturePanOverlay = new GesturePanOverlay(); //dpad
                gestureProps.GesturePanOverlay.Hide();
                gestureProps.GesturePanOverlay.Top = gestureProps.StartArea.Y;
                gestureProps.GesturePanOverlay.Left = gestureProps.StartArea.X;
                gestureProps.GesturePanOverlay.Width = gestureProps.StartArea.Width;
                gestureProps.GesturePanOverlay.Height = gestureProps.StartArea.Height;

                if (Config.GestureOverlayPanOpacity > 0.0)
                {
                    gestureProps.GesturePanOverlay.Opacity = Config.GestureOverlayPanOpacity;
                }

                if (Config.GestureOverlayOpacity > 0.0)
                {
                    gestureProps.GestureOverlay.Opacity = Config.GestureOverlayOpacity;
                }
            }
        }

        private void HandleError(string message, Exception error)
        {
            MessageBox.Show("TouchGamingMouse has encountered an error: \n"+message+"\n\n"+error.ToString(), "Error");
        }

        private void ParseOptions()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length>1)
            {
                for (var i=1; i<args.Length; i++)
                {
                    var token = args[i].Split('=');
                    switch (token[0])
                    {
                        case "--help":
                            MessageBox.Show(@"Command line options for TouchGamingMouse:
--config=<file.json> | Specify a specific config file to load so different games can have their own profile.
--writeconfig | Creates the file config.json with the built-in default config, or the contents of the file specified with --config, then exits.
---noahk | Forces the application to skip launching the autohotkey script, even if the config specifies otherwise.
--skipahkcheck | By default the application checks if Autohotkey is installed and warns the user if its not, this options suppresses that message.
");
                            Application.Current.Shutdown();
                            break;
                        case "--config":
                            Options.ConfigFile = token[1];
                            break;
                        case "--writeconfig":
                            Options.WriteConfig = true;
                            break;
                        case "--noahk":
                            Options.ForceNoAhk = true;
                            break;
                        case "--skipahkcheck":
                            Options.SkipAhkCheck = true;
                            break;
                    }
                }
            }
        }

        //reject focus
        protected override void OnActivated(EventArgs e)
        {

        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            helper = new WindowInteropHelper(this);
            //origStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_NOACTIVATE | WS_EX_LAYERED | WS_EX_TRANSPARENT;
            origStyle = WS_EX_NOACTIVATE| WS_EX_TOOLWINDOW; //no taskbar icon or anything

            //SetWindowLong(helper.Handle, GWL_STYLE, WS_CHILD);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, (uint)origStyle);

            //SetWindowLong(helper.Handle, GWL_STYLE, 0xD7FF0000); //values inspected from win10 on-screen touchpad window
            //SetWindowLong(helper.Handle, GWL_EXSTYLE, 0x0A7F77FD);

            var source = PresentationSource.FromVisual(this) as HwndSource;
            source.AddHook(WndProc);
        }
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        private const int GWL_EXSTYLE = -20;
        private const int GWL_STYLE = -16;

        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_CHILD = 0x40000000;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_MOUSEACTIVATE:
                    handled = true;
                    return new IntPtr(MA_NOACTIVATEANDEAT);
                case WM_POINTERACTIVATE:
                    handled = true;
                    return new IntPtr(PA_NOACTIVATE);
            }            
            return IntPtr.Zero;    
        }
        private const int WM_POINTERACTIVATE = 0x024B;
        private const int WM_POINTERDOWN                  = 0x0246;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 0x0003;
        private const int MA_NOACTIVATEANDEAT = 0x0004;
        private const int PA_NOACTIVATE = 3;

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);


        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (ahkProc != null)
            {
                ahkProc.Kill();
            }
        }

        private void Tray_RightClick( object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void InitializeGrid()
        {
            /* Example double-tap, 200ms delay:
                "XX": {
			        "Content":"XX",
			        "Row": 16,
			        "Column":10,
			        "ColSpan":2,
			        "RowSpan":2,
			        "Type":"KeyPress2",
			        "TypeParam":"DIK_X",
			        "TypeFlag":200
		        },
            */
            //read config, create grid and buttons
            var configtxt = ConfigDefault;
            if (Options.ConfigFile != "")
            {
                try
                {
                    configtxt = File.ReadAllText(Options.ConfigFile);
                }
                catch (Exception err)
                {
                    HandleError("Couldn't load specified config file.", err);
                }
            }

            GridConfig config;
            try
            {
                config = JsonSerializer.Deserialize<GridConfig>(configtxt);
            } catch (Exception err)
            {
                HandleError("The specified config file could not be parsed, check your JSON formatting.", err);
                Application.Current.Shutdown();
                return;
            }
                      
            mainGrid.Children.Clear();
            mainGrid.ColumnDefinitions.Clear();
            mainGrid.RowDefinitions.Clear();
            for (var i = 0; i < config.Width; i++)
            {
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }
            for (var i = 0; i < config.Height; i++)
            {
                mainGrid.RowDefinitions.Add(new RowDefinition());
            }

            Config = config;

            if (config.MouseInterceptMode)
            {
                interceptButton = CreateInterceptButton();
                mainGrid.Children.Add(interceptButton);
            }
            foreach (KeyValuePair<string, ButtonConfig> entry in config.Buttons)
            {
                var b = CreateButton(entry.Value, entry.Key);
                mainGrid.Children.Add(b);
            }
            if (config.AutohotkeyFile == null)
                config.AutohotkeyFile = "autohotkey.ahk";
        }

        private Button CreateInterceptButton()
        {
            Button b = new Button();
            b.Name = "";
            b.FontWeight = FontWeight.FromOpenTypeWeight(700);
            b.Style = this.FindResource("mainButtonStyle") as Style;
            b.Margin = new Thickness(0);
            b.Background = buttonColor;
            b.Foreground = buttonForeground;
            Grid.SetColumn(b, 0);
            Grid.SetRow(b, 0);
            Grid.SetRowSpan(b, 30);
            Grid.SetColumnSpan(b, 30);
            b.Content = "";
            
            b.PreviewTouchDown += Intercept_TouchDown;
            b.PreviewTouchUp += Intercept_TouchUp;
            b.StylusDown += Intercept_TouchDown;
            b.StylusUp += Intercept_TouchUp;

            b.Opacity = 0.005;
            b.Visibility = Visibility.Hidden;

            return b;
        }

        private Button CreateButton(ButtonConfig c, string name)
        {
            Button b = new Button();
            b.Name = name;
            b.FontWeight = FontWeight.FromOpenTypeWeight(700);
            b.Style = this.FindResource("mainButtonStyle") as Style;
            if (Config.FontSize>0)
                b.FontSize = Config.FontSize;
            b.Margin = new Thickness(0);
            b.Background = buttonColor;
            b.Foreground = buttonForeground;
            Grid.SetColumn(b, c.Column);
            Grid.SetRow(b, c.Row);
            if (c.RowSpan>1)
                Grid.SetRowSpan(b, c.RowSpan);
            if (c.ColSpan > 1)
                Grid.SetColumnSpan(b, c.ColSpan);

            //handle special case buttons
            b.Content = c.Content;
            switch (c.Type)
            {
                case "HideShow":
                    b.Content = "-";
                    b.PreviewTouchDown += BtnShowHide_TouchDown;
                    b.PreviewTouchUp += BtnShowHide_TouchUp;
                    b.StylusDown += BtnShowHide_TouchDown;
                    b.StylusUp += BtnShowHide_TouchUp;
                    break;

                case "ScrollUp":
                    b.PreviewTouchDown += BtnScUp_TouchDown;
                    b.StylusDown += BtnScUp_TouchDown;
                    b.PreviewTouchUp += BtnScUp_TouchUp;
                    b.StylusUp += BtnScUp_TouchUp;
                    break;

                case "ScrollDown":
                    b.PreviewTouchDown += BtnScDn_TouchDown;
                    b.StylusDown += BtnScDn_TouchDown;
                    b.PreviewTouchUp += BtnScDn_TouchUp;
                    b.StylusUp += BtnScDn_TouchUp;
                    break;

                case "LMouse":
                    b.PreviewTouchDown += BtnLMouse_TouchDown;
                    b.StylusDown += BtnLMouse_TouchDown;
                    mouseButtons.Add(b);
                    break;

                case "MMouse":
                    b.PreviewTouchDown += BtnMMouse_TouchDown;
                    b.StylusDown += BtnMMouse_TouchDown;
                    mouseButtons.Add(b);
                    break;

                case "RMouse":
                    b.PreviewTouchDown += BtnRMouse_TouchDown;
                    b.StylusDown += BtnRMouse_TouchDown;
                    mouseButtons.Add(b);
                    break;

                case "HMouse":
                    b.PreviewTouchDown += BtnHover_TouchDown;
                    b.StylusDown += BtnHover_TouchDown;
                    mouseButtons.Add(b);
                    break;

                case "KeyPress":
                    b.PreviewTouchDown += KeyPress_Down;
                    b.PreviewTouchUp += KeyPress_Up;
                    b.StylusDown += KeyPress_Down;
                    b.StylusUp += KeyPress_Up;
                    break;

                case "KeyPress2":
                    b.PreviewTouchDown += KeyPress2_Down;
                    b.StylusDown += KeyPress2_Down;
                    break;

                case "KeyToggle":
                    b.PreviewTouchDown += KeyToggle_Down;
                    b.StylusDown += KeyToggle_Down;
                    break;

                case "ShowKeyboard":
                    b.PreviewTouchDown += ShowKeyboard_TouchDown;
                    b.StylusDown += ShowKeyboard_TouchDown;
                    break;

                default:
                    // ? warn here
                    break;
            }

            return b;
        }

        
        private void Window_Closing(object sender, EventArgs e)
        {
            
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            //may need to force loss of focus here
        }

        private bool IsZero(CursorPosition.PointInter p)
        {
            return p.X == 0 && p.Y == 0;
        }

        private void Window_StylusDown(object sender, RoutedEventArgs e)
        {
            mouseDisabler.DisableMouse();
        }

        private void Window_TouchDown(object sender, TouchEventArgs e)
        {
            mouseDisabler.DisableMouse(); 
            //e.Handled = true;
        }

        private void Window_TouchMove(object sender, TouchEventArgs e)
        {
            //e.Handled = true;
        }

        private void Window_TouchUp(object sender, TouchEventArgs e)
        {
            //e.Handled = true;
        }

        private void ShowKeyboard_TouchDown(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            try
            {                
                ProcessStartInfo p = new ProcessStartInfo(Environment.GetFolderPath(Environment.SpecialFolder.System)+"\\cmd.exe", "/c start %WINDIR%\\System32\\osk.exe");
                p.UseShellExecute = true;
                p.CreateNoWindow = true;
                p.WindowStyle = ProcessWindowStyle.Hidden;
                p.RedirectStandardOutput = false;
                p.RedirectStandardInput = false;
                p.RedirectStandardError = false;
                
                Process.Start(p);

            } catch (Exception er)
            {
                MessageBox.Show(er.ToString());
            }
        }

        //gestures
        private struct GestureRuntimeProps
        {
            public DispatcherTimer Timer { get; set; }
            public List<CursorPosition.PointInter> CursorHistory { get; set; }
            public System.Drawing.Rectangle StartArea { get; set; }
            public System.Drawing.Rectangle EndArea { get; set; }
            public int DivertTolerance { get; set; }
            public int ZigCardinalSize { get; set; }
            public CursorPosition.PointInter StartPos { get; set; }
            public bool Started { get; set; }
            public DateTime StartTime { get; set; }
            public bool DisableTimeout { get; set; }
            public GestureState State { get; set; }
            public List<GestureQuadrant> QuadrantHistory { get; set; }
            public GestureQuadrant StartQuadrant { get; set; }

            public GestureOverlay GestureOverlay { get; set; }
            public GesturePanOverlay GesturePanOverlay { get; set; }
            public Dictionary<string, GestureKeyState> ZigKeyStates { get; set; }
            public System.Drawing.Rectangle[] ZigZones { get; set; }
            public int CurrentZigZone { get; set; }
        }

        enum GestureZigZone
        {
            TopLeft = 0,
            TopMiddle,
            TopRight,
            MiddleLeft,
            MiddleMiddle,
            MiddleRight,
            BottomLeft,
            BottomMiddle,
            BottomRight,
            ZIG_ZONE_MAX
        }

        enum GestureKeyState
        {
            PendingUp,
            Up,
            PendingDown,
            Down
        }

        enum GestureState 
        {
            None,
            ZigLeftHalf,
            ZigLeft,
            ZigRightHalf,
            ZigRight,
            ZigUpHalf,
            ZigUp,
            ZigDownHalf,
            ZigDown,
            CircleCW,
            CircleCCW,
        }

        enum GestureQuadrant
        {
            None,
            TopRight,
            TopLeft,
            BottomLeft,
            BottomRight,
        }

        private void ApplyGestureKeyDown(string gestKey)
        {
            if (gestureProps.ZigKeyStates[gestKey] == GestureKeyState.Up)
            {
                gestureProps.ZigKeyStates[gestKey] = GestureKeyState.PendingDown;
            }
            if (gestureProps.ZigKeyStates[gestKey] == GestureKeyState.PendingUp)
            {
                gestureProps.ZigKeyStates[gestKey] = GestureKeyState.Down;
            }
        }

        private void ApplyGestureKeyUp(string gestKey)
        {
            if (gestureProps.ZigKeyStates[gestKey] == GestureKeyState.Down)
            {
                gestureProps.ZigKeyStates[gestKey] = GestureKeyState.PendingUp;
            }
            if (gestureProps.ZigKeyStates[gestKey] == GestureKeyState.PendingDown)
            {
                gestureProps.ZigKeyStates[gestKey] = GestureKeyState.Up;
            }
        }

        private void SendGestureKeyDown()
        {
            string[] keys = new string[gestureProps.ZigKeyStates.Keys.Count];
            gestureProps.ZigKeyStates.Keys.CopyTo(keys, 0);
            foreach (string key in keys)
            {
                if (gestureProps.ZigKeyStates[key] == GestureKeyState.PendingDown)
                {
                    SendGestureKey(key, false);
                    gestureProps.ZigKeyStates[key] = GestureKeyState.Down;
                }
            }
        }

        private void SendGestureKeyUp(bool all=true)
        {
            string[] keys = new string[gestureProps.ZigKeyStates.Keys.Count];
            gestureProps.ZigKeyStates.Keys.CopyTo(keys, 0);
            foreach (string key in keys)
            {
                if (gestureProps.ZigKeyStates[key] == GestureKeyState.PendingUp)
                {
                    SendGestureKey(key, true);
                    gestureProps.ZigKeyStates[key] = GestureKeyState.Up;
                }
                if (all && gestureProps.ZigKeyStates[key] == GestureKeyState.Down)
                {
                    SendGestureKey(key, true);
                    gestureProps.ZigKeyStates[key] = GestureKeyState.Up;
                }
            }
        }

        private void SendGestureKey(string gesture, bool up)
        {
            if (Config.Gestures.ContainsKey(gesture))
            {
                var r = InputHelper.GetKeyType(Config.Gestures[gesture].TypeParam);
                if (r.Dxkeyfound)
                    InputHelper.SendKey(r.Dxkey, up, InputHelper.InputType.Keyboard);
                else if (r.Vkeyfound)
                    InputHelper.SendKey(r.Vkey, up, InputHelper.InputType.Keyboard);
            }
        }

        private void GestureStartPan()
        {
            gestureProps.State = GestureState.ZigDown;
            gestureProps.GestureOverlay.Show();
            gestureProps.GesturePanOverlay.Show();
            gestureProps.DisableTimeout = true;
            if (Config.SendGestureMouseUp)
                InputHelper.SendMouse((uint)InputHelper.MouseEventF.MOUSEEVENTF_LEFTUP);
        }

        private void GestureTimer_Tick(object sender, EventArgs e)
        {
            var cPos = CursorPosition.Pos();
            if (!gestureProps.Started && gestureProps.StartArea.Contains(cPos.X,cPos.Y))
            {
                gestureProps.CursorHistory.Clear();
                gestureProps.QuadrantHistory.Clear();
                gestureProps.State = GestureState.None;
                gestureProps.Started = true;
                gestureProps.StartTime = DateTime.Now;
                gestureProps.StartPos = cPos;
                gestureProps.StartQuadrant = GetGestureQuadrant(cPos, gestureProps.EndArea, cPos);
                gestureProps.DisableTimeout = false;

                gestureProps.CursorHistory.Add(cPos);
                //TODO show WS_EX_TRANSPARENT gesture area
            }

            if (gestureProps.Started)
            {
                gestureProps.CursorHistory.Add(cPos);

                //detect cursor OOB to end gesture detection
                if (!gestureProps.EndArea.Contains(cPos.X, cPos.Y))
                {
                    if (gestureProps.State == GestureState.ZigDown)
                        SendGestureKeyUp();
                    
                    gestureProps.Started = false;
                    gestureProps.State = GestureState.None;
                    gestureProps.GestureOverlay.Hide();
                    gestureProps.GesturePanOverlay.Hide();
                    return;
                }

                //detect movement into new third and update keypresses
                if (gestureProps.State == GestureState.ZigDown)
                {
                    for (int i=0; i<gestureProps.ZigZones.Length; i++)
                    {
                        if (gestureProps.ZigZones[i].Contains(cPos.X,cPos.Y) && gestureProps.CurrentZigZone!=i)
                        {
                            switch ((GestureZigZone)i)
                            {
                                case GestureZigZone.TopLeft:
                                    ApplyGestureKeyUp("ZigRight");
                                    ApplyGestureKeyUp("ZigDown");
                                    ApplyGestureKeyDown("ZigLeft");
                                    ApplyGestureKeyDown("ZigUp");
                                    gestureProps.DisableTimeout = true;
                                    break;
                                case GestureZigZone.TopMiddle:
                                    ApplyGestureKeyUp("ZigRight");
                                    ApplyGestureKeyUp("ZigDown");
                                    ApplyGestureKeyUp("ZigLeft");
                                    ApplyGestureKeyDown("ZigUp");
                                    gestureProps.DisableTimeout = true;
                                    break;
                                case GestureZigZone.TopRight:
                                    ApplyGestureKeyDown("ZigRight");
                                    ApplyGestureKeyUp("ZigDown");
                                    ApplyGestureKeyUp("ZigLeft");
                                    ApplyGestureKeyDown("ZigUp");
                                    gestureProps.DisableTimeout = true;
                                    break;
                                case GestureZigZone.MiddleLeft:
                                    ApplyGestureKeyUp("ZigRight");
                                    ApplyGestureKeyUp("ZigDown");
                                    ApplyGestureKeyDown("ZigLeft");
                                    ApplyGestureKeyUp("ZigUp");
                                    gestureProps.DisableTimeout = true;
                                    break;
                                case GestureZigZone.MiddleMiddle:
                                    ApplyGestureKeyUp("ZigRight");
                                    ApplyGestureKeyUp("ZigDown");
                                    ApplyGestureKeyUp("ZigLeft");
                                    ApplyGestureKeyUp("ZigUp");
                                    if (gestureProps.DisableTimeout)
                                    {
                                        gestureProps.DisableTimeout = false;
                                        gestureProps.StartTime = DateTime.Now;
                                    }
                                    break;
                                case GestureZigZone.MiddleRight:
                                    ApplyGestureKeyDown("ZigRight");
                                    ApplyGestureKeyUp("ZigDown");
                                    ApplyGestureKeyUp("ZigLeft");
                                    ApplyGestureKeyUp("ZigUp");
                                    gestureProps.DisableTimeout = true;
                                    break;
                                case GestureZigZone.BottomLeft:
                                    ApplyGestureKeyUp("ZigRight");
                                    ApplyGestureKeyDown("ZigDown");
                                    ApplyGestureKeyDown("ZigLeft");
                                    ApplyGestureKeyUp("ZigUp");
                                    gestureProps.DisableTimeout = true;
                                    break;
                                case GestureZigZone.BottomMiddle:
                                    ApplyGestureKeyUp("ZigRight");
                                    ApplyGestureKeyDown("ZigDown");
                                    ApplyGestureKeyUp("ZigLeft");
                                    ApplyGestureKeyUp("ZigUp");
                                    gestureProps.DisableTimeout = true;
                                    break;
                                case GestureZigZone.BottomRight:
                                    ApplyGestureKeyDown("ZigRight");
                                    ApplyGestureKeyDown("ZigDown");
                                    ApplyGestureKeyUp("ZigLeft");
                                    ApplyGestureKeyUp("ZigUp");
                                    gestureProps.DisableTimeout = true;
                                    break;
                            }

                            SendGestureKeyUp(false);
                            SendGestureKeyDown();
                            break;
                        }
                    }
                }

                //detect cardinal direction finish gesture step
                if (gestureProps.State == GestureState.ZigDownHalf)
                {
                    if (cPos.Y > gestureProps.StartPos.Y && Math.Abs(cPos.X - gestureProps.StartPos.X) <= gestureProps.DivertTolerance)
                    {
                        GestureStartPan();
                    }
                    else if (Math.Abs(cPos.X - gestureProps.StartPos.X) > gestureProps.DivertTolerance)
                    {
                        gestureProps.DisableTimeout = false;
                        gestureProps.State = GestureState.None;
                    }
                } 
                else if (gestureProps.State == GestureState.ZigUpHalf)
                {
                    if (cPos.Y < gestureProps.StartPos.Y && Math.Abs(cPos.X - gestureProps.StartPos.X) <= gestureProps.DivertTolerance)
                    {
                        GestureStartPan();
                    }
                    else if (Math.Abs(cPos.X - gestureProps.StartPos.X) > gestureProps.DivertTolerance)
                    {
                        gestureProps.DisableTimeout = false;
                        gestureProps.State = GestureState.None;
                    }
                }

                //detect panning start gesture
                if (gestureProps.State == GestureState.None)
                {
                    //detect zigs
                    if (cPos.Y < gestureProps.StartPos.Y - gestureProps.ZigCardinalSize && Math.Abs(cPos.X - gestureProps.StartPos.X) <= gestureProps.DivertTolerance)
                    {
                        gestureProps.State = GestureState.ZigDownHalf; //zig down starts by moving up
                    }
                    else if (cPos.Y > gestureProps.StartPos.Y + gestureProps.ZigCardinalSize && Math.Abs(cPos.X - gestureProps.StartPos.X) <= gestureProps.DivertTolerance)
                    {
                        gestureProps.State = GestureState.ZigUpHalf; //zig down starts by moving up
                    }
                }

                //always check for CCW and CW quadrant movement, reset startTime and gesture state after each trigger
                if (gestureProps.State != GestureState.ZigDown)
                {
                    var q = GetGestureQuadrant(cPos, gestureProps.EndArea, gestureProps.StartPos);
                    if (q != GestureQuadrant.None)
                    {
                        if (gestureProps.QuadrantHistory.Count == 0 || q != gestureProps.QuadrantHistory[gestureProps.QuadrantHistory.Count - 1])
                            gestureProps.QuadrantHistory.Add(q);
                    }

                    if (gestureProps.QuadrantHistory.Count > 1 && Config.ShowGestureOverlay)
                    {
                        gestureProps.GestureOverlay.Show();
                    }

                    if (gestureProps.QuadrantHistory.Count > 4)
                        gestureProps.QuadrantHistory.RemoveAt(0);

                    if (gestureProps.QuadrantHistory.Count == 4 && gestureProps.QuadrantHistory[0] == gestureProps.StartQuadrant)
                    {
                        //TODO: optimize
                        int scrolldir = 0; //-1 = ccw, 1 = cw
                        var q1 = gestureProps.QuadrantHistory[1];
                        var q2 = gestureProps.QuadrantHistory[2];
                        var q3 = gestureProps.QuadrantHistory[3];
                        switch (gestureProps.StartQuadrant)
                        {
                            case GestureQuadrant.BottomLeft:
                                if (q1 == GestureQuadrant.BottomRight && q2 == GestureQuadrant.TopRight && q3 == GestureQuadrant.TopLeft)
                                    scrolldir = -1;
                                else if (q1 == GestureQuadrant.TopLeft && q2 == GestureQuadrant.TopRight && q3 == GestureQuadrant.BottomRight)
                                    scrolldir = 1;
                                break;

                            case GestureQuadrant.BottomRight:
                                if (q1 == GestureQuadrant.TopRight && q2 == GestureQuadrant.TopLeft && q3 == GestureQuadrant.BottomLeft)
                                    scrolldir = -1;
                                else if (q1 == GestureQuadrant.BottomLeft && q2 == GestureQuadrant.TopLeft && q3 == GestureQuadrant.TopRight)
                                    scrolldir = 1;
                                break;

                            case GestureQuadrant.TopLeft:
                                if (q1 == GestureQuadrant.BottomLeft && q2 == GestureQuadrant.BottomRight && q3 == GestureQuadrant.TopRight)
                                    scrolldir = -1;
                                else if (q1 == GestureQuadrant.TopRight && q2 == GestureQuadrant.BottomRight && q3 == GestureQuadrant.BottomLeft)
                                    scrolldir = 1;
                                break;

                            case GestureQuadrant.TopRight:
                                if (q1 == GestureQuadrant.TopLeft && q2 == GestureQuadrant.BottomLeft && q3 == GestureQuadrant.BottomRight)
                                    scrolldir = -1;
                                else if (q1 == GestureQuadrant.BottomRight && q2 == GestureQuadrant.BottomLeft && q3 == GestureQuadrant.TopLeft)
                                    scrolldir = 1;
                                break;
                        }

                        if (scrolldir == -1)
                        {
                            //wheelup and reset history, timeout, state
                            InputHelper.SendMouse((uint)InputHelper.MouseEventF.MOUSEEVENTF_WHEEL, unchecked((uint)(InputHelper.WHEEL_DELTA * -1)));
                            gestureProps.QuadrantHistory.Clear();
                            gestureProps.StartTime = DateTime.Now;
                            if (gestureProps.State != GestureState.None)
                                SendGestureKeyUp();
                            gestureProps.State = GestureState.CircleCCW;
                            if (Config.SendGestureMouseUp)
                                InputHelper.SendMouse((uint)InputHelper.MouseEventF.MOUSEEVENTF_LEFTUP);
                        }
                        else if (scrolldir == 1)
                        {
                            //wheeldown and reset history, timeout, state
                            InputHelper.SendMouse((uint)InputHelper.MouseEventF.MOUSEEVENTF_WHEEL, unchecked((uint)(InputHelper.WHEEL_DELTA)));
                            gestureProps.QuadrantHistory.Clear();
                            gestureProps.StartTime = DateTime.Now;
                            if (gestureProps.State != GestureState.None)
                                SendGestureKeyUp();
                            gestureProps.State = GestureState.CircleCW;
                            if (Config.SendGestureMouseUp)
                                InputHelper.SendMouse((uint)InputHelper.MouseEventF.MOUSEEVENTF_LEFTUP);
                        }
                    }
                }

                //timeout
                if (!gestureProps.DisableTimeout && DateTime.Now.Subtract(gestureProps.StartTime) >= TimeSpan.FromMilliseconds(GESTURE_TIMEOUT_MS))
                {
                    if (gestureProps.State == GestureState.ZigDown)
                        SendGestureKeyUp();
                    gestureProps.Started = false;
                    gestureProps.State = GestureState.None;
                    gestureProps.GestureOverlay.Hide();
                    gestureProps.GesturePanOverlay.Hide();
                }                
            }
            
            if (gestureProps.CursorHistory.Count > GESTURE_MAX_HISTORY)
            {
                gestureProps.CursorHistory.RemoveAt(0);
            }
        }

        private GestureQuadrant GetGestureQuadrant(CursorPosition.PointInter p, System.Drawing.Rectangle r, CursorPosition.PointInter st)
        {
            var cx = r.X + r.Width / 2;
            var cy = r.Y + r.Height / 2;
            //var cx = st.X;
            //var cy = st.Y;

            if (p.X > cx + GESTURE_CIRCLE_MARGIN && p.Y > cy + GESTURE_CIRCLE_MARGIN) return GestureQuadrant.BottomRight;

            if (p.X < cx - GESTURE_CIRCLE_MARGIN && p.Y > cy + GESTURE_CIRCLE_MARGIN) return GestureQuadrant.BottomLeft;

            if (p.X > cx + GESTURE_CIRCLE_MARGIN && p.Y < cy - GESTURE_CIRCLE_MARGIN) return GestureQuadrant.TopRight;

            if (p.X < cx - GESTURE_CIRCLE_MARGIN && p.Y < cy - GESTURE_CIRCLE_MARGIN) return GestureQuadrant.TopLeft;

            return GestureQuadrant.None;
        }

        //dynamic scrolling        
        private double lastScrollY;
        private CursorPosition.PointInter scrollStartPos;
        private void BtnScArea_TouchDown(object sender, TouchEventArgs e)
        {
            scrollStartPos = CursorPosition.GetLastPos();
            lastScrollY = e.GetTouchPoint(this).Position.Y;
        }

        private void BtnScArea_TouchMove(object sender, TouchEventArgs e)
        {
            CursorPosition.MoveCursorToLastGood();
            var Y = e.GetTouchPoint(this).Position.Y;
            var dif = Y - lastScrollY;
            if (dif * dif > 0)
            { // ie > 3,< -3 px
                float pn = 1;
                if (dif < 0)
                    pn = -1;
                var amount = InputHelper.WHEEL_DELTA * pn;
                InputHelper.SendMouse((uint)(InputHelper.MouseEventF.MOUSEEVENTF_WHEEL), unchecked((uint)amount));
                lastScrollY = Y;
            }
        }

        private void BtnScArea_TouchUp(object sender, TouchEventArgs e)
        {
            InputHelper.SendMouse((uint)(InputHelper.MouseEventF.MOUSEEVENTF_ABSOLUTE), 0, scrollStartPos.X, scrollStartPos.Y);
            mouseDisabler.EnableMouse();
        }

        //simple programmable buttons
        private bool isHidden = false;
        private DateTime hideTouchDownTime;
        private void BtnShowHide_TouchDown(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            hideTouchDownTime = DateTime.Now;
            isHidden = !isHidden;
            for (var i=0; i<mainGrid.Children.Count; i++)
            {
                try
                {
                    var b = (Button)mainGrid.Children[i];
                    if (b!=sender)
                    {
                        b.Visibility = isHidden ? Visibility.Hidden : Visibility.Visible;
                    }
                }
                catch
                {}
            }
            (sender as Button).Content = isHidden ? "+" : "-";
            if (Config.MouseInterceptMode)
            {
                ResetButtonModeStyles();
            }
            mouseDisabler.EnableMouse();
        }

        private void BtnShowHide_TouchUp(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (DateTime.Now.Subtract(hideTouchDownTime).TotalMilliseconds >= 2000.0f)
            {
                Application.Current.Shutdown();
            }
        }

        private DispatcherTimer scrollRepeatTimer;
        private void BtnScUp_TouchDown(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var b = (Button)sender;
            var t = 1;
            if (Config.Buttons[b.Name].TypeFlag > 1)
                t = (int)Config.Buttons[b.Name].TypeFlag;

            for (var i = 0; i < t; i++)
            {
                InputHelper.SendMouse((uint)InputHelper.MouseEventF.MOUSEEVENTF_WHEEL, InputHelper.WHEEL_DELTA);
                Thread.Sleep(17); //TODO: configable
            }
            
            if (Config.Buttons[b.Name].RepeatDelay > 0)
            {
                if (scrollRepeatTimer != null)
                    scrollRepeatTimer.Stop();

                scrollRepeatTimer = new DispatcherTimer();
                scrollRepeatTimer.Interval = TimeSpan.FromMilliseconds(Config.Buttons[b.Name].RepeatDelay);
                scrollRepeatTimer.Tick += ScUp_Repeat;
                scrollRepeatTimer.Start();
            } 
            else
            {
                mouseDisabler.EnableMouse();
            }
        }

        private void ScUp_Repeat(object sender, EventArgs e)
        {
            CursorPosition.MoveCursorToLastGood();
            InputHelper.SendMouse((uint)InputHelper.MouseEventF.MOUSEEVENTF_WHEEL, InputHelper.WHEEL_DELTA);
        }

        private void BtnScUp_TouchUp(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (scrollRepeatTimer != null)
            {
                mouseDisabler.EnableMouse();
                scrollRepeatTimer.Stop();
            }
            scrollRepeatTimer = null;
        }

        private void BtnScDn_TouchDown(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var b = (Button)sender;
            var t = 1;
            if (Config.Buttons[b.Name].TypeFlag > 1)
                t = (int)Config.Buttons[b.Name].TypeFlag;

            for (var i=0; i<t; i++)
            {
                InputHelper.SendMouse((uint)InputHelper.MouseEventF.MOUSEEVENTF_WHEEL, unchecked((uint)(InputHelper.WHEEL_DELTA * -1)));
                Thread.Sleep(17); //TODO: configable
            }

            if (Config.Buttons[b.Name].RepeatDelay > 0)
            {
                if (scrollRepeatTimer != null)
                    scrollRepeatTimer.Stop();

                scrollRepeatTimer = new DispatcherTimer();
                scrollRepeatTimer.Interval = TimeSpan.FromMilliseconds(Config.Buttons[b.Name].RepeatDelay);
                scrollRepeatTimer.Tick += ScDn_Repeat;
                scrollRepeatTimer.Start();
            } 
            else
            {
                mouseDisabler.EnableMouse();
            }
        }

        private void ScDn_Repeat(object sender, EventArgs e)
        {
            //mouseDisabler.EnableMouse(false);
            CursorPosition.MoveCursorToLastGood();
            InputHelper.SendMouse((uint)InputHelper.MouseEventF.MOUSEEVENTF_WHEEL, unchecked((uint)(InputHelper.WHEEL_DELTA * -1)));
            //mouseDisabler.DisableMouse();
        }

        private void BtnScDn_TouchUp(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (scrollRepeatTimer != null)
            {
                mouseDisabler.EnableMouse();
                scrollRepeatTimer.Stop();
            }
            scrollRepeatTimer = null;
        }

        private void KeyPress_Down(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var b = (Button)sender;
            var r = InputHelper.GetKeyType(Config.Buttons[b.Name].TypeParam);
            if (r.Dxkeyfound)
            {
                switch (Config.Buttons[b.Name].TypeFlag)
                {
                    case 1:
                        InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_LCONTROL, false, InputHelper.InputType.Keyboard);
                        break;
                    case 2:
                        InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_LALT, false, InputHelper.InputType.Keyboard);
                        break;
                    case 3:
                        InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_LSHIFT, false, InputHelper.InputType.Keyboard);
                        break;
                }
                InputHelper.SendKey(r.Dxkey, false, InputHelper.InputType.Keyboard);
            }
            else if (r.Vkeyfound)
            {
                InputHelper.SendKey(r.Vkey, false, InputHelper.InputType.Keyboard);
            }
        }
        
        private void KeyPress_Up(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var b = (Button)sender;
            var r = InputHelper.GetKeyType(Config.Buttons[b.Name].TypeParam);
            if (r.Dxkeyfound)
            {
                InputHelper.SendKey(r.Dxkey, true, InputHelper.InputType.Keyboard);
                switch (Config.Buttons[b.Name].TypeFlag)
                {
                    case 1:
                        InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_LCONTROL, true, InputHelper.InputType.Keyboard);
                        break;
                    case 2:
                        InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_LALT, true, InputHelper.InputType.Keyboard);
                        break;
                    case 3:
                        InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_LSHIFT, true, InputHelper.InputType.Keyboard);
                        break;
                }
            }
            else if (r.Vkeyfound)
            {
                InputHelper.SendKey(r.Vkey, true, InputHelper.InputType.Keyboard);
            }
            mouseDisabler.EnableMouse();
        }

        private void KeyPress2_Down(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var b = (Button)sender;
            var r = InputHelper.GetKeyType(Config.Buttons[b.Name].TypeParam);
            if (r.Dxkeyfound)
            {
                InputHelper.SendKey(r.Dxkey, false, InputHelper.InputType.Keyboard);
                InputHelper.SendKey(r.Dxkey, true, InputHelper.InputType.Keyboard);
                Thread.Sleep((int)Config.Buttons[b.Name].TypeFlag);
                InputHelper.SendKey(r.Dxkey, false, InputHelper.InputType.Keyboard);
                InputHelper.SendKey(r.Dxkey, true, InputHelper.InputType.Keyboard);
            }
            else if (r.Vkeyfound)
            {
                InputHelper.SendKey(r.Vkey, false, InputHelper.InputType.Keyboard);
                InputHelper.SendKey(r.Vkey, true, InputHelper.InputType.Keyboard);
                Thread.Sleep((int)Config.Buttons[b.Name].TypeFlag);
                InputHelper.SendKey(r.Vkey, false, InputHelper.InputType.Keyboard);
                InputHelper.SendKey(r.Vkey, true, InputHelper.InputType.Keyboard);
            }
            mouseDisabler.EnableMouse();
        }

        private void KeyToggle_Down(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var b = (Button)sender;
            var r = InputHelper.GetKeyType(Config.Buttons[b.Name].TypeParam);
            
            if (toggledKeys.ContainsKey(b.Name)) //turn it off
            {
                if (r.Dxkeyfound)
                {                    
                    InputHelper.SendKey(r.Dxkey, true, InputHelper.InputType.Keyboard);                    
                }
                else if (r.Vkeyfound)
                {
                    InputHelper.SendKey(r.Vkey, true, InputHelper.InputType.Keyboard);
                }
                styleButton(b, false);
                toggledKeys.Remove(b.Name);
            }
            else //turn it on
            {
                if (r.Dxkeyfound)
                {
                    InputHelper.SendKey(r.Dxkey, false, InputHelper.InputType.Keyboard);
                }
                else if (r.Vkeyfound)
                {
                    InputHelper.SendKey(r.Vkey, false, InputHelper.InputType.Keyboard);
                }
                styleButton(b, true);
                toggledKeys[b.Name] = b;
            }

            mouseDisabler.EnableMouse();
        }
        
        /*
         * Special F keys+scrollock for AHK to pick up for mouse events
         */
        private string mButtonMode = "";
        private void BtnMMouse_TouchDown(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (Config.MouseInterceptMode)
            {
                if (mButtonMode == "")
                {
                    ResetButtonModeStyles();
                    mButtonMode = "down";
                    interceptMode = 2;
                    interceptButton.Visibility = Visibility.Visible;
                    styleButton((Button)sender, true);
                }
                else
                {
                    mButtonMode = "";
                    interceptMode = 0;
                    ResetButtonModeStyles();
                }
            }
            else
            {
                mouseDisabler.EnableMouse(false);
                if (mButtonMode == "")
                {
                    InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_F14, false, InputHelper.InputType.Keyboard);
                    InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_F14, true, InputHelper.InputType.Keyboard);
                    //InputHelper.SendMouse((uint)(InputHelper.MouseEventF.MOUSEEVENTF_MIDDLEDOWN));
                    mButtonMode = "down";
                    styleButton((Button)sender, true);
                }
                else if (mButtonMode == "down")
                {
                    InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_F14, false, InputHelper.InputType.Keyboard);
                    InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_F14, true, InputHelper.InputType.Keyboard);
                    //InputHelper.SendMouse((uint)(InputHelper.MouseEventF.MOUSEEVENTF_MIDDLEUP));
                    styleButton((Button)sender, false);
                    mButtonMode = "";
                }
            }
        }

        private void Intercept_TouchDown(object sender, RoutedEventArgs e)
        {
            double x, y;
            try
            {
                TouchEventArgs ee = (TouchEventArgs)e;
                x = ee.GetTouchPoint(null).Position.X;
                y = ee.GetTouchPoint(null).Position.Y;
            } 
            catch (Exception ex)
            {
                StylusEventArgs ee = (StylusEventArgs)e;
                x = ee.GetPosition(null).X;
                y = ee.GetPosition(null).Y;
            }
            e.Handled = true;
            mouseDisabler.EnableMouse(false);

            //hide window
            CursorPosition.MoveCursorTo(x,y); 
            this.WindowState = WindowState.Minimized;
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(16);
            timer.Tick += InterceptDownTimer;
            timer.Start();            
        }

        private InputHelper.MouseEventF GetInterceptButton(bool up)
        {
            switch (interceptMode)
            {
                case 0:
                    return InputHelper.MouseEventF.MOUSEEVENTF_MOVE;
                case 1:
                    return (!up) ? InputHelper.MouseEventF.MOUSEEVENTF_LEFTDOWN : InputHelper.MouseEventF.MOUSEEVENTF_LEFTUP;
                case 2:
                    return (!up) ? InputHelper.MouseEventF.MOUSEEVENTF_MIDDLEDOWN : InputHelper.MouseEventF.MOUSEEVENTF_MIDDLEUP;
                case 3:
                    return (!up) ? InputHelper.MouseEventF.MOUSEEVENTF_RIGHTDOWN : InputHelper.MouseEventF.MOUSEEVENTF_RIGHTUP;
            }

            return InputHelper.MouseEventF.MOUSEEVENTF_MOVE;
        }

        private void InterceptDownTimer(object sender, EventArgs e)
        {
            if (interceptMode != 0)
            {
                InputHelper.SendMouse((uint)(GetInterceptButton(false)));
            }
            else
            {
                InputHelper.SendMouse((uint)(InputHelper.MouseEventF.MOUSEEVENTF_MOVE | GetInterceptButton(false)), 0, 1, 0);
            }
            ((DispatcherTimer)sender).Stop();
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(22);
            timer.Tick += InterceptUpTimer;
            timer.Start();
        }

        private void InterceptUpTimer(object sender, EventArgs e)
        {
            if (interceptMode != 0)
            {
                InputHelper.SendMouse((uint)(GetInterceptButton(true)));
            }
            ((DispatcherTimer)sender).Stop();
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += InterceptShowWindow;
            timer.Start();
        }

        private void InterceptShowWindow(object sender, EventArgs e)
        {
            if (interceptMode==0)
            {
                mouseDisabler.EnableMouse(false);
                this.WindowState = WindowState.Maximized;
                //ResetButtonModeStyles();
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
            ((DispatcherTimer)sender).Stop();
           
        }

        private void Intercept_TouchUp(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void ResetButtonModeStyles()
        {
            mButtonMode = "";
            rButtonMode = "";
            lButtonMode = "";
            hoverMode = "";
            foreach (var b in mouseButtons)
            {
                styleButton(b, false);
            }
            interceptButton.Visibility = Visibility.Hidden;
            this.WindowState = WindowState.Minimized;
            this.WindowState = WindowState.Maximized;
        }

        private string rButtonMode = "";
        private void BtnRMouse_TouchDown(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (Config.MouseInterceptMode)
            {
                if (rButtonMode == "")
                {
                    ResetButtonModeStyles();
                    interceptMode = 3;
                    rButtonMode = "down";
                    interceptButton.Visibility = Visibility.Visible;
                    styleButton((Button)sender, true);
                }
                else
                {
                    interceptMode = 0;
                    rButtonMode = "";
                    ResetButtonModeStyles();
                }
            }
            else
            {
                mouseDisabler.EnableMouse(false);
                if (rButtonMode == "")
                {
                    InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_F15, false, InputHelper.InputType.Keyboard);
                    InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_F15, true, InputHelper.InputType.Keyboard);
                    //InputHelper.SendMouse((uint)(InputHelper.MouseEventF.MOUSEEVENTF_RIGHTDOWN));
                    rButtonMode = "down";
                    styleButton((Button)sender, true);
                }
                else if (rButtonMode == "down")
                {
                    InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_F15, false, InputHelper.InputType.Keyboard);
                    InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_F15, true, InputHelper.InputType.Keyboard);
                    //InputHelper.SendMouse((uint)(InputHelper.MouseEventF.MOUSEEVENTF_RIGHTUP));
                    styleButton((Button)sender, false);
                    rButtonMode = "";
                }
            }
        }

        private string lButtonMode = "";
        private void BtnLMouse_TouchDown(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (Config.MouseInterceptMode)
            {
                if (lButtonMode == "")
                {
                    ResetButtonModeStyles();
                    lButtonMode = "down";
                    interceptMode = 1;
                    interceptButton.Visibility = Visibility.Visible;
                    styleButton((Button)sender, true);
                }
                else
                {
                    lButtonMode = "";
                    interceptMode = 0;
                    ResetButtonModeStyles();
                }
            } 
            else
            {
                mouseDisabler.EnableMouse(false);
                if (lButtonMode == "")
                {
                    InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_SCROLL, false, InputHelper.InputType.Keyboard);
                    InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_SCROLL, true, InputHelper.InputType.Keyboard);
                    lButtonMode = "down";
                    styleButton((Button)sender, true);
                }
                else if (lButtonMode == "down")
                {
                    InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_SCROLL, false, InputHelper.InputType.Keyboard);
                    InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_SCROLL, true, InputHelper.InputType.Keyboard);
                    styleButton((Button)sender, false);
                    lButtonMode = "";
                }
            }            
        }

        private string hoverMode = ""; //this mode good for reading tooltips
        private void BtnHover_TouchDown(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (Config.MouseInterceptMode)
            {
                if (hoverMode == "")
                {
                    ResetButtonModeStyles();
                    hoverMode = "down";
                    interceptMode = 0;
                    interceptButton.Visibility = Visibility.Visible;
                    styleButton((Button)sender, true);
                }
                else
                {
                    hoverMode = "";
                    interceptMode = 0;
                    ResetButtonModeStyles();
                }
            }
            else
            {
                mouseDisabler.EnableMouse(false);
                if (hoverMode == "")
                {
                    InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_F13, false, InputHelper.InputType.Keyboard);
                    InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_F13, true, InputHelper.InputType.Keyboard);
                    hoverMode = "down";
                    styleButton((Button)sender, true);
                }
                else if (hoverMode == "down")
                {
                    InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_F13, false, InputHelper.InputType.Keyboard);
                    InputHelper.SendKey(InputHelper.DirectXKeyStrokes.DIK_F13, true, InputHelper.InputType.Keyboard);
                    //btnHover.Content = "🖱☩";
                    hoverMode = "";
                    styleButton((Button)sender, false);
                }
            }
        }
               
        private void styleButton(Button btn, bool isOn)
        {
            btn.Background = isOn ? buttonColorOn:buttonColor;
        }

        
        private string ConfigDefault = @"{
	""Name"":""ParadoxNew"",
	""Width"":27,
	""Height"":18,
	""Opacity"":0.8,
    ""UseAutohotkey"":false,
    ""UseGestures"":true,
    ""SendGestureMouseUp"":true,
    ""ShowGestureOverlay"":true,
    ""GestureOverlayPanOpacity"": 0.1,
    ""MouseInterceptMode"":true,
    ""FontSize"":20,
    ""Gestures"": {
        ""ZigLeft"": {
          ""TypeParam"": ""DIK_A""
        },
        ""ZigRight"": {
          ""TypeParam"": ""DIK_D""
        },
        ""ZigUp"": {
          ""TypeParam"": ""DIK_W""
        },
        ""ZigDown"": {
          ""TypeParam"": ""DIK_S""
        }
    },
	""Buttons"": {
		""HideShow"": {
			""Content"":""-"",
			""Row"":17,
			""Column"":0,
			""Type"":""HideShow""
		},
		""LMouse"": {
			""Content"":""🖱L"",
			""Row"":17,
			""Column"":18,
			""Type"":""LMouse""
		},
		""MMouse"": {
			""Content"":""🖱M"",
			""Row"":17,
			""Column"":19,
			""Type"":""MMouse""
		},
		""RMouse"": {
			""Content"":""🖱R"",
			""Row"":17,
			""Column"":20,
			""Type"":""RMouse""
		},
		""HMouse"": {
			""Content"":""🖱☩"",
			""Row"":17,
			""Column"":6,
			""Type"":""HMouse""
		},
		""ScUp"": {
			""Content"":""🖱⭡"",
			""Row"":17,
			""Column"":4,
			""Type"":""ScrollUp"",
            ""RepeatDelay"":0
		},
		""ScDn"": {
			""Content"":""🖱⭣"",
			""Row"":17,
			""Column"":5,
			""Type"":""ScrollDown"",
            ""RepeatDelay"":0
		},
		""Esc"": {
			""Content"":""Esc"",
			""Row"": 11,
			""Column"":26,
			""Type"":""KeyPress"",
			""TypeParam"":""DIK_ESCAPE""
		},
		""Space"": {
			""Content"":""Spc"",
			""Row"": 12,
			""Column"":26,
            ""ColSpan"":1,
			""Type"":""KeyPress"",
			""TypeParam"":""DIK_SPACE""
		},
		""Enter"": {
			""Content"":""Entr"",
			""Row"": 13,
			""Column"":26,
			""Type"":""KeyPress"",
			""TypeParam"":""DIK_RETURN""
		},
        ""Tab"": {
			""Content"":""Tab"",
			""Row"": 14,
			""Column"":26,
			""Type"":""KeyPress"",
			""TypeParam"":""DIK_TAB""
		},
        ""Keyboard"": {
			""Content"":""🖮"",
			""Row"": 6,
			""Column"":26,
			""Type"":""ShowKeyboard""
		},
		""LShift"": {
			""Content"":""Shft"",
			""Row"": 7,
			""Column"":26,
			""Type"":""KeyPress"",
			""TypeParam"":""DIK_LSHIFT""
		},
		""LCtrl"": {
			""Content"":""Ctrl"",
			""Row"": 8,
			""Column"":26,
			""Type"":""KeyPress"",
			""TypeParam"":""DIK_LCONTROL""
		},
		""LAlt"": {
			""Content"":""Alt"",
			""Row"": 9,
			""Column"":26,
			""Type"":""KeyPress"",
			""TypeParam"":""DIK_LALT""
		}
	}
}";

        /*
        ""ScUp2"": {
			""Content"":""⭡⭡"",
			""Row"":17,
			""Column"":3,
			""Type"":""ScrollUp"",
			""TypeFlag"":5
		},
		""ScDn2"": {
			""Content"":""⭣⭣"",
			""Row"":17,
			""Column"":6,
			""Type"":""ScrollDown"",
			""TypeFlag"":5
		},
        */
    }
}
