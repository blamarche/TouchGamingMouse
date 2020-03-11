using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TouchGamingMouse
{
    class Utils
    {
        [DllImport("shell32.dll", EntryPoint = "FindExecutable")]
        static extern int FindExecutable(string lpFile, string lpDirectory, StringBuilder lpResult);
        const int SE_ERR_NOASSOC = 31;

        public static bool IsAutohotkeyAssociated(string ahkfile)
        {
            StringBuilder output = new StringBuilder(1024);
            var r = FindExecutable(ahkfile, null, output);
            return !(r == SE_ERR_NOASSOC);
        }
    }
}
