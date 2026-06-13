using System;
using System.IO;
using System.Runtime.InteropServices;

namespace StorageScanner.Services;

public static class FileActions
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    private const uint FO_DELETE = 3;
    private const ushort FOF_ALLOWUNDO = 0x40;
    private const ushort FOF_NOCONFIRMATION = 0x10;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

    public static void OpenInExplorer(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    public static bool DeleteToRecycleBin(string path)
    {
        try
        {
            var shf = new SHFILEOPSTRUCT
            {
                hwnd = IntPtr.Zero,
                wFunc = FO_DELETE,
                pFrom = path + "\0\0",
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION
            };

            return SHFileOperation(ref shf) == 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool DeleteDirectoryToRecycleBin(string path)
    {
        return DeleteToRecycleBin(path);
    }
}
