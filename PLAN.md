# StorageScanner — Disk Usage Scanner for Windows 11

## Context

User punya disk ~1TB hampir penuh dan butuh tool untuk tau **file/folder mana yang boros**, **lokasi path-nya**, dan **size-nya**. Tujuan: bersih-bersih storage.

Project baru di `E:\StorageScanner` (folder kosong, bukan git repo). Toolchain tersedia: `dotnet` (.NET), Python 3.10, Node.

**Keputusan (sudah dikonfirmasi user):**
- **Stack**: C# .NET 8 WPF (desktop GUI native Windows, single-exe).
- **Scan method**: recursive walk **multi-thread** pakai Win32 `FindFirstFileExW` + parallel enumeration. Alasan: jalan di semua drive, tak butuh admin, ~1-2 menit untuk 1TB penuh. MFT NTFS mode di-defer ke v2 (opsional turbo, butuh admin, kompleks).
- **Fitur v1**: Top N terbesar, Tree explorer, Treemap visual, Filter by type, Export CSV/JSON.
- **Aksi file**: Open in Explorer (select file), Delete ke Recycle Bin (restoreable).

## Architecture

Pola **MVVM**. Struktur project:

```
E:\StorageScanner\
  StorageScanner.sln
  src\StorageScanner\
    StorageScanner.csproj        # net8.0-windows, WPF, <UseWPF>true
    App.xaml / App.xaml.cs
    Models\
      FileNode.cs                # node tree: FullPath, Name, Size, IsDirectory, Children, Parent
    Core\
      NativeMethods.cs           # P/Invoke FindFirstFileExW / FindNextFileW / FindClose, WIN32_FIND_DATA
      DiskScanner.cs             # scan engine multi-thread, build tree, IProgress<ScanProgress>
      FileTypeStats.cs           # agregasi size per ekstensi/kategori
      ScanProgress.cs            # struct: FilesScanned, BytesScanned, CurrentPath
    Services\
      FileActions.cs             # OpenInExplorer, DeleteToRecycleBin
      ExportService.cs           # ExportCsv, ExportJson
    ViewModels\
      MainViewModel.cs           # orchestrate scan, hold root node, drive list, status
      FileNodeViewModel.cs       # wrapper FileNode untuk TreeView (lazy children, percent of parent)
      RelayCommand.cs            # ICommand impl
    Views\
      MainWindow.xaml/.cs        # layout: drive picker + Scan btn + progress + TabControl
      TreemapControl.cs          # custom FrameworkElement, squarified treemap via DrawingVisual
    Utils\
      SizeFormatter.cs           # bytes -> "1.4 GB"
```

## Implementation

### 1. Scan engine — `Core\DiskScanner.cs` + `NativeMethods.cs`
- P/Invoke `FindFirstFileExW` dengan `FINDEX_INFO_LEVELS.FindExInfoBasic` + flag `FIND_FIRST_EX_LARGE_FETCH` (lebih cepat dari `Directory.EnumerateFiles`).
- File size langsung dari `WIN32_FIND_DATA` (`nFileSizeHigh`/`nFileSizeLow`) — tak perlu query tambahan per file.
- Skip reparse points / symlink (cek `FILE_ATTRIBUTE_REPARSE_POINT`) → hindari loop & double-count.
- **Parallelisme**: enqueue subfolder ke `Parallel.ForEach` / `Channel` worker pool (degree = `Environment.ProcessorCount`). Tiap thread proses 1 folder, push subfolder baru ke queue.
- Bottom-up size aggregation: folder size = sum file langsung + sum subfolder size. Pakai `Interlocked.Add` pada counter, hitung total folder setelah subtree selesai.
- Try/catch akses-denied → skip diam-diam, hitung jumlah skipped.
- `IProgress<ScanProgress>` report tiap N file untuk update UI tanpa flooding (throttle ~10/detik).
- `CancellationToken` untuk tombol Cancel.

