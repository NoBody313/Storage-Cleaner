namespace StorageScanner.Models;

public class FileNode
{
    public string FullPath { get; set; } = "";
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public long AllocatedSize { get; set; }
    public bool IsDirectory { get; set; }
    public bool IsCompressed { get; set; }
    public FileNode? Parent { get; set; }
    public List<FileNode> Children { get; } = [];
    public string Category { get; set; } = "Other";

    private long _cachedTotalSize = -1;
    private long _cachedAllocated = -1;

    public long TotalSize
    {
        get
        {
            if (!IsDirectory) return Size;
            if (_cachedTotalSize >= 0) return _cachedTotalSize;
            _cachedTotalSize = Children.Sum(c => c.TotalSize);
            return _cachedTotalSize;
        }
    }

    public long TotalAllocated
    {
        get
        {
            if (!IsDirectory) return AllocatedSize;
            if (_cachedAllocated >= 0) return _cachedAllocated;
            _cachedAllocated = Children.Sum(c => c.TotalAllocated);
            return _cachedAllocated;
        }
    }

    public int FileCount => IsDirectory ? Children.Sum(c => c.FileCount) : 1;
    public int FolderCount => IsDirectory ? Children.Count(c => c.IsDirectory) + Children.Sum(c => c.FolderCount) : 0;
    public int ItemCount => FileCount + FolderCount;

    public double PercentOfParent => Parent?.TotalSize > 0 ? (TotalSize * 100.0) / Parent.TotalSize : 100.0;

    public void InvalidateCache()
    {
        _cachedTotalSize = -1;
        _cachedAllocated = -1;
        Parent?.InvalidateCache();
    }

    public FileNode() { }

    public FileNode(string path, string name, long size, long allocated, bool isDir, bool isCompressed = false, FileNode? parent = null)
    {
        FullPath = path;
        Name = name;
        Size = size;
        AllocatedSize = allocated;
        IsDirectory = isDir;
        IsCompressed = isCompressed;
        Parent = parent;
        Category = DetectCategory(path);
    }

    private static string DetectCategory(string path)
    {
        var p = path.ToLowerInvariant();

        if (p.Contains(@"\windows\") || p.StartsWith(@"c:\windows")) return "System";
        if (p.Contains(@"\program files\") || p.Contains(@"\program files (x86)\")) return "Applications";
        if (p.Contains(@"\docker\") || p.Contains(@"\.docker\") || p.Contains(@"\dockerdesktop")) return "Docker";
        if (p.Contains(@"\node_modules\")) return "Node Modules";
        if (p.Contains(@"\.git\") || p.Contains(@"\git\")) return "Git";
        if (p.Contains(@"\downloads\")) return "Downloads";
        if (p.Contains(@"\documents\")) return "Documents";
        if (p.Contains(@"\desktop\")) return "Desktop";
        if (p.Contains(@"\appdata\local\temp")) return "Temp";
        if (p.Contains(@"\appdata\")) return "AppData";
        if (p.Contains(@"\pictures\") || p.Contains(@"\photos\")) return "Pictures";
        if (p.Contains(@"\videos\") || p.Contains(@"\movies\")) return "Videos";
        if (p.Contains(@"\music\")) return "Music";
        if (p.Contains(@"\recycle.bin")) return "Recycle Bin";
        if (p.Contains(@"\winsxs\") || p.Contains(@"\servicing\")) return "System";

        return "Other";
    }
}
