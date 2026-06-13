# Storage Cleaner

> [English](README.en.md) | **Indonesia**

Aplikasi analisis penggunaan disk untuk Windows 11 — scan drive/folder, visualisasi storage, temukan & bersihkan file yang memakan banyak ruang.

## Fitur

### Engine Scan
- Win32 `FindFirstFileW` / `FindNextFileW` — lebih cepat dari `Directory.EnumerateFiles` bawaan .NET
- Scan rekursif multi-thread (`ConcurrentQueue` + `Task.Run`)
- Baca ukuran file langsung dari `WIN32_FIND_DATAW` (tanpa query tambahan per file)
- Skip reparse point / symlink — tidak ada double-counting atau infinite loop
- Ukuran allocated berbasis cluster: `ceil(fileSize / clusterSize) * clusterSize` via `GetDiskFreeSpaceW`
- Deteksi file terkompresi NTFS (`FILE_ATTRIBUTE_COMPRESSED`)
- Dukungan `CancellationToken` — tombol Cancel menghentikan scan di tengah jalan
- Progress di-throttle via `IProgress<ScanProgress>` — tidak membanjiri UI

### Tab Tree Explorer
Kolom tersedia:

| Kolom     | Keterangan                                     |
|-----------|------------------------------------------------|
| Name      | Nama file/folder dengan indent + ikon          |
| Size      | Total ukuran (rekursif ke dalam)               |
| %         | Persentase terhadap folder induk               |
| Allocated | Ukuran allocated (dibulatkan ke cluster)       |
| Items     | Total item (file + folder) di dalamnya         |
| Files     | Jumlah file di dalamnya                        |
| Folders   | Jumlah subfolder di dalamnya                   |
| Category  | Kategori yang terdeteksi otomatis (lihat bawah)|

- Klik ikon chevron untuk expand/collapse folder — anak diurutkan by size desc
- Ikon Segoe MDL2 Assets: folder, tipe file (video, musik, gambar, zip, exe, pdf, doc, kode, iso)
- Context menu: **Buka di Explorer** / **Kirim ke Recycle Bin**

### Tab Top 100 Terbesar
- Daftar flat 100 file terbesar di seluruh hasil scan
- Kolom: Name, Path, Size, Allocated, %, Category, Compressed
- Sortable — klik header kolom untuk mengurutkan

### Tab Treemap
- Treemap squarified — luas kotak proporsional dengan ukuran file
- Warna berdasarkan kategori
- Dirender via `DrawingContext` menggunakan custom `FrameworkElement`

### Tab By Type
- Ukuran teragregasi per ekstensi file
- Kolom: Extension, Total Size, Files, %

### Tab By Category
- Ukuran teragregasi per kategori yang terdeteksi otomatis
- Kolom: Category, Total Size, Files, %

### Tab Cleanup Analyzer
Menemukan file yang aman dihapus tanpa menyentuh data pengguna, **berbasis drive yang dipilih**.

**Target sistem yang dipindai otomatis:**
| Path | Kategori | Keamanan |
|------|----------|----------|
| `%LOCALAPPDATA%\Temp` | Temp | Aman |
| `C:\Windows\Temp` | Temp | Aman |
| `C:\Windows\Prefetch` | Windows Cache | Aman |
| `%LOCALAPPDATA%\Microsoft\Windows\WER` | Windows Cache | Aman |
| `C:\Windows\SoftwareDistribution\Download` | Windows Update | Aman |
| `C:\Windows.old` | Old Windows | Aman |
| `C:\$Windows.~BT` / `C:\$Windows.~WS` | Old Windows | Aman |
| `%LOCALAPPDATA%\pip\Cache` | Dev Cache | Aman |
| `%APPDATA%\npm-cache` | Dev Cache | Aman |
| `%LOCALAPPDATA%\Yarn\Cache` | Dev Cache | Aman |
| `~\.gradle\caches` | Dev Cache | Aman |
| `%LOCALAPPDATA%\NuGet\Cache` | Dev Cache | Aman |
| `~\.nuget\packages` | Dev Cache | Review |
| `~\.m2\repository` | Dev Cache | Review |
| Cache Chrome / Edge | Browser | Aman |
| `%LOCALAPPDATA%\Docker\wsl` | Docker | Hati-hati |
| `%LOCALAPPDATA%\Docker\log` | Docker | Aman |
| `$Recycle.Bin` (drive terpilih) | Recycle Bin | Review |

**Scan dev waste** — mencari pola folder di drive yang dipilih (kedalaman maks 6):
`node_modules`, `__pycache__`, `.pytest_cache`, `.tox`, `.next`, `.nuxt`, `.parcel-cache`, `dist`, `build`, `.gradle`

