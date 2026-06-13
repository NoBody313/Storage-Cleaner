using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using StorageScanner.Models;

namespace StorageScanner.Core;

public class DiskScanner
{
    private IProgress<ScanProgress>? _progress;
    private CancellationToken _cancellationToken;
    private long _filesScanned;
    private long _bytesScanned;
    private long _skippedFolders;
    private DateTime _startTime;
    private long _clusterSize;

    public async Task<FileNode> ScanAsync(string rootPath, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        _progress = progress;
        _cancellationToken = cancellationToken;
        _filesScanned = 0;
        _bytesScanned = 0;
        _skippedFolders = 0;
        _startTime = DateTime.Now;
        _clusterSize = NativeMethods.GetClusterSize(rootPath);

        var rootName = Path.GetFileName(rootPath);
        var root = new FileNode(rootPath, string.IsNullOrEmpty(rootName) ? rootPath : rootName, 0, 0, true);

        var queue = new ConcurrentQueue<(FileNode parent, string path)>();
        queue.Enqueue((root, rootPath));

        await Task.Run(() =>
        {
            while (queue.TryDequeue(out var item))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    ScanFolder(item.parent, item.path, queue);
                }
                catch (UnauthorizedAccessException)
                {
                    Interlocked.Increment(ref _skippedFolders);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    _ = ex;
                    Interlocked.Increment(ref _skippedFolders);
                }

                ReportProgress(item.parent.FullPath);
            }
        }, cancellationToken);

        return root;
    }

    private void ScanFolder(FileNode parent, string folderPath, ConcurrentQueue<(FileNode, string)> queue)
    {
        var searchPath = Path.Combine(folderPath, "*");

        var handle = NativeMethods.FindFirstFileW(searchPath, out var findData);
        if (handle.ToInt64() == NativeMethods.INVALID_HANDLE_VALUE)
            return;

        try
        {
            do
            {
                if (_cancellationToken.IsCancellationRequested)
                    break;

                var fileName = findData.cFileName;
                if (fileName == "." || fileName == "..")
                    continue;

                if (findData.IsReparsePoint)
                    continue;

                var fullPath = Path.Combine(folderPath, fileName);

                if (findData.IsDirectory)
                {
                    var dirNode = new FileNode(fullPath, fileName, 0, 0, true, false, parent);
                    lock (parent.Children)
                        parent.Children.Add(dirNode);
                    queue.Enqueue((dirNode, fullPath));
                }
                else
                {
                    var fileSize = findData.FileSize;
                    var allocated = NativeMethods.CalcAllocated(fileSize, _clusterSize);
                    var isCompressed = findData.IsCompressed;
                    var fileNode = new FileNode(fullPath, fileName, fileSize, allocated, false, isCompressed, parent);
                    lock (parent.Children)
                        parent.Children.Add(fileNode);

                    Interlocked.Add(ref _bytesScanned, fileSize);
                    Interlocked.Increment(ref _filesScanned);
                }

            } while (NativeMethods.FindNextFileW(handle, out findData));
        }
        finally
        {
            NativeMethods.FindClose(handle);
        }
    }

    private void ReportProgress(string currentPath)
    {
        if (Interlocked.Read(ref _filesScanned) % 100 == 0)
        {
            _progress?.Report(new ScanProgress
            {
                FilesScanned = Interlocked.Read(ref _filesScanned),
                BytesScanned = Interlocked.Read(ref _bytesScanned),
                CurrentPath = currentPath,
                SkippedFolders = Interlocked.Read(ref _skippedFolders),
                StartTime = _startTime
            });
        }
    }
}
