# Storage Cleaner

> **English** | [Indonesia](README.md)

Windows 11 disk usage analyzer — scan drive/folder, visualize storage, find & clean waste.

## Features

### Scan Engine
- Win32 `FindFirstFileW` / `FindNextFileW` — faster than .NET `Directory.EnumerateFiles`
- Multi-threaded recursive walk (`ConcurrentQueue` + `Task.Run`)
- Reads file size directly from `WIN32_FIND_DATAW` (no extra stat call per file)
- Skips reparse points / symlinks — no double-counting, no infinite loops
- Cluster-aligned allocated size: `ceil(fileSize / clusterSize) * clusterSize` via `GetDiskFreeSpaceW`
- Detects NTFS compressed files (`FILE_ATTRIBUTE_COMPRESSED`)
- `CancellationToken` support — Cancel button stops scan mid-flight
- Progress throttled via `IProgress<ScanProgress>` — no UI flooding

### Tree Explorer Tab
Available columns:

| Column    | Description                          |
|-----------|--------------------------------------|
| Name      | File/folder name with indent + icon  |
| Size      | Total size (files inside, recursive) |
| %         | Percent of parent folder             |
| Allocated | Cluster-rounded allocated size       |
| Items     | Total items (files + folders) inside |
| Files     | File count inside                    |
| Folders   | Subfolder count inside               |
| Category  | Auto-detected category (see below)   |

- Click chevron icon to expand/collapse folder — children sorted by size desc
- Segoe MDL2 Assets icons: folder, file types (video, music, image, zip, exe, pdf, doc, code, iso)
- Context menu: **Open in Explorer** / **Send to Recycle Bin**

### Top 100 Largest Tab
- Flat list of 100 largest files across entire scan
- Columns: Name, Path, Size, Allocated, %, Category, Compressed

### Treemap Tab
- Squarified treemap — area proportional to file size
- Color-coded by category
- Custom `FrameworkElement` rendered via `DrawingContext`

### By Type Tab
- Aggregated size per file extension
- Columns: Extension, Total Size, Files, %

### By Category Tab
- Aggregated size per auto-detected category
- Columns: Category, Total Size, Files, %

### Cleanup Analyzer Tab
Finds safe-to-delete waste without touching user data.

**Known system targets scanned automatically:**
| Path | Category | Safety |
|------|----------|--------|
| `%LOCALAPPDATA%\Temp` | Temp | Safe |
| `C:\Windows\Temp` | Temp | Safe |
| `C:\Windows\Prefetch` | Windows Cache | Safe |
| `%LOCALAPPDATA%\Microsoft\Windows\WER` | Windows Cache | Safe |
| `C:\Windows\SoftwareDistribution\Download` | Windows Update | Safe |
| `C:\Windows.old` | Old Windows | Safe |
| `C:\$Windows.~BT` / `C:\$Windows.~WS` | Old Windows | Safe |
| `%LOCALAPPDATA%\pip\Cache` | Dev Cache | Safe |
| `%APPDATA%\npm-cache` | Dev Cache | Safe |
| `%LOCALAPPDATA%\Yarn\Cache` | Dev Cache | Safe |
| `~\.gradle\caches` | Dev Cache | Safe |
| `%LOCALAPPDATA%\NuGet\Cache` | Dev Cache | Safe |
| `~\.nuget\packages` | Dev Cache | Review |
| `~\.m2\repository` | Dev Cache | Review |
| Chrome / Edge cache | Browser | Safe |
| `%LOCALAPPDATA%\Docker\wsl` | Docker | Caution |
| `%LOCALAPPDATA%\Docker\log` | Docker | Safe |
| `$Recycle.Bin` (all drives) | Recycle Bin | Review |

**Dev waste scan** — searches common dev roots (`~/source`, `~/projects`, `~/dev`, `C:\dev`, `D:\dev`, etc.) up to depth 6 for:
`node_modules`, `__pycache__`, `.pytest_cache`, `.tox`, `.next`, `.nuxt`, `.parcel-cache`, `dist`, `build`, `.gradle`

