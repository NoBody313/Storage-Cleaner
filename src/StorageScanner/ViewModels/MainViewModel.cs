using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StorageScanner.Core;
using StorageScanner.Models;
using StorageScanner.Utils;

namespace StorageScanner.ViewModels;

public class CategoryStat
{
    public string Category { get; set; } = "";
    public long TotalSize { get; set; }
    public int FileCount { get; set; }
    public double Percentage { get; set; }
    public string SizeFormatted => SizeFormatter.FormatBytes(TotalSize);
}

public class CleanupFileEntry
{
    public string Path { get; set; } = "";
    public string Name => System.IO.Path.GetFileName(Path);
    public long Size { get; set; }
    public string SizeFormatted => SizeFormatter.FormatBytes(Size);
}

public enum NotifState { None, Running, Success, Error, Warning }

public class MainViewModel : INotifyPropertyChanged
{
    private FileNode? _scanResult;
    private string _selectedDrive = "";
    private bool _isScanning;
    private bool _isCleanupScanning;
    private bool _isDeleting;
    private string _statusText = "Ready — select a drive and click Scan";
    private long _filesScanned;
    private long _bytesScanned;
    private string _elapsedTime = "00:00:00";
    private double _scanProgressValue;
    private string _cleanupStatus = "Click 'Scan Cleanup' to find cleanup candidates";
    private CleanupItem? _selectedCleanupItem;
    private CancellationTokenSource? _cts;
    private NotifState _notifState = NotifState.None;
    private string _notifMessage = "";
    private CancellationTokenSource? _notifDismissCts;

    public ObservableCollection<string> AvailableDrives { get; } = [];
    public ObservableCollection<TreeFileItem> TreeItems { get; } = [];
    public ObservableCollection<FileTypeStats> FileTypeStats { get; } = [];
    public ObservableCollection<CleanupItem> CleanupItems { get; } = [];
    public ObservableCollection<CleanupFileEntry> CleanupFileDetails { get; } = [];

    private List<FileNode> _topLargestItems = [];
    private List<CategoryStat> _categoryStats = [];

