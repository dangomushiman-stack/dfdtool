using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace DfdToolWpf
{
    public class NodeData
        {
            public Guid Id { get; set; }
            public EditorMode Type { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public string Text { get; set; }
            public string FileFormat { get; set; }
            public bool IsFileFormatVisible { get; set; }
            public string StrokeColor { get; set; }
            public string FillColor { get; set; }
            public bool? IsDashed { get; set; }
            public double? TailTargetX { get; set; }
            public double? TailTargetY { get; set; }
            public string ImageDataBase64 { get; set; }
        }
    
}
