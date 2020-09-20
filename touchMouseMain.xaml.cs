using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
        const int CURSOR_WATCHER_FRAMEDELAY = 4; //8 = 125hz, 4= 250hz etc
        const int MOUSE_DISABLER_DELAY = 10; 
        //make sure windows doesnt try to move the 'real' mouse cursor, and if so, move it back

        private WindowInteropHelper helper;
        private int origStyle;
        private MouseDisabler mouseDisabler;

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
            public bool MouseInterceptMode { get; set; }
            public string AutohotkeyFile { get; set; }
            public Dictionary<string, ButtonConfig> Buttons { get; set; }
        }

        public struct ButtonConfig
        {
            public string Content { get; set; }
            public int Column { get; set; }
            public int Row { get; set; }
            public int ColSpan { get; set; }
            public int RowSpan { get; set; }
            public string Type { get; set; } //KeyPress, KeyPress2, KeyToggle, HideShow, LMouse, RMouse, MMouse, HMouse, ScrollUp, ScrollDown, ScrollArea (todo)
            public string TypeParam { get; set; } //DIK_SPACE, LEFTSHIFT etc
            public float TypeFlag { get; set; } //for KeyPress types: 0 = no mod, 1 = ctrl, 2 = alt, 3 = shift
            public int RepeatDelay { get; set; } //if > 0, tells scrollup/down to repeat when held down
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
            //origStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_NOACTIVATE | WS_EX_LAYERED;
            origStyle = WS_EX_NOACTIVATE; //no taskbar icon or anything
            
            SetWindowLong(helper.Handle, GWL_EXSTYLE, origStyle);
            var source = PresentationSource.FromVisual(this) as HwndSource;
            source.AddHook(WndProc);
        }
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEACTIVATE)
            {
                handled = true;
                return new IntPtr(MA_NOACTIVATE);
            }
            else
            {
                return IntPtr.Zero;
            }
        }
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 0x0003;

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

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
            //b.StylusDown += Intercept_TouchDown;
            //b.StylusUp += Intercept_TouchUp;

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
            mouseDisabler.DisableMouse(); //TODO: configable
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
            InputHelper.SendMouse((uint)InputHelper.MouseEventF.MOUSEEVENTF_WHEEL, unchecked((uint)(InputHelper.WHEEL_DELTA * -1)));
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

        private void Intercept_TouchDown(object sender, TouchEventArgs e)
        {
            e.Handled = true;
            mouseDisabler.EnableMouse(false);

            //hide window
            CursorPosition.MoveCursorTo(e.GetTouchPoint(null).Position.X, e.GetTouchPoint(null).Position.Y); 
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
            timer.Interval = TimeSpan.FromMilliseconds(55);
            timer.Tick += InterceptShowWindow;
            timer.Start();
        }

        private void InterceptShowWindow(object sender, EventArgs e)
        {
            this.WindowState = WindowState.Maximized;
            ((DispatcherTimer)sender).Stop();
           
        }

        private void Intercept_TouchUp(object sender, TouchEventArgs e)
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
    ""MouseInterceptMode"":true,
    ""FontSize"":24,
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
			""Column"":17,
			""Type"":""HMouse""
		},
		""ScUp"": {
			""Content"":""🖱⭡"",
			""Row"":17,
			""Column"":4,
			""Type"":""ScrollUp"",
            ""RepeatDelay"":250
		},
		""ScDn"": {
			""Content"":""🖱⭣"",
			""Row"":17,
			""Column"":5,
			""Type"":""ScrollDown"",
            ""RepeatDelay"":250
		},
		""Esc"": {
			""Content"":""Esc"",
			""Row"": 17,
			""Column"":8,
			""Type"":""KeyPress"",
			""TypeParam"":""DIK_ESCAPE""
		},
		""Space"": {
			""Content"":""Spc"",
			""Row"": 17,
			""Column"":9,
            ""ColSpan"":2,
			""Type"":""KeyPress"",
			""TypeParam"":""DIK_SPACE""
		},
		""Enter"": {
			""Content"":""Entr"",
			""Row"": 17,
			""Column"":11,
			""Type"":""KeyPress"",
			""TypeParam"":""DIK_RETURN""
		},
        ""Tab"": {
			""Content"":""Tab"",
			""Row"": 17,
			""Column"":12,
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
		},
        ""left"": {
            ""Content"": ""←"",
            ""Column"": 25,
            ""Row"": 13,
            ""Type"": ""KeyPress"",
            ""TypeParam"": ""DIK_A""
        },
        ""right"": {
            ""Content"": ""→"",
            ""Column"": 26,
            ""Row"": 13,
            ""Type"": ""KeyPress"",
            ""TypeParam"": ""DIK_D""
        },
        ""up"": {
            ""Content"": ""↑"",
            ""Column"": 26,
            ""Row"": 12,
            ""Type"": ""KeyPress"",
            ""TypeParam"": ""DIK_W""
        },
        ""down"": {
            ""Content"": ""↓"",
            ""Column"": 26,
            ""Row"": 14,
            ""Type"": ""KeyPress"",
            ""TypeParam"": ""DIK_S""
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