    public List<FileNode> TopLargestItems
    {
        get => _topLargestItems;
        set { _topLargestItems = value; OnPropertyChanged(); }
    }
    public List<CategoryStat> CategoryStats
    {
        get => _categoryStats;
        set { _categoryStats = value; OnPropertyChanged(); }
    }
    public FileNode? ScanResult
    {
        get => _scanResult;
        set { _scanResult = value; OnPropertyChanged(); }
    }
    public string SelectedDrive
    {
        get => _selectedDrive;
        set { _selectedDrive = value; OnPropertyChanged(); }
    }
    public bool IsScanning
    {
        get => _isScanning;
        set { _isScanning = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }
    public bool IsCleanupScanning
    {
        get => _isCleanupScanning;
        set { _isCleanupScanning = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }
    public bool IsDeleting
    {
        get => _isDeleting;
        set { _isDeleting = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }
    public long FilesScanned
    {
        get => _filesScanned;
        set { _filesScanned = value; OnPropertyChanged(); }
    }
    public long BytesScanned
    {
        get => _bytesScanned;
        set { _bytesScanned = value; OnPropertyChanged(); }
    }
    public string ElapsedTime
    {
        get => _elapsedTime;
        set { _elapsedTime = value; OnPropertyChanged(); }
    }
    public double ScanProgressValue
    {
        get => _scanProgressValue;
        set { _scanProgressValue = value; OnPropertyChanged(); }
    }
    public string CleanupStatus
    {
        get => _cleanupStatus;
        set { _cleanupStatus = value; OnPropertyChanged(); }
    }
    public CleanupItem? SelectedCleanupItem
    {
        get => _selectedCleanupItem;
        set
        {
            _selectedCleanupItem = value;
            OnPropertyChanged();
            RefreshCleanupDetails();
        }
    }

    public ICommand ScanCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseFolderCommand { get; }
    public ICommand DeleteFileCommand { get; }
    public ICommand OpenInExplorerCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand ToggleExpandCommand { get; }
    public ICommand ScanCleanupCommand { get; }
    public ICommand DeleteCleanupItemCommand { get; }

    public MainViewModel()
    {
        LoadDrives();
        ScanCommand = new RelayCommand(_ => ScanAsync(), _ => !IsScanning);
        CancelCommand = new RelayCommand(_ => Cancel(), _ => IsScanning || IsCleanupScanning);
        BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());
        DeleteFileCommand = new RelayCommand<FileNode>(DeleteFile, _ => !IsScanning);
        OpenInExplorerCommand = new RelayCommand<FileNode>(n => { if (n != null) Services.FileActions.OpenInExplorer(n.FullPath); });
        ExportCsvCommand = new RelayCommand(_ => ExportCsv(), _ => ScanResult != null);
        ExportJsonCommand = new RelayCommand(_ => ExportJson(), _ => ScanResult != null);
        ToggleExpandCommand = new RelayCommand<TreeFileItem>(ToggleExpand);
        ScanCleanupCommand = new RelayCommand(_ => ScanCleanupAsync(), _ => !IsCleanupScanning && !IsScanning);
        DeleteCleanupItemCommand = new RelayCommand<CleanupItem>(DeleteCleanupItem, _ => !IsDeleting);
    }

    private void LoadDrives()
    {
        AvailableDrives.Clear();
        foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
            AvailableDrives.Add(d.Name);
        if (AvailableDrives.Count > 0)
            SelectedDrive = AvailableDrives[0];
    }

    private void BrowseFolder()
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog();
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            SelectedDrive = dlg.SelectedPath;
    }

    private async void ScanAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedDrive)) { StatusText = "Select a drive or folder first"; return; }

        IsScanning = true;
        ScanProgressValue = 0;
        StatusText = "Scanning…";
        TreeItems.Clear();
        _cts = new CancellationTokenSource();

        var progress = new Progress<ScanProgress>(p =>
        {
            FilesScanned = p.FilesScanned;
            BytesScanned = p.BytesScanned;
            ElapsedTime = $"{p.Elapsed:hh\\:mm\\:ss}";
            StatusText = $"Scanning: {Path.GetFileName(p.CurrentPath)}";
        });

        try
        {
            var scanner = new DiskScanner();
            ScanResult = await scanner.ScanAsync(SelectedDrive, progress, _cts.Token);

            if (ScanResult != null)
            {
                StatusText = "Computing sizes…";

                // Pre-warm TotalSize cache so sorting is accurate
                await Task.Run(() => WarmCache(ScanResult));

                // Build tree (top-level sorted by size desc)
                var sorted = ScanResult.Children
                    .OrderByDescending(c => c.TotalSize)
                    .ToList();
                foreach (var child in sorted)
                    TreeItems.Add(new TreeFileItem(child, 0));

                TopLargestItems = await Task.Run(() =>
                    GetAllFiles(ScanResult)
                        .OrderByDescending(n => n.Size)
                        .Take(100)
                        .ToList());

                var stats = await Task.Run(() => FileTypeAnalyzer.AnalyzeTree(ScanResult));
                FileTypeStats.Clear();
                foreach (var s in stats) FileTypeStats.Add(s);

                CategoryStats = await Task.Run(() => BuildCategoryStats(ScanResult));

                ScanProgressValue = 100;
                var total = SizeFormatter.FormatBytes(ScanResult.TotalSize);
                StatusText = $"Done  —  {total}  |  {FilesScanned:N0} files  |  {ElapsedTime}";
            }
            else StatusText = "Scan cancelled";
        }
        catch (OperationCanceledException) { StatusText = "Cancelled"; ScanProgressValue = 0; }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
        finally { IsScanning = false; _cts?.Dispose(); }
    }

    private static void WarmCache(FileNode node)
    {
        _ = node.TotalSize;
        _ = node.TotalAllocated;
        foreach (var child in node.Children)
            WarmCache(child);
    }

    private void ToggleExpand(TreeFileItem? item)
    {
        if (item == null || !item.HasChildren) return;
        var idx = TreeItems.IndexOf(item);
        if (idx < 0) return;

        if (item.IsExpanded)
        {
            item.IsExpanded = false;
            int count = 0;
            for (int i = idx + 1; i < TreeItems.Count; i++)
            {
                if (TreeItems[i].Level > item.Level) count++;
                else break;
            }
            for (int i = 0; i < count; i++) TreeItems.RemoveAt(idx + 1);
        }
        else
        {
            item.IsExpanded = true;
            var children = item.Node.Children
                .OrderByDescending(c => c.TotalSize)
                .Select(c => new TreeFileItem(c, item.Level + 1))
                .ToList();
            for (int i = 0; i < children.Count; i++)
                TreeItems.Insert(idx + 1 + i, children[i]);
        }
    }

    private async void ScanCleanupAsync()
    {
        IsCleanupScanning = true;
        CleanupStatus = "Scanning for cleanup candidates…";
        CleanupItems.Clear();
        CleanupFileDetails.Clear();
        _cts = new CancellationTokenSource();

        var progress = new Progress<string>(s => CleanupStatus = s);
        try
        {
            var items = await CleanupAnalyzer.ScanAsync(progress, _cts.Token, SelectedDrive);
            foreach (var item in items) CleanupItems.Add(item);

            var totalSize = SizeFormatter.FormatBytes(items.Sum(i => i.Size));
            var totalFiles = items.Sum(i => i.FileCount);
            CleanupStatus = $"Found {items.Count} items  —  {totalSize} potential savings  —  {totalFiles:N0} files";
        }
        catch (OperationCanceledException) { CleanupStatus = "Cancelled"; }
        catch (Exception ex) { CleanupStatus = $"Error: {ex.Message}"; }
        finally { IsCleanupScanning = false; _cts?.Dispose(); }
    }

    private void RefreshCleanupDetails()
    {
        CleanupFileDetails.Clear();
        if (_selectedCleanupItem == null) return;
        foreach (var (path, size) in _selectedCleanupItem.TopFiles)
            CleanupFileDetails.Add(new CleanupFileEntry { Path = path, Size = size });
    }

    private async void DeleteCleanupItem(CleanupItem? item)
    {
        if (item == null) return;
        var msg = $"Permanently delete:\n\n{item.Path}\n\nSize: {item.SizeLabel}  |  {item.FileCountLabel}\n\nThis cannot be undone.";
        var confirm = System.Windows.MessageBox.Show(msg, "Confirm Permanent Delete",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        IsDeleting = true;
        await Notify(NotifState.Running, $"Deleting {item.Name}…", autoDismiss: false);

        string? error = null;
        var progress = new Progress<string>(s => { CleanupStatus = s; NotifMessage = s; OnPropertyChanged(nameof(NotifMessage)); });
        try
        {
            await CleanupAnalyzer.DeleteItemAsync(item, progress);
            CleanupItems.Remove(item);
            if (SelectedCleanupItem == item) SelectedCleanupItem = null;
            CleanupStatus = $"Deleted: {item.Name}  ({item.SizeLabel})";
        }
        catch (Exception ex) { error = ex.Message; }

        IsDeleting = false;
        if (error == null)
            await Notify(NotifState.Success, $"Deleted  {item.Name}  ({item.SizeLabel})");
        else
            await Notify(NotifState.Error, $"Delete failed: {error}");
    }

    private void Cancel()
    {
        _cts?.Cancel();
        StatusText = "Cancelling…";
        CleanupStatus = "Cancelling…";
    }

    private async void DeleteFile(FileNode? node)
    {
        if (node == null) return;
        var result = System.Windows.MessageBox.Show(
            $"Send \"{node.Name}\" to Recycle Bin?", "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes) return;

        await Notify(NotifState.Running, $"Sending to Recycle Bin: {node.Name}…", autoDismiss: false);

        bool ok = node.IsDirectory
            ? Services.FileActions.DeleteDirectoryToRecycleBin(node.FullPath)
            : Services.FileActions.DeleteToRecycleBin(node.FullPath);

        if (ok)
        {
            node.Parent?.Children.Remove(node);
            node.Parent?.InvalidateCache();
            StatusText = $"Sent to Recycle Bin: {node.Name}";
            await Notify(NotifState.Success, $"Sent to Recycle Bin: {node.Name}");
        }
        else
        {
            StatusText = $"Delete failed: {node.Name}";
            await Notify(NotifState.Error, $"Failed to send to Recycle Bin: {node.Name}");
        }
    }

    private List<CategoryStat> BuildCategoryStats(FileNode root)
    {
        var dict = new Dictionary<string, CategoryStat>(StringComparer.Ordinal);
        long total = root.TotalSize;
        foreach (var f in GetAllFiles(root))
        {
            if (!dict.TryGetValue(f.Category, out var s))
                dict[f.Category] = s = new CategoryStat { Category = f.Category };
            s.TotalSize += f.Size;
            s.FileCount++;
        }
        foreach (var s in dict.Values)
            s.Percentage = total > 0 ? s.TotalSize * 100.0 / total : 0;
        return [.. dict.Values.OrderByDescending(s => s.TotalSize)];
    }

    private void ExportCsv()
    {
        if (ScanResult == null) return;
        var dlg = new System.Windows.Forms.SaveFileDialog { Filter = "CSV|*.csv", FileName = "scan_result" };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        { Services.ExportService.ExportCsv(ScanResult, dlg.FileName); StatusText = $"Exported: {dlg.FileName}"; }
    }

    private void ExportJson()
    {
        if (ScanResult == null) return;
        var dlg = new System.Windows.Forms.SaveFileDialog { Filter = "JSON|*.json", FileName = "scan_result" };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        { Services.ExportService.ExportJson(ScanResult, dlg.FileName); StatusText = $"Exported: {dlg.FileName}"; }
    }

    private static IEnumerable<FileNode> GetAllFiles(FileNode node)
    {
        if (!node.IsDirectory) { yield return node; yield break; }
        foreach (var c in node.Children)
            foreach (var f in GetAllFiles(c))
                yield return f;
    }

    // ── Notification toast ───────────────────────────────────────
    public NotifState NotifState
    {
        get => _notifState;
        set { _notifState = value; OnPropertyChanged(); OnPropertyChanged(nameof(NotifVisible)); }
    }
    public string NotifMessage
    {
        get => _notifMessage;
        set { _notifMessage = value; OnPropertyChanged(); }
    }
    public bool NotifVisible => _notifState != NotifState.None;

    public string NotifIcon => _notifState switch
    {
        NotifState.Running => "",   // MDL2: ProgressRingDots
        NotifState.Success => "",   // MDL2: CheckMark
        NotifState.Error   => "",   // MDL2: ErrorBadge
        NotifState.Warning => "",   // MDL2: Warning
        _                  => ""
    };

    private async Task Notify(NotifState state, string message, bool autoDismiss = true)
    {
        _notifDismissCts?.Cancel();
        NotifState = state;
        NotifMessage = message;
        OnPropertyChanged(nameof(NotifIcon));

        if (autoDismiss && state != NotifState.Running)
        {
            _notifDismissCts = new CancellationTokenSource();
            try
            {
                await Task.Delay(3500, _notifDismissCts.Token);
                NotifState = NotifState.None;
            }
            catch (OperationCanceledException) { }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
