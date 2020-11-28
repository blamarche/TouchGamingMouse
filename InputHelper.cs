using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TouchGamingMouse
{
    /// <summary>
    /// My own question as reference: https://stackoverflow.com/questions/35138778/sending-keys-to-a-directx-game
    /// http://www.gamespp.com/directx/directInputKeyboardScanCodes.html
    /// </summary>
    public class InputHelper
    {
        [Flags]
        public enum InputType
        {
            Mouse = 0,
            Keyboard = 1,
            Hardware = 2
        }

        [Flags]
        public enum KeyEventF
        {
            KeyDown = 0x0000,
            ExtendedKey = 0x0001,
            KeyUp = 0x0002,
            Unicode = 0x0004,
            Scancode = 0x0008,
        }

        [Flags]
        public enum MouseEventF : uint
        {
            MOUSEEVENTF_MOVE = 0x0001,
            MOUSEEVENTF_LEFTDOWN = 0x0002,
            MOUSEEVENTF_LEFTUP = 0x0004,
            MOUSEEVENTF_RIGHTDOWN = 0x0008,
            MOUSEEVENTF_RIGHTUP = 0x0010,
            MOUSEEVENTF_MIDDLEDOWN = 0x0020,
            MOUSEEVENTF_MIDDLEUP = 0x0040,
            MOUSEEVENTF_XDOWN = 0x0080,
            MOUSEEVENTF_XUP = 0x0100,
            MOUSEEVENTF_WHEEL = 0x0800,
            MOUSEEVENTF_VIRTUALDESK = 0x4000,
            MOUSEEVENTF_ABSOLUTE = 0x8000,
        }

        public const uint WHEEL_DELTA = 120;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        /// <summary>
        /// Enumeration for virtual keys.
        /// </summary>
        public enum VirtualKeys : ushort
        {
            LEFTBUTTON = 0X01,
            RIGHTBUTTON = 0X02,
            CANCEL = 0X03,
            MIDDLEBUTTON = 0X04,
            EXTRABUTTON1 = 0X05,
            EXTRABUTTON2 = 0X06,
            BACK = 0X08,
            TAB = 0X09,
            CLEAR = 0X0C,
            RETURN = 0X0D,
            SHIFT = 0X10,
            CONTROL = 0X11,
            MENU = 0X12,
            PAUSE = 0X13,
            CAPSLOCK = 0X14,
            KANA = 0X15,
            HANGEUL = 0X15,
            HANGUL = 0X15,
            JUNJA = 0X17,
            FINAL = 0X18,
            HANJA = 0X19,
            KANJI = 0X19,
            ESCAPE = 0X1B,
            CONVERT = 0X1C,
            NONCONVERT = 0X1D,
            ACCEPT = 0X1E,
            MODECHANGE = 0X1F,
            SPACE = 0X20,
            PRIOR = 0X21,
            NEXT = 0X22,
            END = 0X23,
            HOME = 0X24,
            LEFT = 0X25,
            UP = 0X26,
            RIGHT = 0X27,
            DOWN = 0X28,
            SELECT = 0X29,
            PRINT = 0X2A,
            EXECUTE = 0X2B,
            SNAPSHOT = 0X2C,
            INSERT = 0X2D,
            DELETE = 0X2E,
            HELP = 0X2F,
            N0 = 0X30,
            N1 = 0X31,
            N2 = 0X32,
            N3 = 0X33,
            N4 = 0X34,
            N5 = 0X35,
            N6 = 0X36,
            N7 = 0X37,
            N8 = 0X38,
            N9 = 0X39,
            A = 0X41,
            B = 0X42,
            C = 0X43,
            D = 0X44,
            E = 0X45,
            F = 0X46,
            G = 0X47,
            H = 0X48,
            I = 0X49,
            J = 0X4A,
            K = 0X4B,
            L = 0X4C,
            M = 0X4D,
            N = 0X4E,
            O = 0X4F,
            P = 0X50,
            Q = 0X51,
            R = 0X52,
            S = 0X53,
            T = 0X54,
            U = 0X55,
            V = 0X56,
            W = 0X57,
            X = 0X58,
            Y = 0X59,
            Z = 0X5A,
            LEFTWINDOWS = 0X5B,
            RIGHTWINDOWS = 0X5C,
            APPLICATION = 0X5D,
            SLEEP = 0X5F,
            NUMPAD0 = 0X60,
            NUMPAD1 = 0X61,
            NUMPAD2 = 0X62,
            NUMPAD3 = 0X63,
            NUMPAD4 = 0X64,
            NUMPAD5 = 0X65,
            NUMPAD6 = 0X66,
            NUMPAD7 = 0X67,
            NUMPAD8 = 0X68,
            NUMPAD9 = 0X69,
            MULTIPLY = 0X6A,
            ADD = 0X6B,
            SEPARATOR = 0X6C,
            SUBTRACT = 0X6D,
            DECIMAL = 0X6E,
            DIVIDE = 0X6F,
            F1 = 0X70,
            F2 = 0X71,
            F3 = 0X72,
            F4 = 0X73,
            F5 = 0X74,
            F6 = 0X75,
            F7 = 0X76,
            F8 = 0X77,
            F9 = 0X78,
            F10 = 0X79,
            F11 = 0X7A,
            F12 = 0X7B,
            F13 = 0X7C,
            F14 = 0X7D,
            F15 = 0X7E,
            F16 = 0X7F,
            F17 = 0X80,
            F18 = 0X81,
            F19 = 0X82,
            F20 = 0X83,
            F21 = 0X84,
            F22 = 0X85,
            F23 = 0X86,
            F24 = 0X87,
            NUMLOCK = 0X90,
            SCROLLLOCK = 0X91,
            NEC_EQUAL = 0X92,
            FUJITSU_JISHO = 0X92,
            FUJITSU_MASSHOU = 0X93,
            FUJITSU_TOUROKU = 0X94,
            FUJITSU_LOYA = 0X95,
            FUJITSU_ROYA = 0X96,
            LEFTSHIFT = 0XA0,
            RIGHTSHIFT = 0XA1,
            LEFTCONTROL = 0XA2,
            RIGHTCONTROL = 0XA3,
            LEFTMENU = 0XA4,
            RIGHTMENU = 0XA5,
            BROWSERBACK = 0XA6,
            BROWSERFORWARD = 0XA7,
            BROWSERREFRESH = 0XA8,
            BROWSERSTOP = 0XA9,
            BROWSERSEARCH = 0XAA,
            BROWSERFAVORITES = 0XAB,
            BROWSERHOME = 0XAC,
            VOLUMEMUTE = 0XAD,
            VOLUMEDOWN = 0XAE,
            VOLUMEUP = 0XAF,
            MEDIANEXTTRACK = 0XB0,
            MEDIAPREVTRACK = 0XB1,
            MEDIASTOP = 0XB2,
            MEDIAPLAYPAUSE = 0XB3,
            LAUNCHMAIL = 0XB4,
            LAUNCHMEDIASELECT = 0XB5,
            LAUNCHAPPLICATION1 = 0XB6,
            LAUNCHAPPLICATION2 = 0XB7,
            OEM1 = 0XBA,
            OEMPLUS = 0XBB,
            OEMCOMMA = 0XBC,
            OEMMINUS = 0XBD,
            OEMPERIOD = 0XBE,
            OEM2 = 0XBF,
            OEM3 = 0XC0,
            OEM4 = 0XDB,
            OEM5 = 0XDC,
            OEM6 = 0XDD,
            OEM7 = 0XDE,
            OEM8 = 0XDF,
            OEMAX = 0XE1,
            OEM102 = 0XE2,
            ICOHELP = 0XE3,
            ICO00 = 0XE4,
            PROCESSKEY = 0XE5,
            ICOCLEAR = 0XE6,
            PACKET = 0XE7,
            OEMRESET = 0XE9,
            OEMJUMP = 0XEA,
            OEMPA1 = 0XEB,
            OEMPA2 = 0XEC,
            OEMPA3 = 0XED,
            OEMWSCTRL = 0XEE,
            OEMCUSEL = 0XEF,
            OEMATTN = 0XF0,
            OEMFINISH = 0XF1,
            OEMCOPY = 0XF2,
            OEMAUTO = 0XF3,
            OEMENLW = 0XF4,
            OEMBACKTAB = 0XF5,
            ATTN = 0XF6,
            CRSEL = 0XF7,
            EXSEL = 0XF8,
            EREOF = 0XF9,
            PLAY = 0XFA,
            ZOOM = 0XFB,
            NONAME = 0XFC,
            PA1 = 0XFD,
            OEMCLEAR = 0XFE
        }
 
        /// <summary>
        /// DirectX key list collected out from the gamespp.com list by me.
        /// </summary>
        public enum DirectXKeyStrokes
        {
            DIK_ESCAPE = 0x01,
            DIK_1 = 0x02,
            DIK_2 = 0x03,
            DIK_3 = 0x04,
            DIK_4 = 0x05,
            DIK_5 = 0x06,
            DIK_6 = 0x07,
            DIK_7 = 0x08,
            DIK_8 = 0x09,
            DIK_9 = 0x0A,
            DIK_0 = 0x0B,
            DIK_MINUS = 0x0C,
            DIK_EQUALS = 0x0D,
            DIK_BACK = 0x0E,
            DIK_TAB = 0x0F,
            DIK_Q = 0x10,
            DIK_W = 0x11,
            DIK_E = 0x12,
            DIK_R = 0x13,
            DIK_T = 0x14,
            DIK_Y = 0x15,
            DIK_U = 0x16,
            DIK_I = 0x17,
            DIK_O = 0x18,
            DIK_P = 0x19,
            DIK_LBRACKET = 0x1A,
            DIK_RBRACKET = 0x1B,
            DIK_RETURN = 0x1C,
            DIK_LCONTROL = 0x1D,
            DIK_A = 0x1E,
            DIK_S = 0x1F,
            DIK_D = 0x20,
            DIK_F = 0x21,
            DIK_G = 0x22,
            DIK_H = 0x23,
            DIK_J = 0x24,
            DIK_K = 0x25,
            DIK_L = 0x26,
            DIK_SEMICOLON = 0x27,
            DIK_APOSTROPHE = 0x28,
            DIK_GRAVE = 0x29,
            DIK_LSHIFT = 0x2A,
            DIK_BACKSLASH = 0x2B,
            DIK_Z = 0x2C,
            DIK_X = 0x2D,
            DIK_C = 0x2E,
            DIK_V = 0x2F,
            DIK_B = 0x30,
            DIK_N = 0x31,
            DIK_M = 0x32,
            DIK_COMMA = 0x33,
            DIK_PERIOD = 0x34,
            DIK_SLASH = 0x35,
            DIK_RSHIFT = 0x36,
            DIK_MULTIPLY = 0x37,
            DIK_LMENU = 0x38,
            DIK_SPACE = 0x39,
            DIK_CAPITAL = 0x3A,
            DIK_F1 = 0x3B,
            DIK_F2 = 0x3C,
            DIK_F3 = 0x3D,
            DIK_F4 = 0x3E,
            DIK_F5 = 0x3F,
            DIK_F6 = 0x40,
            DIK_F7 = 0x41,
            DIK_F8 = 0x42,
            DIK_F9 = 0x43,
            DIK_F10 = 0x44,
            DIK_NUMLOCK = 0x45,
            DIK_SCROLL = 0x46,
            DIK_NUMPAD7 = 0x47,
            DIK_NUMPAD8 = 0x48,
            DIK_NUMPAD9 = 0x49,
            DIK_SUBTRACT = 0x4A,
            DIK_NUMPAD4 = 0x4B,
            DIK_NUMPAD5 = 0x4C,
            DIK_NUMPAD6 = 0x4D,
            DIK_ADD = 0x4E,
            DIK_NUMPAD1 = 0x4F,
            DIK_NUMPAD2 = 0x50,
            DIK_NUMPAD3 = 0x51,
            DIK_NUMPAD0 = 0x52,
            DIK_DECIMAL = 0x53,
            DIK_F11 = 0x57,
            DIK_F12 = 0x58,
            DIK_F13 = 0x64,
            DIK_F14 = 0x65,
            DIK_F15 = 0x66,
            DIK_KANA = 0x70,
            DIK_CONVERT = 0x79,
            DIK_NOCONVERT = 0x7B,
            DIK_YEN = 0x7D,
            DIK_NUMPADEQUALS = 0x8D,
            DIK_CIRCUMFLEX = 0x90,
            DIK_AT = 0x91,
            DIK_COLON = 0x92,
            DIK_UNDERLINE = 0x93,
            DIK_KANJI = 0x94,
            DIK_STOP = 0x95,
            DIK_AX = 0x96,
            DIK_UNLABELED = 0x97,
            DIK_NUMPADENTER = 0x9C,
            DIK_RCONTROL = 0x9D,
            DIK_NUMPADCOMMA = 0xB3,
            DIK_DIVIDE = 0xB5,
            DIK_SYSRQ = 0xB7,
            DIK_RMENU = 0xB8,
            DIK_HOME = 0xC7,
            DIK_UP = 0xC8,
            DIK_PRIOR = 0xC9,
            DIK_LEFT = 0xCB,
            DIK_RIGHT = 0xCD,
            DIK_END = 0xCF,
            DIK_DOWN = 0xD0,
            DIK_NEXT = 0xD1,
            DIK_INSERT = 0xD2,
            DIK_DELETE = 0xD3,
            DIK_LWIN = 0xDB,
            DIK_RWIN = 0xDC,
            DIK_APPS = 0xDD,
            DIK_BACKSPACE = DIK_BACK,
            DIK_NUMPADSTAR = DIK_MULTIPLY,
            DIK_LALT = DIK_LMENU,
            DIK_CAPSLOCK = DIK_CAPITAL,
            DIK_NUMPADMINUS = DIK_SUBTRACT,
            DIK_NUMPADPLUS = DIK_ADD,
            DIK_NUMPADPERIOD = DIK_DECIMAL,
            DIK_NUMPADSLASH = DIK_DIVIDE,
            DIK_RALT = DIK_RMENU,
            DIK_UPARROW = DIK_UP,
            DIK_PGUP = DIK_PRIOR,
            DIK_LEFTARROW = DIK_LEFT,
            DIK_RIGHTARROW = DIK_RIGHT,
            DIK_DOWNARROW = DIK_DOWN,
            DIK_PGDN = DIK_NEXT,

            // Mined these out of nowhere.
            DIK_LEFTMOUSEBUTTON = 0x100,
            DIK_RIGHTMOUSEBUTTON = 0x101,
            DIK_MIDDLEWHEELBUTTON = 0x102,
            DIK_MOUSEBUTTON3 = 0x103,
            DIK_MOUSEBUTTON4 = 0x104,
            DIK_MOUSEBUTTON5 = 0x105,
            DIK_MOUSEBUTTON6 = 0x106,
            DIK_MOUSEBUTTON7 = 0x107,
            DIK_MOUSEWHEELUP = 0x108,
            DIK_MOUSEWHEELDOWN = 0x109,
        }

        public struct KeyTypeResult
        {
            public InputHelper.DirectXKeyStrokes Dxkey;
            public bool Dxkeyfound;
            public InputHelper.VirtualKeys Vkey;
            public bool Vkeyfound;
        }

        public static KeyTypeResult GetKeyType(string TypeParam)
        {
            bool dxkeyfound = false;
            bool vkeyfound = false;
            InputHelper.DirectXKeyStrokes dxkey = InputHelper.DirectXKeyStrokes.DIK_0;
            InputHelper.VirtualKeys vkey = InputHelper.VirtualKeys.A;
            try
            {
                dxkey = (InputHelper.DirectXKeyStrokes)Enum.Parse(typeof(InputHelper.DirectXKeyStrokes), TypeParam, true);
                dxkeyfound = true;
            }
            catch { }
            try
            {
                vkey = (InputHelper.VirtualKeys)Enum.Parse(typeof(InputHelper.VirtualKeys), TypeParam, true);
                vkeyfound = true;
            }
            catch { }
            return new KeyTypeResult { Dxkeyfound = dxkeyfound, Dxkey = dxkey, Vkey = vkey, Vkeyfound = vkeyfound };
        }

        /// <summary>
        /// Sends a virtual key.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="KeyUp"></param>
        /// <param name="inputType"></param>
        public static void SendKey(VirtualKeys key, bool KeyUp, InputType inputType)
        {
            uint flagtosend;
            if (KeyUp)
            {
                flagtosend = (uint)(KeyEventF.KeyUp );
            }
            else
            {
                flagtosend = (uint)(KeyEventF.KeyDown );
            }

            Input[] inputs =
            {
                new Input
                {
                    type = (int) inputType,
                    u = new InputUnion
                    {
                        ki = new KeyboardInput
                        {
                            wVk = (ushort) key,
                            wScan = 0,
                            dwFlags = flagtosend,
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                }
            };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input)));
        }

        /// <summary>
        /// Sends a directx key.
        /// http://www.gamespp.com/directx/directInputKeyboardScanCodes.html
        /// </summary>
        /// <param name="key"></param>
        /// <param name="KeyUp"></param>
        /// <param name="inputType"></param>
        public static void SendKey(DirectXKeyStrokes key, bool KeyUp, InputType inputType)
        {            
            uint flagtosend;
            if (KeyUp)
            {
                flagtosend = (uint)(KeyEventF.KeyUp | KeyEventF.Scancode);
            }
            else
            {
                flagtosend = (uint)(KeyEventF.Scancode);
            }

            Input[] inputs =
            {
                new Input
                {
                    type = (int) inputType,
                    u = new InputUnion
                    {
                        ki = new KeyboardInput
                        {
                            wVk = 0,
                            wScan = (ushort) key,
                            dwFlags = flagtosend,
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                }
            };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input)));
        }

        /// <summary>
        /// Sends a directx key or raw vk key value. Recommended NOT to use this variation
        /// http://www.gamespp.com/directx/directInputKeyboardScanCodes.html
        /// </summary>
        /// <param name="key"></param>
        /// <param name="KeyUp"></param>
        /// <param name="inputType"></param>
        public static void SendKey(ushort key, bool KeyUp, InputType inputType, bool directx)
        {
            uint flagtosend;
            if (KeyUp)
            {
                flagtosend = (uint)(KeyEventF.KeyUp | KeyEventF.Scancode);
            }
            else
            {
                flagtosend = (uint)(KeyEventF.KeyDown | KeyEventF.Scancode);
            }

            Input[] inputs =
            {
                new Input
                {
                    type = (int) inputType,
                    u = new InputUnion
                    {
                        ki = new KeyboardInput
                        {
                            wVk = !directx ? key:(ushort)0,
                            wScan = directx ? key:(ushort)0,
                            dwFlags = flagtosend,
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                }
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input)));
        }

        /// <summary>
        /// Sends a mouse event.
        /// </summary>
        /// <param name="mouseEventFlags"></param>
        /// <param name="mData"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public static void SendMouse(uint mouseEventFlags, uint mData=0, int x = 0, int y = 0)
        {           
            Input[] inputs =
            {
                new Input
                {
                    type = (int) InputType.Mouse,
                    u = new InputUnion
                    {
                        mi = new MouseInput
                        {
                            dx = x,
                            dy = y,
                            mouseData = mData,
                            dwFlags = mouseEventFlags,
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                }
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input)));
        }

        public struct Input
        {
            public int type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MouseInput mi;
            [FieldOffset(0)] public KeyboardInput ki;
            [FieldOffset(0)] public readonly HardwareInput hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MouseInput
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public readonly uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KeyboardInput
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public readonly uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HardwareInput
        {
            public readonly uint uMsg;
            public readonly ushort wParamL;
            public readonly ushort wParamH;
        }
    }
}
