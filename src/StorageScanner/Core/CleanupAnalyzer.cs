using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StorageScanner.Utils;

namespace StorageScanner.Core;

public enum CleanupSafety { Safe, Review, Caution }

public class CleanupItem
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public CleanupSafety Safety { get; set; }
    public long Size { get; set; }
    public int FileCount { get; set; }
    public bool IsDirectory { get; set; }
    public List<(string Path, long Size)> TopFiles { get; set; } = [];

    public string SafetyLabel => Safety switch
    {
        CleanupSafety.Safe    => "Safe",
        CleanupSafety.Review  => "Review",
        CleanupSafety.Caution => "Caution",
        _ => ""
    };

    public string SizeLabel => SizeFormatter.FormatBytes(Size);
    public string FileCountLabel => FileCount == 0 ? "" : $"{FileCount:N0} files";
}

public static class CleanupAnalyzer
{
    private static readonly List<(string path, string category, string desc, CleanupSafety safety)> KnownTargets;

    static CleanupAnalyzer()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        KnownTargets =
        [
            (System.IO.Path.Combine(local,   "Temp"),                              "Temp",          "Windows user temp files",                         CleanupSafety.Safe),
            (@"C:\Windows\Temp",                                                    "Temp",          "Windows system temp files",                       CleanupSafety.Safe),
            (@"C:\Windows\Prefetch",                                                "Windows Cache", "Prefetch cache (auto-rebuilt)",                   CleanupSafety.Safe),
            (System.IO.Path.Combine(local,   "Microsoft\\Windows\\WER"),           "Windows Cache", "Windows Error Reporting crash dumps",             CleanupSafety.Safe),
            (@"C:\Windows\SoftwareDistribution\\Download",                         "Windows Update","Downloaded update packages (re-downloaded auto)", CleanupSafety.Safe),
            (@"C:\Windows.old",                                                     "Old Windows",   "Previous Windows install (safe after 10 days)",  CleanupSafety.Safe),
            (@"C:\$Windows.~BT",                                                    "Old Windows",   "Windows upgrade temp",                            CleanupSafety.Safe),
            (@"C:\$Windows.~WS",                                                    "Old Windows",   "Windows upgrade workspace",                       CleanupSafety.Safe),
            (System.IO.Path.Combine(local,   "pip\\Cache"),                        "Dev Cache",     "Python pip download cache",                       CleanupSafety.Safe),
            (System.IO.Path.Combine(roaming, "npm-cache"),                         "Dev Cache",     "npm package cache",                               CleanupSafety.Safe),
            (System.IO.Path.Combine(local,   "Yarn\\Cache"),                       "Dev Cache",     "Yarn package cache",                              CleanupSafety.Safe),
            (System.IO.Path.Combine(user,    ".gradle\\caches"),                   "Dev Cache",     "Gradle build cache",                              CleanupSafety.Safe),
            (System.IO.Path.Combine(local,   "NuGet\\Cache"),                      "Dev Cache",     "NuGet HTTP cache",                                CleanupSafety.Safe),
            (System.IO.Path.Combine(user,    ".nuget\\packages"),                  "Dev Cache",     "NuGet local packages (restore re-downloads)",     CleanupSafety.Review),
            (System.IO.Path.Combine(user,    ".m2\\repository"),                   "Dev Cache",     "Maven local repo (restore re-downloads)",         CleanupSafety.Review),
            (System.IO.Path.Combine(local,   "Google\\Chrome\\User Data\\Default\\Cache"), "Browser", "Chrome cache",                                CleanupSafety.Safe),
            (System.IO.Path.Combine(local,   "Microsoft\\Edge\\User Data\\Default\\Cache"), "Browser", "Edge cache",                                 CleanupSafety.Safe),
            (System.IO.Path.Combine(local,   "Docker\\wsl"),                       "Docker",        "Docker WSL2 disk (images/containers/volumes)",    CleanupSafety.Caution),
            (System.IO.Path.Combine(local,   "Docker\\log"),                       "Docker",        "Docker log files",                                CleanupSafety.Safe),
        ];
    }

    public static async Task<List<CleanupItem>> ScanAsync(IProgress<string>? progress = null, CancellationToken ct = default, string? selectedDrive = null)
    {
        var results = new List<CleanupItem>();

        // Determine which drive root to scope to (null = all drives / C: system scan)
        var scopeRoot = string.IsNullOrEmpty(selectedDrive)
            ? null
            : System.IO.Path.GetPathRoot(selectedDrive);   // e.g. "D:\"

        // KnownTargets: only include if path is on the selected drive
        foreach (var (path, category, desc, safety) in KnownTargets)
        {
            if (ct.IsCancellationRequested) break;

            // Filter by drive root when a specific drive is selected
            if (scopeRoot != null &&
                !path.StartsWith(scopeRoot, StringComparison.OrdinalIgnoreCase))
                continue;

            progress?.Report($"Checking: {System.IO.Path.GetFileName(path)}");

            if (!Directory.Exists(path) && !File.Exists(path)) continue;

            await Task.Run(() =>
            {
                try
                {
                    var (size, count, topFiles) = AnalyzeDir(path);
                    if (size < 1024 * 1024) return;

                    results.Add(new CleanupItem
                    {
                        Path = path,
                        Name = System.IO.Path.GetFileName(path),
                        Category = category,
                        Description = desc,
                        Safety = safety,
                        Size = size,
                        FileCount = count,
                        IsDirectory = Directory.Exists(path),
                        TopFiles = topFiles
                    });
                }
                catch { }
            }, ct);
        }

        // Dev waste scan — scoped to selected drive (or all drives if none selected)
        var user2 = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var devSubFolders = new[] { "dev", "projects", "source", "repos", "code", "workspace" };

        IEnumerable<string> driveRootsToScan;
        if (scopeRoot != null)
        {
            driveRootsToScan = new[] { scopeRoot };
            // Also include user profile subdirs only if they're on the same drive
            var userRoot = System.IO.Path.GetPathRoot(user2);
            if (string.Equals(userRoot, scopeRoot, StringComparison.OrdinalIgnoreCase))
                driveRootsToScan = driveRootsToScan.Append(user2).ToList();
        }
        else
        {
            driveRootsToScan = DriveInfo.GetDrives()
                .Where(d => d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable))
                .Select(d => d.RootDirectory.FullName);
        }

        var devRoots = driveRootsToScan
            .SelectMany(r => r == user2
                ? devSubFolders.Select(d => System.IO.Path.Combine(r, d))
                : devSubFolders.Select(d => System.IO.Path.Combine(r, d)))
            .Append(selectedDrive ?? "")          // selected path itself (e.g. D:\Projects)
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists);

        // Also include user-profile dev dirs if on same drive
        if (scopeRoot != null &&
            string.Equals(System.IO.Path.GetPathRoot(user2), scopeRoot, StringComparison.OrdinalIgnoreCase))
        {
            devRoots = devRoots.Concat(
                devSubFolders.Select(d => System.IO.Path.Combine(user2, d)).Where(Directory.Exists));
        }

        foreach (var devRoot in devRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (ct.IsCancellationRequested) break;
            progress?.Report($"Scanning dev: {devRoot}");
            await ScanDevWaste(devRoot, results, ct);
        }

        // Recycle Bin — scoped to selected drive
        await Task.Run(() =>
        {
            try
            {
                long rbSize = 0;
                int rbCount = 0;
                var topFiles = new List<(string, long)>();
                var drivesToCheck = scopeRoot != null
                    ? DriveInfo.GetDrives().Where(d => d.IsReady &&
                        string.Equals(d.RootDirectory.FullName, scopeRoot, StringComparison.OrdinalIgnoreCase))
                    : DriveInfo.GetDrives().Where(d => d.IsReady);
                foreach (var drive in drivesToCheck)
                {
                    var rb = System.IO.Path.Combine(drive.Name, "$Recycle.Bin");
                    if (!Directory.Exists(rb)) continue;
                    var (s, c, tf) = AnalyzeDir(rb);
                    rbSize += s; rbCount += c;
                    topFiles.AddRange(tf);
                }
                if (rbSize > 0)
                    results.Add(new CleanupItem
                    {
                        Path = "$Recycle.Bin",
                        Name = "Recycle Bin",
                        Category = "Recycle Bin",
                        Description = "Items in Recycle Bin (selected drive)",
                        Safety = CleanupSafety.Review,
                        Size = rbSize,
                        FileCount = rbCount,
                        IsDirectory = true,
                        TopFiles = topFiles.OrderByDescending(f => f.Item2).ToList()
                    });
            }
            catch { }
        }, ct);

        return results.OrderByDescending(r => r.Size).ToList();
    }

    private static async Task ScanDevWaste(string root, List<CleanupItem> results, CancellationToken ct)
    {
        var targets = new Dictionary<string, (string cat, string desc, CleanupSafety safety)>(StringComparer.OrdinalIgnoreCase)
        {
            ["node_modules"]  = ("Dev Cache",    "Node.js dependencies — npm install to restore",   CleanupSafety.Safe),
            ["__pycache__"]   = ("Dev Cache",    "Python bytecode cache",                           CleanupSafety.Safe),
            [".pytest_cache"] = ("Dev Cache",    "pytest cache directory",                          CleanupSafety.Safe),
            [".tox"]          = ("Dev Cache",    "tox virtual environments",                        CleanupSafety.Safe),
            [".next"]         = ("Dev Cache",    "Next.js build cache",                             CleanupSafety.Safe),
            [".nuxt"]         = ("Dev Cache",    "Nuxt.js build output",                            CleanupSafety.Safe),
            [".parcel-cache"] = ("Dev Cache",    "Parcel bundler cache",                            CleanupSafety.Safe),
            ["dist"]          = ("Build Output", "Build output — review before deleting",           CleanupSafety.Review),
            ["build"]         = ("Build Output", "Build output — review before deleting",           CleanupSafety.Review),
            [".gradle"]       = ("Dev Cache",    "Gradle cache inside project",                     CleanupSafety.Safe),
        };

        try
        {
            var opts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 6 };
            foreach (var dir in Directory.EnumerateDirectories(root, "*", opts))
            {
                if (ct.IsCancellationRequested) break;
                var dirName = System.IO.Path.GetFileName(dir);
                if (!targets.TryGetValue(dirName, out var meta)) continue;

                await Task.Run(() =>
                {
                    try
                    {
                        var (size, count, topFiles) = AnalyzeDir(dir);
                        if (size < 5 * 1024 * 1024) return;

                        lock (results)
                        {
                            results.Add(new CleanupItem
                            {
                                Path = dir,
                                Name = dirName,
                                Category = meta.cat,
                                Description = $"{meta.desc}",
                                Safety = meta.safety,
                                Size = size,
                                FileCount = count,
                                IsDirectory = true,
                                TopFiles = topFiles
                            });
                        }
                    }
                    catch { }
                }, ct);
            }
        }
        catch { }
    }

    private static (long size, int count, List<(string path, long size)> topFiles) AnalyzeDir(string path)
    {
        long total = 0;
        int count = 0;
        var allFiles = new List<(string, long)>();

        if (File.Exists(path))
        {
            var fi = new FileInfo(path);
            return (fi.Length, 1, [(path, fi.Length)]);
        }

        foreach (var file in Directory.EnumerateFiles(path, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true
        }))
        {
            try
            {
                var size = new FileInfo(file).Length;
                total += size;
                count++;
                allFiles.Add((file, size));
            }
            catch { }
        }

        var top = allFiles.OrderByDescending(f => f.Item2).ToList();
        return (total, count, top);
    }

    public static async Task DeleteItemAsync(CleanupItem item, IProgress<string>? progress = null)
    {
        await Task.Run(() =>
        {
            try
            {
                progress?.Report($"Deleting: {item.Name}");
                if (item.IsDirectory && Directory.Exists(item.Path))
                    Directory.Delete(item.Path, true);
                else if (File.Exists(item.Path))
                    File.Delete(item.Path);
            }
            catch (Exception ex)
            {
                progress?.Report($"Failed: {item.Name} — {ex.Message}");
            }
        });
    }
}