**Label keamanan:**
- `Aman` — dibangun ulang otomatis oleh sistem/tools, tidak ada kehilangan data
- `Review` — bisa di-restore tapi mungkin perlu download ulang (NuGet, Maven, npm)
- `Hati-hati` — mengandung data nyata (image, volume Docker)

Panel detail menampilkan **semua file** di dalam item cleanup yang dipilih, sortable by size.

**Hapus** — hapus permanen (bukan Recycle Bin), memerlukan konfirmasi. Toast notifikasi menampilkan status: Running / Berhasil / Gagal.

### Kategori File (deteksi otomatis berdasarkan path)
| Kategori | Path yang dicocokkan |
|----------|----------------------|
| System | `C:\Windows\`, `C:\Program Files\` |
| Docker | `\Docker\` |
| Downloads | `\Downloads\` |
| AppData | `\AppData\` |
| Temp | `\Temp\`, `\tmp\` |
| Dev | `node_modules`, `.git`, `__pycache__`, dll. |
| Other | semua lainnya |

### Export
- **CSV** — daftar flat semua node yang discan (Path, Size bytes, Size human, Category, IsDirectory)
- **JSON** — tree rekursif lengkap

### Notifikasi
Toast muncul di sudut kanan bawah saat operasi delete:
- Biru (Running) — sedang berjalan
- Hijau (Berhasil) — selesai, auto-hilang 3.5 detik
- Merah (Gagal) — error, auto-hilang 3.5 detik

## Tech Stack

| Layer | Teknologi |
|-------|-----------|
| Bahasa | C# 13 / .NET 9 |
| UI | WPF (Windows Presentation Foundation) |
| Pattern | MVVM (`INotifyPropertyChanged`, `ObservableCollection`) |
| Native scan | P/Invoke: `FindFirstFileW`, `FindNextFileW`, `FindClose`, `GetDiskFreeSpaceW` |
| Recycle Bin | P/Invoke: `SHFileOperation` dengan `FOF_ALLOWUNDO` |
| Dialog file | `System.Windows.Forms` (`FolderBrowserDialog`, `SaveFileDialog`) |
| Ikon | Segoe MDL2 Assets (Unicode Private Use Area) |
| Threading | `ConcurrentQueue`, `Task.Run`, `CancellationTokenSource` |
| Export | `System.Text.Json` (JSON), `StreamWriter` (CSV) |

## Build

Kebutuhan: .NET 9 SDK, Windows

```powershell
# Build
dotnet build src\StorageScanner -c Release

# Run (development)
dotnet run --project src\StorageScanner

# Publish single self-contained exe (~70MB)
dotnet publish src\StorageScanner -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish\
```

Output: `publish\StorageScanner.exe` — tidak perlu install, langsung jalankan.

## Versioning

Menggunakan [Semantic Versioning](https://semver.org/): `MAJOR.MINOR.PATCH`

| Versi | Perubahan |
|-------|-----------|
| 1.0.0 | Rilis pertama |

## Struktur Project

```
StorageScanner\
  src\StorageScanner\
    Models\
      FileNode.cs              # node tree: Size, AllocatedSize, TotalSize (lazy), Category, Children
    Core\
      NativeMethods.cs         # Win32 P/Invoke, GetClusterSize, CalcAllocated
      DiskScanner.cs           # engine scan multi-thread
      CleanupAnalyzer.cs       # scanner temp/cache/dev-waste + delete, scoped per drive
      FileTypeAnalyzer.cs      # statistik per ekstensi
      ScanProgress.cs          # struct laporan progress
    Services\
      FileActions.cs           # OpenInExplorer, DeleteToRecycleBin (SHFileOperation)
      ExportService.cs         # export CSV + JSON
    ViewModels\
      MainViewModel.cs         # orkestrasi, commands, WarmCache, ToggleExpand, toast notif
      TreeFileItem.cs          # baris DataGrid flat: indent, ikon MDL2, expand state
      RelayCommand.cs          # implementasi ICommand
    Views\
      TreemapControl.cs        # FrameworkElement kustom treemap squarified
    Converters\
      BoolToVisibilityConverter.cs
    Utils\
      SizeFormatter.cs         # FormatBytes → "1.4 GB"
    App.xaml                   # style global: warna, button, DataGrid, TabItem, ComboBox
    MainWindow.xaml            # layout utama: toolbar, tabs, status bar, toast
```

## Keterbatasan

- Scan butuh akses baca — folder sistem yang dilindungi dilewati secara diam-diam
- MFT direct read belum diimplementasi — menggunakan recursive walk (~1-2 menit untuk 1TB penuh)
- Drill-down treemap belum ada — hanya menampilkan top-level
- Docker WSL disk (`ext4.vhdx`) dilaporkan sebagai satu file — konten sebenarnya hanya terlihat dari dalam WSL

## Lisensi

[MIT](LICENSE) © 2025 NoBody313
