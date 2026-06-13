using System.IO;
using System.Linq;
using StorageScanner.Models;

namespace StorageScanner.Core;

public class CompactCandidate
{
    public string FullPath { get; set; } = "";
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public string Extension { get; set; } = "";
    public string Category { get; set; } = "";
    public double EstimatedRatio { get; set; }
    public long EstimatedSavings { get; set; }
    public bool IsAlreadyCompressed { get; set; }
}

public static class CompactAnalyzer
{
    private static readonly Dictionary<string, double> CompressRatios = new(StringComparer.OrdinalIgnoreCase)
    {
        // High compression gain
        { ".log",  0.80 }, { ".txt",  0.75 }, { ".xml",  0.80 }, { ".json", 0.75 },
        { ".csv",  0.80 }, { ".html", 0.70 }, { ".htm",  0.70 }, { ".sql",  0.78 },
        { ".md",   0.70 }, { ".yaml", 0.72 }, { ".yml",  0.72 }, { ".ini",  0.65 },
        { ".cfg",  0.65 }, { ".conf", 0.65 }, { ".reg",  0.70 },

        // Medium compression gain
        { ".pdb",  0.60 }, { ".iso",  0.40 }, { ".vhd",  0.50 }, { ".vhdx", 0.50 },
        { ".vmdk", 0.45 }, { ".bak",  0.55 }, { ".db",   0.50 }, { ".sqlite", 0.50 },
        { ".mdf",  0.55 }, { ".ldf",  0.65 }, { ".exe",  0.35 }, { ".dll",  0.35 },

        // Low/no gain (already compressed or encrypted)
        { ".mp4",  0.02 }, { ".mkv",  0.02 }, { ".avi",  0.03 }, { ".mov",  0.02 },
        { ".mp3",  0.01 }, { ".flac", 0.01 }, { ".aac",  0.01 },
        { ".jpg",  0.02 }, { ".jpeg", 0.02 }, { ".png",  0.02 }, { ".gif",  0.01 },
        { ".zip",  0.01 }, { ".rar",  0.01 }, { ".7z",   0.01 }, { ".gz",   0.01 },
        { ".br",   0.01 }, { ".zst",  0.01 },
    };

    private const long MinSizeBytes = 1 * 1024 * 1024; // 1 MB min

    public static List<CompactCandidate> Analyze(FileNode root, long minSizeBytes = MinSizeBytes)
    {
        var candidates = new List<CompactCandidate>();
        CollectCandidates(root, candidates, minSizeBytes);
        return candidates.OrderByDescending(c => c.EstimatedSavings).ToList();
    }

    private static void CollectCandidates(FileNode node, List<CompactCandidate> list, long minSize)
    {
        if (!node.IsDirectory)
        {
            if (node.IsCompressed) return;
            if (node.Size < minSize) return;

            var ext = Path.GetExtension(node.Name);
            var ratio = CompressRatios.TryGetValue(ext, out var r) ? r : 0.10;
            if (ratio < 0.05) return;

            var savings = (long)(node.Size * ratio);
            if (savings < 512 * 1024) return; // skip if < 512KB savings

            list.Add(new CompactCandidate
            {
                FullPath = node.FullPath,
                Name = node.Name,
                Size = node.Size,
                Extension = string.IsNullOrEmpty(ext) ? "(none)" : ext,
                Category = node.Category,
                EstimatedRatio = ratio,
                EstimatedSavings = savings,
                IsAlreadyCompressed = node.IsCompressed
            });
        }
        else
        {
            foreach (var child in node.Children)
                CollectCandidates(child, list, minSize);
        }
    }

    public static async Task RunCompactAsync(IEnumerable<string> paths, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        foreach (var path in paths)
        {
            if (ct.IsCancellationRequested) break;
            progress?.Report($"Compacting: {Path.GetFileName(path)}");

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "compact.exe",
                    Arguments = $"/c \"{path}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };

                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null)
                    await proc.WaitForExitAsync(ct);
            }
            catch { }
        }
    }
}
