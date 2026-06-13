using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using StorageScanner.Models;
using StorageScanner.Utils;

namespace StorageScanner.Services;

public static class ExportService
{
    public static void ExportCsv(FileNode root, string filePath)
    {
        using var writer = new StreamWriter(filePath);
        writer.WriteLine("Path,Size (Bytes),Size (Human),Type,IsDirectory");

        var allNodes = FlattenTree(root);
        foreach (var node in allNodes.OrderByDescending(n => n.Size))
        {
            var type = node.IsDirectory ? "Folder" : Path.GetExtension(node.Name);
            writer.WriteLine($"\"{node.FullPath}\",{node.Size},\"{SizeFormatter.FormatBytes(node.Size)}\",\"{type}\",{node.IsDirectory}");
        }
    }

    public static void ExportJson(FileNode root, string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(NodeToJson(root), options);
        File.WriteAllText(filePath, json);
    }

    private static Dictionary<string, object?> NodeToJson(FileNode node)
    {
        var dict = new Dictionary<string, object?>
        {
            { "name", node.Name },
            { "path", node.FullPath },
            { "size", node.Size },
            { "isDirectory", node.IsDirectory }
        };

        if (node.Children.Count > 0)
            dict["children"] = node.Children.Select(NodeToJson).ToList();

        return dict;
    }

    private static List<FileNode> FlattenTree(FileNode node, List<FileNode>? result = null)
    {
        result ??= [];
        result.Add(node);
        foreach (var child in node.Children)
            FlattenTree(child, result);
        return result;
    }
}
