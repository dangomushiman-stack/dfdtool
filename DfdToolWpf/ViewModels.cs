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
    public enum EditorMode { Process, Entity, DataStore, Arrow, DashEntity, CategoryFrame, ConnectableFrame, Database, HorizontalDatabase, Document }

    public class DfdSaveData
    {
        // 旧バージョン互換用：旧JSONではここに直接ノード・接続が保存されている。
        // 新バージョンでは、主に Sheets を使用する。
        public List<NodeData> Nodes { get; set; } = new List<NodeData>();
        public List<ConnectionData> Connections { get; set; } = new List<ConnectionData>();

        // 新機能：Excelのように1ファイル内に複数シートを保存する。
        public List<DiagramSheetData> Sheets { get; set; } = new List<DiagramSheetData>();
        public int ActiveSheetIndex { get; set; } = 0;
    }

    public class DiagramSheetData
    {
        public string Name { get; set; } = "Sheet1";
        public List<NodeData> Nodes { get; set; } = new List<NodeData>();
        public List<ConnectionData> Connections { get; set; } = new List<ConnectionData>();
    }

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
        public bool? IsDashed { get; set; } 
    }

    public class WaypointData
    {
        public double X { get; set; }
        public double Y { get; set; }
        public bool IsJump { get; set; }
    }

    public class ConnectionData
    {
        public Guid SourceId { get; set; }
        public Guid TargetId { get; set; }
        public string Text { get; set; }
        
        public List<Point> Waypoints { get; set; } = new List<Point>(); 
        public List<WaypointData> WaypointNodes { get; set; } = new List<WaypointData>(); 
    }

    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class WaypointViewModel : ViewModelBase
    {
        private double _x, _y;
        private bool _isJump;
        public double X { get => _x; set { _x = value; OnPropertyChanged(); } }
        public double Y { get => _y; set { _y = value; OnPropertyChanged(); } }
        public bool IsJump { get => _isJump; set { _isJump = value; OnPropertyChanged(); } }
    }

    public class NodeViewModel : ViewModelBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        private double _x, _y;
        private double _width = 100;
        private double _height = 50;
        private string _text;
        private string _fileFormat = string.Empty;
        private bool _isSelected, _isEditing, _isDashed, _isFileFormatVisible;

        public EditorMode Type { get; set; }
        
        public double X { get => _x; set { _x = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterX)); } }
        public double Y { get => _y; set { _y = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterY)); } }
        public double Width { get => _width; set { if (value > 0) _width = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterX)); } }
        public double Height { get => _height; set { if (value > 0) _height = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterY)); } }
        public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }
        public string FileFormat { get => _fileFormat; set { _fileFormat = value ?? string.Empty; OnPropertyChanged(); } }
        public bool IsFileFormatVisible { get => _isFileFormatVisible; set { _isFileFormatVisible = value; OnPropertyChanged(); } }
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
        public bool IsEditing { get => _isEditing; set { _isEditing = value; OnPropertyChanged(); } }
        public bool IsDashed { get => _isDashed; set { _isDashed = value; OnPropertyChanged(); } }

        public double CenterX => X + Width / 2; 
        public double CenterY => Y + Height / 2; 
    }

    public class DiagramSheetViewModel : ViewModelBase
    {
        private string _name;
        private bool _isNameEditing;
        private bool _isSearchHit;

        public string Name
        {
            get => _name;
            set
            {
                _name = string.IsNullOrWhiteSpace(value) ? "Sheet" : value;
                OnPropertyChanged();
            }
        }

        public bool IsNameEditing
        {
            get => _isNameEditing;
            set
            {
                if (_isNameEditing == value) return;
                _isNameEditing = value;
                OnPropertyChanged();
            }
        }

        // 検索結果として該当ノードを含む別シートをオレンジ表示するための一時状態。
        // 保存対象にはしない。
        public bool IsSearchHit
        {
            get => _isSearchHit;
            set
            {
                if (_isSearchHit == value) return;
                _isSearchHit = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<NodeViewModel> Nodes { get; } = new ObservableCollection<NodeViewModel>();
        public ObservableCollection<ConnectionViewModel> Connections { get; } = new ObservableCollection<ConnectionViewModel>();

        public DiagramSheetViewModel(string name)
        {
            _name = name;
        }
    }

    public class MainViewModel : ViewModelBase
    {
        public ObservableCollection<DiagramSheetViewModel> Sheets { get; } = new ObservableCollection<DiagramSheetViewModel>();

        private DiagramSheetViewModel _selectedSheet;
        public DiagramSheetViewModel SelectedSheet
        {
            get => _selectedSheet;
            set
            {
                if (_selectedSheet == value) return;
                _selectedSheet = value;
                firstSelectedNode = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Nodes));
                OnPropertyChanged(nameof(Connections));
                OnPropertyChanged(nameof(CanDeleteSheet));
            }
        }

        // 既存XAML・既存処理との互換用。現在選択中のシートの内容を返す。
        public ObservableCollection<NodeViewModel> Nodes => SelectedSheet?.Nodes;
        public ObservableCollection<ConnectionViewModel> Connections => SelectedSheet?.Connections;

        public bool CanDeleteSheet => Sheets.Count > 1;

        public EditorMode CurrentMode { get; set; } = EditorMode.Process;
        private int nodeCount = 1;
        private NodeViewModel firstSelectedNode = null;

        // シンボルコピー用。接続線はコピー対象外で、選択中の1シンボルだけを複製・貼り付けする。
        private NodeData copiedNodeData = null;
        private int copiedNodePasteCount = 1;

        private bool _snapToGrid = true;
        public bool SnapToGrid { get => _snapToGrid; set { _snapToGrid = value; OnPropertyChanged(); } }

        public MainViewModel()
        {
            AddSheet("Sheet1");
        }

        public void AddSheet(string name = null)
        {
            string sheetName = string.IsNullOrWhiteSpace(name) ? GetNextSheetName() : name;
            var sheet = new DiagramSheetViewModel(sheetName);
            Sheets.Add(sheet);
            SelectedSheet = sheet;
            OnPropertyChanged(nameof(CanDeleteSheet));
        }

        public void DeleteCurrentSheet()
        {
            if (Sheets.Count <= 1 || SelectedSheet == null) return;

            int index = Sheets.IndexOf(SelectedSheet);
            Sheets.Remove(SelectedSheet);

            if (index >= Sheets.Count) index = Sheets.Count - 1;
            SelectedSheet = Sheets[index];
            OnPropertyChanged(nameof(CanDeleteSheet));
        }
        public void ClearSheetSearchMarks()
        {
            foreach (var sheet in Sheets)
            {
                sheet.IsSearchHit = false;
            }
        }

        public int MarkSheetsContainingSameNode(NodeViewModel sourceNode)
        {
            ClearSheetSearchMarks();

            if (sourceNode == null) return 0;

            string targetText = NormalizeSearchText(sourceNode.Text);
            int hitSheetCount = 0;

            foreach (var sheet in Sheets)
            {
                // ユーザー要望は「別の該当シートをオレンジでマーク」なので、現在のシートはマークしない。
                if (sheet == SelectedSheet) continue;

                bool found = sheet.Nodes.Any(n =>
                    n.Type == sourceNode.Type &&
                    NormalizeSearchText(n.Text) == targetText);

                sheet.IsSearchHit = found;
                if (found) hitSheetCount++;
            }

            return hitSheetCount;
        }

        private string NormalizeSearchText(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        }


        private string GetNextSheetName()
        {
            int index = 1;
            while (Sheets.Any(s => s.Name == $"Sheet{index}"))
            {
                index++;
            }
            return $"Sheet{index}";
        }

        private string GetUniqueSheetName(string desiredName)
        {
            string baseName = string.IsNullOrWhiteSpace(desiredName) ? GetNextSheetName() : desiredName.Trim();

            if (!Sheets.Any(s => s.Name == baseName))
            {
                return baseName;
            }

            int index = 2;
            string candidate;
            do
            {
                candidate = $"{baseName} ({index})";
                index++;
            }
            while (Sheets.Any(s => s.Name == candidate));

            return candidate;
        }

        public bool CopySelectedNode()
        {
            var selectedNode = Nodes?.FirstOrDefault(n => n.IsSelected);
            if (selectedNode == null) return false;

            copiedNodeData = CreateNodeDataCopy(selectedNode);
            copiedNodePasteCount = 1;
            return true;
        }

        public bool PasteCopiedNode()
        {
            if (SelectedSheet == null || copiedNodeData == null) return false;

            double offset = 20 * copiedNodePasteCount;
            var pastedNode = CreateNodeFromData(copiedNodeData, offset, offset);

            ResetSelection();
            pastedNode.IsSelected = true;
            Nodes.Add(pastedNode);

            copiedNodePasteCount++;
            return true;
        }

        public bool DuplicateSelectedNode()
        {
            var selectedNode = Nodes?.FirstOrDefault(n => n.IsSelected);
            if (selectedNode == null) return false;

            var copiedData = CreateNodeDataCopy(selectedNode);
            var pastedNode = CreateNodeFromData(copiedData, 20, 20);

            ResetSelection();
            pastedNode.IsSelected = true;
            Nodes.Add(pastedNode);

            // 直後にCtrl+Vした場合も、複製元と同じシンボルを続けて貼り付けられるようにする。
            copiedNodeData = copiedData;
            copiedNodePasteCount = 2;
            return true;
        }

        private NodeData CreateNodeDataCopy(NodeViewModel node)
        {
            return new NodeData
            {
                Id = Guid.NewGuid(),
                Type = node.Type,
                X = node.X,
                Y = node.Y,
                Width = node.Width,
                Height = node.Height,
                Text = node.Text,
                FileFormat = node.FileFormat,
                IsFileFormatVisible = node.IsFileFormatVisible,
                IsDashed = node.IsDashed
            };
        }

        private NodeViewModel CreateNodeFromData(NodeData data, double offsetX, double offsetY)
        {
            return new NodeViewModel
            {
                Id = Guid.NewGuid(),
                Type = data.Type,
                X = data.X + offsetX,
                Y = data.Y + offsetY,
                Width = data.Width > 0 ? data.Width : 100,
                Height = data.Height > 0 ? data.Height : 50,
                Text = data.Text,
                FileFormat = data.FileFormat ?? string.Empty,
                IsFileFormatVisible = data.IsFileFormatVisible,
                IsDashed = data.IsDashed ?? (data.Type == EditorMode.CategoryFrame),
                IsSelected = false,
                IsEditing = false
            };
        }

        public void AddNode(EditorMode type, double x, double y)
        {
            if (SelectedSheet == null) AddSheet("Sheet1");

            var node = new NodeViewModel { Type = type, X = x, Y = y, Text = $"要素 {nodeCount++}" };

            if (type == EditorMode.Database)
            {
                node.Width = 120;
                node.Height = 80;
                node.Text = "データベース";
            }
            else if (type == EditorMode.HorizontalDatabase)
            {
                node.Width = 140;
                node.Height = 80;
                node.Text = "横向きDB";
            }
            else if (type == EditorMode.Document)
            {
                node.Width = 120;
                node.Height = 90;
                node.Text = "文書";
                node.FileFormat = ".txt";
                node.IsFileFormatVisible = false;
            }

            Nodes.Add(node);
        }

        public void DeleteSelected()
        {
            if (SelectedSheet == null) return;

            var selectedNode = Nodes.FirstOrDefault(n => n.IsSelected);
            if (selectedNode != null)
            {
                var relatedConnections = Connections.Where(c => c.Source == selectedNode || c.Target == selectedNode).ToList();
                foreach (var conn in relatedConnections) Connections.Remove(conn);
                Nodes.Remove(selectedNode);
            }

            var selectedConnection = Connections.FirstOrDefault(c => c.IsSelected);
            if (selectedConnection != null)
            {
                Connections.Remove(selectedConnection);
            }
        }

        public void ResetSelection() 
        { 
            if (SelectedSheet == null) return;
            foreach (var n in Nodes) n.IsSelected = false;
            foreach (var c in Connections) c.IsSelected = false;
            firstSelectedNode = null; 
        }

        // 現在のシートだけをクリアする。
        public void ClearAll() 
        { 
            if (SelectedSheet == null) return;
            Nodes.Clear(); 
            Connections.Clear(); 
            nodeCount = 1; 
            ResetSelection(); 
        }

        public DfdSaveData GetSaveData()
        {
            var data = new DfdSaveData();
            data.ActiveSheetIndex = SelectedSheet == null ? 0 : Math.Max(0, Sheets.IndexOf(SelectedSheet));

            foreach (var sheet in Sheets)
            {
                data.Sheets.Add(GetSheetSaveData(sheet));
            }

            // 旧バージョン互換用：アクティブシートだけは従来の場所にも保存する。
            if (SelectedSheet != null)
            {
                var activeSheetData = GetSheetSaveData(SelectedSheet);
                data.Nodes = activeSheetData.Nodes;
                data.Connections = activeSheetData.Connections;
            }

            return data;
        }

        private DiagramSheetData GetSheetSaveData(DiagramSheetViewModel sheet)
        {
            var sheetData = new DiagramSheetData { Name = sheet.Name };
            
            foreach (var n in sheet.Nodes) 
            {
                sheetData.Nodes.Add(new NodeData { Id = n.Id, Type = n.Type, X = n.X, Y = n.Y, Width = n.Width, Height = n.Height, Text = n.Text, FileFormat = n.FileFormat, IsFileFormatVisible = n.IsFileFormatVisible, IsDashed = n.IsDashed });
            }
            
            foreach (var c in sheet.Connections) 
            {
                var cData = new ConnectionData { SourceId = c.Source.Id, TargetId = c.Target.Id, Text = c.Text };
                
                foreach (var wp in c.Waypoints) 
                {
                    cData.WaypointNodes.Add(new WaypointData { X = wp.X, Y = wp.Y, IsJump = wp.IsJump });
                }
                
                sheetData.Connections.Add(cData);
            }

            return sheetData;
        }

        public void LoadSaveData(DfdSaveData data)
        {
            Sheets.Clear();
            firstSelectedNode = null;
            nodeCount = 1;

            if (data.Sheets != null && data.Sheets.Any())
            {
                foreach (var sheetData in data.Sheets)
                {
                    var sheet = new DiagramSheetViewModel(string.IsNullOrWhiteSpace(sheetData.Name) ? GetNextSheetName() : sheetData.Name);
                    LoadSheetData(sheet, sheetData.Nodes, sheetData.Connections);
                    Sheets.Add(sheet);
                }

                int index = data.ActiveSheetIndex;
                if (index < 0 || index >= Sheets.Count) index = 0;
                SelectedSheet = Sheets[index];
            }
            else
            {
                // 旧JSON互換：Sheets がない場合は、従来の Nodes / Connections を Sheet1 として読み込む。
                var sheet = new DiagramSheetViewModel("Sheet1");
                LoadSheetData(sheet, data.Nodes, data.Connections);
                Sheets.Add(sheet);
                SelectedSheet = sheet;
            }

            if (Sheets.Count == 0)
            {
                AddSheet("Sheet1");
            }

            OnPropertyChanged(nameof(CanDeleteSheet));
        }

        public int ImportSaveDataAsSheets(DfdSaveData data, string sourceName = null)
        {
            if (data == null) return 0;

            ClearSheetSearchMarks();
            firstSelectedNode = null;

            var importedSheets = new List<DiagramSheetViewModel>();
            string fileBaseName = string.IsNullOrWhiteSpace(sourceName) ? "Imported" : sourceName.Trim();

            if (data.Sheets != null && data.Sheets.Any())
            {
                foreach (var sheetData in data.Sheets)
                {
                    string importedName = string.IsNullOrWhiteSpace(sheetData.Name) ? fileBaseName : $"{fileBaseName} - {sheetData.Name}";
                    var sheet = new DiagramSheetViewModel(GetUniqueSheetName(importedName));
                    LoadSheetData(sheet, sheetData.Nodes, sheetData.Connections);
                    Sheets.Add(sheet);
                    importedSheets.Add(sheet);
                }
            }
            else if ((data.Nodes != null && data.Nodes.Any()) || (data.Connections != null && data.Connections.Any()))
            {
                // 旧JSONを1枚のシートとして取り込む。
                var sheet = new DiagramSheetViewModel(GetUniqueSheetName(fileBaseName));
                LoadSheetData(sheet, data.Nodes, data.Connections);
                Sheets.Add(sheet);
                importedSheets.Add(sheet);
            }

            if (importedSheets.Count > 0)
            {
                SelectedSheet = importedSheets[0];
                OnPropertyChanged(nameof(CanDeleteSheet));
            }

            return importedSheets.Count;
        }

        private void LoadSheetData(DiagramSheetViewModel sheet, List<NodeData> nodeDataList, List<ConnectionData> connectionDataList)
        {
            var dict = new Dictionary<Guid, NodeViewModel>();
            
            foreach (var n in nodeDataList ?? new List<NodeData>()) 
            {
                var node = new NodeViewModel 
                { 
                    Id = n.Id,
                    Type = n.Type,
                    X = n.X,
                    Y = n.Y,
                    Width = n.Width > 0 ? n.Width : 100,
                    Height = n.Height > 0 ? n.Height : 50,
                    Text = n.Text,
                    FileFormat = n.FileFormat ?? string.Empty,
                    IsFileFormatVisible = n.IsFileFormatVisible,
                    IsDashed = n.IsDashed ?? (n.Type == EditorMode.CategoryFrame) 
                };
                sheet.Nodes.Add(node); 
                dict[n.Id] = node; 
                nodeCount++;
            }
            
            foreach (var c in connectionDataList ?? new List<ConnectionData>()) 
            {
                if (dict.TryGetValue(c.SourceId, out var src) && dict.TryGetValue(c.TargetId, out var tgt)) 
                {
                    var conn = new ConnectionViewModel(src, tgt) { Text = c.Text ?? "データフロー" };
                    
                    if (c.WaypointNodes != null && c.WaypointNodes.Any())
                    {
                        foreach (var wp in c.WaypointNodes) 
                        {
                            conn.Waypoints.Add(new WaypointViewModel { X = wp.X, Y = wp.Y, IsJump = wp.IsJump });
                        }
                    }
                    else if (c.Waypoints != null)
                    {
                        foreach (var pt in c.Waypoints) 
                        {
                            conn.Waypoints.Add(new WaypointViewModel { X = pt.X, Y = pt.Y, IsJump = false });
                        }
                    }
                    
                    sheet.Connections.Add(conn);
                }
            }
        }

        public void HandleNodeClick(NodeViewModel clickedNode)
        {
            if (CurrentMode != EditorMode.Arrow || clickedNode.Type == EditorMode.CategoryFrame) return;
            
            if (firstSelectedNode == null) 
            { 
                firstSelectedNode = clickedNode; 
                clickedNode.IsSelected = true; 
            }
            else 
            {
                if (firstSelectedNode != clickedNode) 
                {
                    Connections.Add(new ConnectionViewModel(firstSelectedNode, clickedNode));
                }
                firstSelectedNode.IsSelected = false; 
                firstSelectedNode = null;
            }
        }

        public Rect GetDiagramBounds()
        {
            if (SelectedSheet == null || (!Nodes.Any() && !Connections.Any())) return Rect.Empty;

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var n in Nodes)
            {
                if (n.X < minX) minX = n.X;
                if (n.Y < minY) minY = n.Y;
                if (n.X + n.Width > maxX) maxX = n.X + n.Width;
                if (n.Y + n.Height > maxY) maxY = n.Y + n.Height;
            }

            foreach (var c in Connections)
            {
                foreach (var wp in c.Waypoints)
                {
                    if (wp.X < minX) minX = wp.X;
                    if (wp.Y < minY) minY = wp.Y;
                    if (wp.X + 10 > maxX) maxX = wp.X + 10;
                    if (wp.Y + 10 > maxY) maxY = wp.Y + 10;
                }
            }
            
            if (minX == double.MaxValue) return Rect.Empty;

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
