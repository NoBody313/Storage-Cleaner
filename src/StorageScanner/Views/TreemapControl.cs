using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using StorageScanner.Core;
using StorageScanner.Models;
using StorageScanner.Utils;

namespace StorageScanner.Views;

public class TreemapControl : FrameworkElement
{
    public static readonly DependencyProperty RootNodeProperty =
        DependencyProperty.Register("RootNode", typeof(FileNode), typeof(TreemapControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) =>
            {
                ((TreemapControl)d).RootNode = (FileNode)e.NewValue;
            }));

    public FileNode? RootNode
    {
        get => (FileNode)GetValue(RootNodeProperty);
        set => SetValue(RootNodeProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (RootNode == null)
            return;

        var rect = new Rect(0, 0, ActualWidth, ActualHeight);
        DrawTreemap(dc, RootNode, rect, 0);
    }

    private void DrawTreemap(DrawingContext dc, FileNode node, Rect rect, int depth)
    {
        if (rect.Width < 2 || rect.Height < 2 || depth > 3)
            return;

        if (node.IsDirectory && node.Children.Count > 0)
        {
            var children = node.Children.OrderByDescending(c => c.TotalSize).ToList();
            LayoutRectangles(dc, children, rect, depth);
        }
        else if (!node.IsDirectory)
        {
            var color = GetColorForType(node.Name);
            var brush = new SolidColorBrush(color);
            dc.DrawRectangle(brush, new System.Windows.Media.Pen(System.Windows.Media.Brushes.White, 0.5), rect);

            var ft = new FormattedText(Path.GetFileName(node.Name), CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, System.Windows.Media.Brushes.Black, 96);
            dc.DrawText(ft, new System.Windows.Point(rect.Left + 2, rect.Top + 2));
        }
    }

    private void LayoutRectangles(DrawingContext dc, List<FileNode> children, Rect bounds, int depth)
    {
        if (children.Count == 0)
            return;

        var totalSize = children.Sum(c => c.TotalSize);
        if (totalSize == 0)
            return;

        var rects = SquarifyLayout(children, bounds, totalSize);
        for (int i = 0; i < children.Count; i++)
        {
            DrawTreemap(dc, children[i], rects[i], depth + 1);
        }
    }

    private List<Rect> SquarifyLayout(List<FileNode> children, Rect bounds, long totalSize)
    {
        var rects = new List<Rect>();
        var x = bounds.Left;
        var y = bounds.Top;
        var width = bounds.Width;
        var height = bounds.Height;

        foreach (var child in children)
        {
            var ratio = (double)child.TotalSize / totalSize;

            if (width >= height)
            {
                var w = width * ratio;
                rects.Add(new Rect(x, y, w, height));
                x += w;
                width -= w;
            }
            else
            {
                var h = height * ratio;
                rects.Add(new Rect(x, y, width, h));
                y += h;
                height -= h;
            }
        }

        return rects;
    }

    private System.Windows.Media.Color GetColorForType(string fileName)
    {
        var category = FileTypeAnalyzer.GetCategory(fileName);
        return category switch
        {
            "Video" => System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B),
            "Audio" => System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00),
            "Image" => System.Windows.Media.Color.FromRgb(0xFF, 0x45, 0x00),
            "Archive" => System.Windows.Media.Color.FromRgb(0x1E, 0x90, 0xFF),
            "Document" => System.Windows.Media.Color.FromRgb(0x00, 0x8B, 0x8B),
            "Executable" => System.Windows.Media.Color.FromRgb(0xDC, 0x14, 0x3C),
            "System" => System.Windows.Media.Color.FromRgb(0x80, 0x80, 0x80),
            _ => System.Windows.Media.Color.FromRgb(0x20, 0xB2, 0xAA)
        };
    }
}
