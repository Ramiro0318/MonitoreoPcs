using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System;
using System.Runtime.InteropServices;

namespace Cliente.Helpers
{

    public static class WindowsSystemHelper
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct TokenPrivileges
        {
            public int Count;
            public long Luid;
            public int Attr;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool OpenProcessToken(IntPtr h, int acc, ref IntPtr tkp);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        internal static extern bool LookupPrivilegeValue(string host, string name, ref long luid);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern bool AdjustTokenPrivileges(IntPtr tkp, bool dis, ref TokenPrivileges newst, int len, IntPtr prev, IntPtr relen);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        internal static extern IntPtr GetCurrentProcess();

        public static void EnableShutdownPrivilege()
        {
            IntPtr hToken = IntPtr.Zero;
            if (OpenProcessToken(GetCurrentProcess(), 0x20 | 0x8, ref hToken))
            {
                TokenPrivileges tp;
                tp.Count = 1;
                tp.Luid = 0;
                tp.Attr = 0x2; // SE_PRIVILEGE_ENABLED
                LookupPrivilegeValue(null, "SeShutdownPrivilege", ref tp.Luid);
                AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }
}
