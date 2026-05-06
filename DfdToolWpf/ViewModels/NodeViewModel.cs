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
    public class NodeViewModel : ViewModelBase
        {
            public Guid Id { get; set; } = Guid.NewGuid();
            private double _x, _y;
            private double _width = 100;
            private double _height = 50;
            private string _text;
            private string _fileFormat = string.Empty;
            private bool _isSelected, _isEditing, _isDashed, _isFileFormatVisible;
            private double _tailTargetX;
            private double _tailTargetY;
    
            private EditorMode _type;
            public EditorMode Type
            {
                get => _type;
                set
                {
                    if (_type == value) return;
                    _type = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsStickySpeechBubble));
                    OnPropertyChanged(nameof(StickySpeechBubbleVisibility));
                    OnPropertyChanged(nameof(TailHandleVisibility));
                    RefreshTail();
                }
            }
            public void OnTypeChangedForView()
            {
                OnPropertyChanged(nameof(Type));
                OnPropertyChanged(nameof(IsStickySpeechBubble));
                OnPropertyChanged(nameof(StickySpeechBubbleVisibility));
                OnPropertyChanged(nameof(TailHandleVisibility));
                RefreshTail();
            }
            
            public double X { get => _x; set { _x = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterX)); RefreshTail(); } }
            public double Y { get => _y; set { _y = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterY)); RefreshTail(); } }
            public double Width { get => _width; set { if (value > 0) _width = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterX)); RefreshTail(); } }
            public double Height { get => _height; set { if (value > 0) _height = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterY)); RefreshTail(); } }
            public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }
            public string FileFormat { get => _fileFormat; set { _fileFormat = value ?? string.Empty; OnPropertyChanged(); } }
            public bool IsFileFormatVisible { get => _isFileFormatVisible; set { _isFileFormatVisible = value; OnPropertyChanged(); } }
            public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); OnPropertyChanged(nameof(TailHandleVisibility)); } }
            public bool IsEditing { get => _isEditing; set { _isEditing = value; OnPropertyChanged(); } }
            public bool IsDashed { get => _isDashed; set { _isDashed = value; OnPropertyChanged(); } }
    
            public double TailTargetX { get => _tailTargetX; set { _tailTargetX = value; OnPropertyChanged(); RefreshTail(); } }
            public double TailTargetY { get => _tailTargetY; set { _tailTargetY = value; OnPropertyChanged(); RefreshTail(); } }
    
            public double CenterX => X + Width / 2; 
            public double CenterY => Y + Height / 2;
            public bool IsStickySpeechBubble => Type == EditorMode.StickySpeechBubble;
            public Visibility StickySpeechBubbleVisibility => IsStickySpeechBubble ? Visibility.Visible : Visibility.Collapsed;
            public Visibility TailHandleVisibility => IsStickySpeechBubble && IsSelected ? Visibility.Visible : Visibility.Collapsed;
            public double TailHandleLeft => TailTargetX - 6;
            public double TailHandleTop => TailTargetY - 6;
            public double TailHandleLocalLeft => TailTargetX - X - 6;
            public double TailHandleLocalTop => TailTargetY - Y - 6;
            public string TailPathData { get; private set; } = string.Empty;
            public string TailPathLocalData { get; private set; } = string.Empty;
    
            public void InitializeTailTargetIfNeeded()
            {
                if (Math.Abs(TailTargetX) < 0.0001 && Math.Abs(TailTargetY) < 0.0001)
                {
                    TailTargetX = X + Width + 60;
                    TailTargetY = Y + Height + 50;
                }
                else
                {
                    RefreshTail();
                }
            }
    
            public void RefreshTail()
            {
                if (!IsStickySpeechBubble)
                {
                    TailPathData = string.Empty;
                    TailPathLocalData = string.Empty;
                    OnPropertyChanged(nameof(TailPathData));
                    OnPropertyChanged(nameof(TailPathLocalData));
                    return;
                }
    
                double cx = CenterX;
                double cy = CenterY;
                double dx = TailTargetX - cx;
                double dy = TailTargetY - cy;
    
                if (Math.Sqrt(dx * dx + dy * dy) < 1)
                {
                    TailPathData = string.Empty;
                    TailPathLocalData = string.Empty;
                }
                else
                {
                    // しっぽの根元を付箋の中心ではなく、付箋の外周上に置く。
                    // これにより、吹き出し三角形が付箋の上側へはみ出して本文を邪魔しにくくなる。
                    const double halfBase = 12;
                    double left = X;
                    double right = X + Width;
                    double top = Y;
                    double bottom = Y + Height;
    
                    double scaleX = Math.Abs(dx) > 0.0001 ? (Width / 2.0) / Math.Abs(dx) : double.MaxValue;
                    double scaleY = Math.Abs(dy) > 0.0001 ? (Height / 2.0) / Math.Abs(dy) : double.MaxValue;
    
                    double bx;
                    double by;
                    Point base1;
                    Point base2;
    
                    if (scaleX < scaleY)
                    {
                        // 左右の辺からしっぽを出す。根元は縦方向に広げる。
                        bx = dx < 0 ? left : right;
                        by = cy + dy * scaleX;
                        by = Math.Max(top + halfBase, Math.Min(bottom - halfBase, by));
                        base1 = new Point(bx, by - halfBase);
                        base2 = new Point(bx, by + halfBase);
                    }
                    else
                    {
                        // 上下の辺からしっぽを出す。根元は横方向に広げる。
                        by = dy < 0 ? top : bottom;
                        bx = cx + dx * scaleY;
                        bx = Math.Max(left + halfBase, Math.Min(right - halfBase, bx));
                        base1 = new Point(bx - halfBase, by);
                        base2 = new Point(bx + halfBase, by);
                    }
    
                    TailPathData = $"M {base1.X},{base1.Y} L {TailTargetX},{TailTargetY} L {base2.X},{base2.Y} Z";
                    TailPathLocalData = $"M {base1.X - X},{base1.Y - Y} L {TailTargetX - X},{TailTargetY - Y} L {base2.X - X},{base2.Y - Y} Z";
                }
    
                OnPropertyChanged(nameof(TailPathData));
                OnPropertyChanged(nameof(TailPathLocalData));
                OnPropertyChanged(nameof(TailHandleLeft));
                OnPropertyChanged(nameof(TailHandleTop));
                OnPropertyChanged(nameof(TailHandleLocalLeft));
                OnPropertyChanged(nameof(TailHandleLocalTop));
            }
        }
    
}
