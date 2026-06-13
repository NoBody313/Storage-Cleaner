namespace StorageScanner.Utils;

public static class SizeFormatter
{
    public static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    public static string FormatBytesShort(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024) :0.#} MB",
            < 1024L * 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):0.#} GB",
            _ => $"{bytes / (1024.0 * 1024 * 1024 * 1024):0.##} TB"
        };
    }
}
