using System;
using System.IO;
using StorageScanner.Models;

namespace StorageScanner.Core;

public class FileTypeStats
{
    public string Type { get; set; } = "";
    public long TotalSize { get; set; }
    public int FileCount { get; set; }
    public double Percentage { get; set; }
}

public static class FileTypeAnalyzer
{
    private static readonly Dictionary<string, string> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".mp4", "Video" }, { ".mkv", "Video" }, { ".avi", "Video" }, { ".mov", "Video" }, { ".flv", "Video" },
        { ".mp3", "Audio" }, { ".wav", "Audio" }, { ".flac", "Audio" }, { ".aac", "Audio" },
        { ".jpg", "Image" }, { ".jpeg", "Image" }, { ".png", "Image" }, { ".gif", "Image" }, { ".bmp", "Image" },
        { ".zip", "Archive" }, { ".rar", "Archive" }, { ".7z", "Archive" }, { ".tar", "Archive" }, { ".gz", "Archive" },
        { ".exe", "Executable" }, { ".msi", "Executable" }, { ".bat", "Executable" }, { ".cmd", "Executable" },
        { ".doc", "Document" }, { ".docx", "Document" }, { ".pdf", "Document" }, { ".xls", "Document" }, { ".xlsx", "Document" },
        { ".dll", "System" }, { ".sys", "System" }, { ".tmp", "System" }, { ".ini", "System" }
    };

    public static string GetCategory(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return ExtensionMap.TryGetValue(ext, out var category) ? category : "Other";
    }

    public static List<FileTypeStats> AnalyzeTree(FileNode root)
    {
        var stats = new Dictionary<string, FileTypeStats>(StringComparer.Ordinal);
        long totalBytes = root.TotalSize;

        CollectStats(root, stats);

        var list = stats.Values.OrderByDescending(s => s.TotalSize).ToList();
        foreach (var stat in list)
            stat.Percentage = totalBytes > 0 ? (stat.TotalSize * 100.0) / totalBytes : 0;

        return list;
    }

    private static void CollectStats(FileNode node, Dictionary<string, FileTypeStats> stats)
    {
        if (!node.IsDirectory)
        {
            var category = GetCategory(node.Name);
            if (!stats.ContainsKey(category))
                stats[category] = new FileTypeStats { Type = category };

            stats[category].TotalSize += node.Size;
            stats[category].FileCount++;
        }

        foreach (var child in node.Children)
            CollectStats(child, stats);
    }
}
