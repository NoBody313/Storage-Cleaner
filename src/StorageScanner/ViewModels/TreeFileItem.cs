using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using StorageScanner.Models;
using StorageScanner.Utils;

namespace StorageScanner.ViewModels;

public class TreeFileItem : INotifyPropertyChanged
{
    private bool _isExpanded;

    public FileNode Node { get; }
    public int Level { get; }
    public Thickness Indent => new Thickness(Level * 18, 0, 0, 0);
    public bool HasChildren => Node.IsDirectory && Node.Children.Count > 0;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExpandIcon));
            OnPropertyChanged(nameof(FileIcon));
        }
    }

    // Segoe MDL2 Assets: ChevronRight E76C, ChevronDown E70D
    public string ExpandIcon => _isExpanded ? "" : "";

    // Segoe MDL2 Assets: FolderOpen E838 / Folder E8B7 / Document E8A5
    public string FileIcon => Node.IsDirectory
        ? (_isExpanded ? "" : "")
        : GetFileIcon(Node.Name);

    public string Name => Node.Name;
    public string FullPath => Node.FullPath;
    public string SizeFormatted => SizeFormatter.FormatBytes(Node.TotalSize);
    public string AllocatedFormatted => SizeFormatter.FormatBytes(Node.TotalAllocated);
    public string Percent => $"{Node.PercentOfParent:F1}%";
    public int ItemCount => Node.ItemCount;
    public int FileCount => Node.FileCount;
    public int FolderCount => Node.FolderCount;
    public string Category => Node.Category;
    public bool IsCompressed => Node.IsCompressed;

    public TreeFileItem(FileNode node, int level)
    {
        Node = node;
        Level = level;
    }

    private static string GetFileIcon(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            // MDL2: Video E8B2, Music EC4F, Photo EB9F, ZipFolder F012,
            //       Application E737, PDF EA90, WordDoc EF3E, ExcelDoc F96C,
            //       Disk E96E, Code E943, Document E8A5
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" => "",
            ".mp3" or ".flac" or ".wav" or ".aac" or ".ogg" => "",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "",
            ".exe" or ".msi" => "",
            ".pdf" => "",
            ".doc" or ".docx" => "",
            ".xls" or ".xlsx" => "塞",
            ".iso" or ".img" => "",
            ".cs" or ".js" or ".ts" or ".py" or ".java" or ".cpp" or ".h" => "",
            _ => ""
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
