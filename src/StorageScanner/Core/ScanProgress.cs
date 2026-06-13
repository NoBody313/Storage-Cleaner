namespace StorageScanner.Core;

public class ScanProgress
{
    public long FilesScanned { get; set; }
    public long BytesScanned { get; set; }
    public string CurrentPath { get; set; } = "";
    public long SkippedFolders { get; set; }
    public DateTime StartTime { get; set; }

    public TimeSpan Elapsed => DateTime.Now - StartTime;
}
