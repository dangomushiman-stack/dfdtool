using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DfdToolWpf
{
    public enum TableEditingPart
    {
        None,
        Header,
        Body
    }

    public class NodeViewModel : ViewModelBase
        {
            public Guid Id { get; set; } = Guid.NewGuid();
            private double _x, _y;
            private double _width = 100;
            private double _height = 50;
            private string _text;
            private string _tableHeaderText = string.Empty;
            private string _tableBodyText = string.Empty;
            private bool _isSynchronizingTableText;
            private TableEditingPart _editingTablePart = TableEditingPart.None;
            private string _fileFormat = string.Empty;
            private string _linkUrl = string.Empty;
            private string _jumpLabel = string.Empty;
            private string _imageDataBase64 = string.Empty;
            private NodeTextPlacement _textPlacement = NodeTextPlacement.Center;
            private ImageSource _imageSource;
            private string _strokeColor = "#4A90E2";
            private string _fillColor = "White";
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
                    OnPropertyChanged(nameof(Layer));
                    OnPropertyChanged(nameof(IsTable));
                    OnPropertyChanged(nameof(IsTableHeaderEditing));
                    OnPropertyChanged(nameof(IsTableBodyEditing));
                    InitializeTableTextFromTextIfNeeded();
                    OnPropertyChanged(nameof(JumpLabelDisplayText));
                    RefreshTail();
                }
            }
            public void OnTypeChangedForView()
            {
                OnPropertyChanged(nameof(Type));
                OnPropertyChanged(nameof(IsStickySpeechBubble));
                OnPropertyChanged(nameof(StickySpeechBubbleVisibility));
                OnPropertyChanged(nameof(TailHandleVisibility));
                OnPropertyChanged(nameof(Layer));
                OnPropertyChanged(nameof(IsTable));
                OnPropertyChanged(nameof(IsTableHeaderEditing));
                OnPropertyChanged(nameof(IsTableBodyEditing));
                InitializeTableTextFromTextIfNeeded();
                OnPropertyChanged(nameof(JumpLabelDisplayText));
                RefreshTail();
            }
            
            public double X { get => _x; set { _x = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterX)); RefreshTail(); } }
            public double Y { get => _y; set { _y = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterY)); RefreshTail(); } }
            public double Width { get => _width; set { if (value > 0) _width = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterX)); RefreshTail(); } }
            public double Height { get => _height; set { if (value > 0) _height = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterY)); RefreshTail(); } }
            public string Text
            {
                get => _text;
                set
                {
                    _text = value ?? string.Empty;
                    OnPropertyChanged();

                    if (!_isSynchronizingTableText && Type == EditorMode.Table)
                    {
                        SplitTextIntoTableFields(_text);
                    }

                    OnPropertyChanged(nameof(JumpLabelDisplayText));
                }
            }

            public string TableHeaderText
            {
                get => _tableHeaderText;
                set
                {
                    string newValue = value ?? string.Empty;
                    if (_tableHeaderText == newValue) return;
                    _tableHeaderText = newValue;
                    OnPropertyChanged();
                    UpdateTextFromTableFields();
                    OnPropertyChanged(nameof(JumpLabelDisplayText));
                }
            }

            public string TableBodyText
            {
                get => _tableBodyText;
                set
                {
                    string newValue = value ?? string.Empty;
                    if (_tableBodyText == newValue) return;
                    _tableBodyText = newValue;
                    OnPropertyChanged();
                    UpdateTextFromTableFields();
                }
            }

            public TableEditingPart EditingTablePart
            {
                get => _editingTablePart;
                set
                {
                    if (_editingTablePart == value) return;
                    _editingTablePart = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsTableHeaderEditing));
                    OnPropertyChanged(nameof(IsTableBodyEditing));
                }
            }
            public NodeTextPlacement TextPlacement
            {
                get => _textPlacement;
                set
                {
                    if (_textPlacement == value) return;
                    _textPlacement = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LabelHorizontalAlignment));
                    OnPropertyChanged(nameof(LabelVerticalAlignment));
                    OnPropertyChanged(nameof(LabelTextAlignment));
                    OnPropertyChanged(nameof(EditHorizontalContentAlignment));
                    OnPropertyChanged(nameof(EditVerticalContentAlignment));
                    OnPropertyChanged(nameof(EditTextAlignment));
                    OnPropertyChanged(nameof(IsTextPlacementCenter));
                    OnPropertyChanged(nameof(IsTextPlacementTopLeft));
                }
            }
            public string FileFormat { get => _fileFormat; set { _fileFormat = value ?? string.Empty; OnPropertyChanged(); } }
            public string LinkUrl { get => _linkUrl; set { _linkUrl = value ?? string.Empty; OnPropertyChanged(); } }
            public string JumpLabel
            {
                get => _jumpLabel;
                set
                {
                    string newValue = value ?? string.Empty;
                    if (_jumpLabel == newValue) return;
                    _jumpLabel = newValue;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasJumpLabel));
                    OnPropertyChanged(nameof(JumpLabelVisibility));
                    OnPropertyChanged(nameof(JumpLabelDisplayText));
                }
            }
            public string ImageDataBase64
            {
                get => _imageDataBase64;
                set
                {
                    _imageDataBase64 = value ?? string.Empty;
                    UpdateImageSourceFromBase64();
                    OnPropertyChanged();
                }
            }
            public ImageSource ImageSource { get => _imageSource; private set { _imageSource = value; OnPropertyChanged(); } }
            public string StrokeColor { get => _strokeColor; set { _strokeColor = string.IsNullOrWhiteSpace(value) ? "#4A90E2" : value; OnPropertyChanged(); } }
            public string FillColor { get => _fillColor; set { _fillColor = string.IsNullOrWhiteSpace(value) ? "White" : value; OnPropertyChanged(); } }
            public bool IsFileFormatVisible { get => _isFileFormatVisible; set { _isFileFormatVisible = value; OnPropertyChanged(); } }
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value) return;
                    _isSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TailHandleVisibility));
                    OnPropertyChanged(nameof(Layer));
                }
            }
            public bool IsEditing
            {
                get => _isEditing;
                set
                {
                    if (_isEditing == value) return;
                    _isEditing = value;
                    if (!_isEditing) EditingTablePart = TableEditingPart.None;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsTableHeaderEditing));
                    OnPropertyChanged(nameof(IsTableBodyEditing));
                }
            }
            public bool IsDashed { get => _isDashed; set { _isDashed = value; OnPropertyChanged(); } }
    
            public double TailTargetX { get => _tailTargetX; set { _tailTargetX = value; OnPropertyChanged(); RefreshTail(); } }
            public double TailTargetY { get => _tailTargetY; set { _tailTargetY = value; OnPropertyChanged(); RefreshTail(); } }
    
            public double CenterX => X + Width / 2; 
            public double CenterY => Y + Height / 2;
            public bool IsStickySpeechBubble => Type == EditorMode.StickySpeechBubble;
            public bool IsTable => Type == EditorMode.Table;
            public bool IsTableHeaderEditing => IsEditing && Type == EditorMode.Table && EditingTablePart == TableEditingPart.Header;
            public bool IsTableBodyEditing => IsEditing && Type == EditorMode.Table && EditingTablePart == TableEditingPart.Body;

            // 同じ ItemsControl/Canvas 内では、Panel.ZIndex が同じ場合は追加順が前後関係に影響する。
            // 図形の種類ごとにレイヤーを固定し、後から配置した枠が通常図形の手前に来ないようにする。
            public int Layer
            {
                get
                {
                    // 選択中の枠は、ほかの枠よりは前面に出す。
                    // ただし通常図形より前面には出さない。
                    //
                    // 以前は選択中ノードを +1000 していたため、選択中の枠が通常図形の上にかぶさり、
                    // 枠内の図形をクリックしても枠ボディクリック扱いになって、配置処理が優先されていた。
                    //
                    // レイヤー順：
                    //   未選択の分類枠        -20
                    //   未選択の接続枠        -10
                    //   選択中の分類枠          0
                    //   選択中の接続枠          5
                    //   通常図形               20
                    //   選択中の通常図形     1020
                    return Type switch
                    {
                        EditorMode.CategoryFrame => IsSelected ? 0 : -20,
                        EditorMode.ConnectableFrame => IsSelected ? 5 : -10,
                        _ => IsSelected ? 1020 : 20
                    };
                }
            }

            public HorizontalAlignment LabelHorizontalAlignment => TextPlacement == NodeTextPlacement.TopLeft ? HorizontalAlignment.Left : HorizontalAlignment.Center;
            public VerticalAlignment LabelVerticalAlignment => TextPlacement == NodeTextPlacement.TopLeft ? VerticalAlignment.Top : VerticalAlignment.Center;
            public TextAlignment LabelTextAlignment => TextPlacement == NodeTextPlacement.TopLeft ? TextAlignment.Left : TextAlignment.Center;
            public HorizontalAlignment EditHorizontalContentAlignment => TextPlacement == NodeTextPlacement.TopLeft ? HorizontalAlignment.Left : HorizontalAlignment.Center;
            public VerticalAlignment EditVerticalContentAlignment => TextPlacement == NodeTextPlacement.TopLeft ? VerticalAlignment.Top : VerticalAlignment.Center;
            public TextAlignment EditTextAlignment => TextPlacement == NodeTextPlacement.TopLeft ? TextAlignment.Left : TextAlignment.Center;
            public bool IsTextPlacementCenter => TextPlacement == NodeTextPlacement.Center;
            public bool IsTextPlacementTopLeft => TextPlacement == NodeTextPlacement.TopLeft;
            public bool HasJumpLabel => !string.IsNullOrWhiteSpace(JumpLabel);
            public Visibility JumpLabelVisibility => HasJumpLabel ? Visibility.Visible : Visibility.Collapsed;
            public string JumpLabelDisplayText
            {
                get
                {
                    string text = Type == EditorMode.Table ? TableHeaderText : Text;
                    text = (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Trim();
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return "ジャンプ先";
                    }

                    return text.Split('\n').FirstOrDefault()?.Trim() ?? "ジャンプ先";
                }
            }

            public Visibility StickySpeechBubbleVisibility => IsStickySpeechBubble ? Visibility.Visible : Visibility.Collapsed;
            public Visibility TailHandleVisibility => IsStickySpeechBubble && IsSelected ? Visibility.Visible : Visibility.Collapsed;
            public double TailHandleLeft => TailTargetX - 6;
            public double TailHandleTop => TailTargetY - 6;
            public double TailHandleLocalLeft => TailTargetX - X - 6;
            public double TailHandleLocalTop => TailTargetY - Y - 6;
            public string TailPathData { get; private set; } = string.Empty;
            public string TailPathLocalData { get; private set; } = string.Empty;

            public void InitializeTableTextFromTextIfNeeded()
            {
                if (Type != EditorMode.Table) return;

                if (string.IsNullOrEmpty(_tableHeaderText) && string.IsNullOrEmpty(_tableBodyText))
                {
                    SplitTextIntoTableFields(_text ?? string.Empty);
                }
            }

            private void SplitTextIntoTableFields(string text)
            {
                string normalized = (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
                string[] lines = normalized.Split('\n');

                _tableHeaderText = lines.Length > 0 && !string.IsNullOrWhiteSpace(lines[0]) ? lines[0] : "table";
                _tableBodyText = lines.Length > 1 ? string.Join("\n", lines.Skip(1)) : string.Empty;

                OnPropertyChanged(nameof(TableHeaderText));
                OnPropertyChanged(nameof(TableBodyText));
                OnPropertyChanged(nameof(JumpLabelDisplayText));
            }

            private void UpdateTextFromTableFields()
            {
                if (Type != EditorMode.Table) return;

                _isSynchronizingTableText = true;
                try
                {
                    _text = string.IsNullOrEmpty(_tableBodyText)
                        ? _tableHeaderText
                        : $"{_tableHeaderText}\n{_tableBodyText}";
                    OnPropertyChanged(nameof(Text));
                    OnPropertyChanged(nameof(JumpLabelDisplayText));
                }
                finally
                {
                    _isSynchronizingTableText = false;
                }
            }

            private void UpdateImageSourceFromBase64()
            {
                if (string.IsNullOrWhiteSpace(_imageDataBase64))
                {
                    ImageSource = null;
                    return;
                }

                try
                {
                    byte[] bytes = Convert.FromBase64String(_imageDataBase64);
                    using var stream = new MemoryStream(bytes);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    ImageSource = bitmap;
                }
                catch
                {
                    ImageSource = null;
                }
            }
    
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