**Safety labels:**
- `Safe` — auto-rebuilt by system/tools, no data loss
- `Review` — restorable but may need re-download (NuGet, Maven, npm)
- `Caution` — contains real data (Docker images, volumes)

Detail panel shows top 10 largest files inside selected cleanup item.

**Delete** — permanent delete (not Recycle Bin), confirmation dialog required.

### File Categories (auto-detect by path)
| Category | Paths matched |
|----------|---------------|
| System | `C:\Windows\`, `C:\Program Files\` |
| Docker | `\Docker\` |
| Downloads | `\Downloads\` |
| AppData | `\AppData\` |
| Temp | `\Temp\`, `\tmp\` |
| Dev | `node_modules`, `.git`, `__pycache__` etc. |
| Other | everything else |

### Export
- **CSV** — flat list of all scanned nodes (Path, Size bytes, Size human, Category, IsDirectory)
- **JSON** — full recursive tree

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Language | C# 13 / .NET 9 |
| UI | WPF (Windows Presentation Foundation) |
| Pattern | MVVM (`INotifyPropertyChanged`, `ObservableCollection`) |
| Native scan | P/Invoke: `FindFirstFileW`, `FindNextFileW`, `FindClose`, `GetDiskFreeSpaceW` |
| Recycle Bin | P/Invoke: `SHFileOperation` with `FOF_ALLOWUNDO` |
| File dialogs | `System.Windows.Forms` (`FolderBrowserDialog`, `SaveFileDialog`) |
| Icons | Segoe MDL2 Assets (Unicode Private Use Area) |
| Threading | `ConcurrentQueue`, `Task.Run`, `CancellationTokenSource` |
| Export | `System.Text.Json` (JSON), `StreamWriter` (CSV) |

## Build

Requirements: .NET 9 SDK, Windows

```powershell
# Build
dotnet build src\StorageScanner -c Release

# Run (dev)
dotnet run --project src\StorageScanner

# Publish single self-contained exe (~70MB)
dotnet publish src\StorageScanner -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish\
```

Output: `publish\StorageScanner.exe` — no install needed, run anywhere.

## Versioning

Uses [Semantic Versioning](https://semver.org/): `MAJOR.MINOR.PATCH`

| Version | Changes |
|---------|---------|
| 1.0.0   | Initial release |

## Project Structure

```
StorageScanner\
  src\StorageScanner\
    Models\
      FileNode.cs              # tree node: Size, AllocatedSize, TotalSize (lazy), Category, Children
    Core\
      NativeMethods.cs         # Win32 P/Invoke, GetClusterSize, CalcAllocated
      DiskScanner.cs           # multi-thread scan engine
      CleanupAnalyzer.cs       # temp/cache/dev-waste scanner + delete
      FileTypeAnalyzer.cs      # per-extension stats
      ScanProgress.cs          # progress report struct
    Services\
      FileActions.cs           # OpenInExplorer, DeleteToRecycleBin (SHFileOperation)
      ExportService.cs         # CSV + JSON export
    ViewModels\
      MainViewModel.cs         # orchestration, commands, WarmCache, ToggleExpand
      TreeFileItem.cs          # flat DataGrid row: indent, MDL2 icons, expand state
      RelayCommand.cs          # ICommand impl
    Views\
      TreemapControl.cs        # custom FrameworkElement squarified treemap
    Converters\
      BoolToVisibilityConverter.cs
    Utils\
      SizeFormatter.cs         # FormatBytes → "1.4 GB"
    App.xaml                   # global styles: colors, buttons, DataGrid, TabItem, ComboBox
    MainWindow.xaml            # main layout: toolbar, tabs, status bar
```

## Known Limitations

- Scan needs read access — protected system folders skipped silently
- MFT direct read not implemented — uses recursive walk instead (~1-2 min for 1TB)
- Treemap drill-down not implemented — shows top-level only
- Docker WSL disk (`ext4.vhdx`) reported as single file — actual contents only visible inside WSL