### 2. UI — `Views\MainWindow.xaml`
Layout atas-ke-bawah:
- **Top bar**: ComboBox daftar drive (`DriveInfo.GetDrives()`) + opsi browse folder spesifik, tombol **Scan** / **Cancel**, ProgressBar + label (file count, bytes, elapsed).
- **TabControl** (4 tab dari hasil scan):
  1. **Tree Explorer** — `TreeView` bind ke `FileNodeViewModel`, lazy-load children, tampil Name + Size + bar persen relatif ke parent. Urut desc by size.
  2. **Top Largest** — `DataGrid` flat list, top N (default 100) file & folder terbesar, kolom: Name, Full Path, Size, Type. Sortable.
  3. **Treemap** — `TreemapControl` (squarified algorithm). Kotak warna by kategori tipe. Klik kotak → drill-down. Tooltip path+size.
  4. **By Type** — `DataGrid` group per ekstensi/kategori (Video, Image, Archive, Executable, dll), kolom: Type, Total Size, File Count, %.
- **Context menu** (di Tree, Top, Treemap): "Open in Explorer", "Delete to Recycle Bin".

### 3. Treemap — `Views\TreemapControl.cs`
- Custom `FrameworkElement`, override `OnRender` pakai `DrawingContext`.
- **Squarified treemap** algorithm: bagi area proporsional ke size, jaga aspect ratio ~1.
- Limit: hanya render node ≥ min-pixel-area & batasi depth (mis. 3 level) → hindari lag jutaan kotak.
- Warna by kategori tipe file. Click → set drill-down root.

### 4. File actions — `Services\FileActions.cs`
- **OpenInExplorer**: `Process.Start("explorer.exe", $"/select,\"{path}\"")`.
- **DeleteToRecycleBin**: reference `Microsoft.VisualBasic` → `FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin)` (file) & `DeleteDirectory(...)` (folder). Restoreable.
- Konfirmasi dialog sebelum delete. Setelah delete, update tree (kurangi size parent).

### 5. Export — `Services\ExportService.cs`
- **CSV**: flat list semua node (atau top N) — Path, Size bytes, Size human, Type, IsDirectory.
- **JSON**: serialize tree pakai `System.Text.Json`.
- SaveFileDialog untuk pilih lokasi.

### 6. Helpers
- `SizeFormatter.FormatBytes(long)` → "B/KB/MB/GB/TB" 1 desimal.
- `FileTypeStats` — map ekstensi → kategori (Video, Audio, Image, Archive, Document, Executable, System, Other).

## Build & Run

```powershell
cd E:\StorageScanner
dotnet new sln -n StorageScanner          # (atau struktur dibuat manual)
dotnet build
dotnet run --project src\StorageScanner
# publish single-exe:
dotnet publish src\StorageScanner -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Verification

1. **Build sukses**: `dotnet build` tanpa error.
2. **Scan folder kecil dulu**: scan `E:\StorageScanner` atau folder kecil → verifikasi total size cocok vs Properties di Explorer (toleransi cluster slack).
3. **Scan drive penuh** (C: atau 1TB drive): pastikan selesai ~1-2 menit, progress jalan, tak crash di akses-denied, total mendekati "used space" di Explorer.
4. **Top Largest**: cek file terbesar yang dikenal muncul di urutan atas dengan path benar.
5. **Treemap**: render tanpa lag, klik drill-down jalan.
6. **By Type**: total per kategori masuk akal, jumlah = total.
7. **Open in Explorer**: klik → Explorer terbuka & file ter-select.
8. **Delete to Recycle Bin**: hapus 1 file test → cek masuk Recycle Bin & bisa restore, size parent ter-update.
9. **Export**: CSV & JSON ter-generate, isi valid (buka di Excel / parse JSON).

## Out of scope (v2+)
- MFT NTFS turbo mode (butuh admin, NTFS-only, parse biner).
- Scan network/cloud drive khusus.
- Scheduled / background monitoring.
