using System.IO;
using System.Runtime.InteropServices;

namespace StorageScanner.Core;

public static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindFirstFileW(string lpFileName, out WIN32_FIND_DATAW lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool FindNextFileW(IntPtr hFindFile, out WIN32_FIND_DATAW lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FindClose(IntPtr hFindFile);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool GetDiskFreeSpaceW(
        string lpRootPathName,
        out uint lpSectorsPerCluster,
        out uint lpBytesPerSector,
        out uint lpNumberOfFreeClusters,
        out uint lpTotalNumberOfClusters);

    public const int INVALID_HANDLE_VALUE = -1;
    public const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    public const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x400;
    public const uint FILE_ATTRIBUTE_COMPRESSED = 0x800;
    public const uint FILE_ATTRIBUTE_SPARSE_FILE = 0x200;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public FILETIME ftCreationTime;
        public FILETIME ftLastAccessTime;
        public FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;

        public long FileSize => ((long)nFileSizeHigh << 32) | nFileSizeLow;
        public bool IsDirectory => (dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
        public bool IsReparsePoint => (dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0;
        public bool IsCompressed => (dwFileAttributes & FILE_ATTRIBUTE_COMPRESSED) != 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    public static long GetClusterSize(string rootPath)
    {
        var root = Path.GetPathRoot(rootPath) ?? rootPath;
        if (GetDiskFreeSpaceW(root, out uint sectors, out uint bytes, out _, out _))
            return (long)sectors * bytes;
        return 4096;
    }

    public static long CalcAllocated(long fileSize, long clusterSize)
    {
        if (fileSize == 0) return 0;
        return ((fileSize + clusterSize - 1) / clusterSize) * clusterSize;
    }
}
