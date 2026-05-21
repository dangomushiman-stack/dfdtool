using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;

namespace DfdToolWpf
{
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
                    OnPropertyChanged(nameof(BranchPoints));
                    OnPropertyChanged(nameof(CanDeleteSheet));
                }
            }
    
            // 既存XAML・既存処理との互換用。現在選択中のシートの内容を返す。
            public ObservableCollection<NodeViewModel> Nodes => SelectedSheet?.Nodes;
            public ObservableCollection<ConnectionViewModel> Connections => SelectedSheet?.Connections;
            public ObservableCollection<BranchPointViewModel> BranchPoints => SelectedSheet?.BranchPoints;
    
            public bool CanDeleteSheet => Sheets.Count > 1;
    
            public EditorMode CurrentMode { get; set; } = EditorMode.Process;
            private int nodeCount = 1;
            private NodeViewModel firstSelectedNode = null;
    
            // シンボルコピー用。接続線はコピー対象外で、選択中の1シンボルだけを複製・貼り付けする。
            private NodeData copiedNodeData = null;
            private int copiedNodePasteCount = 1;

            public bool HasCopiedNode => copiedNodeData != null;
    
            private bool _snapToGrid = true;
            public bool SnapToGrid { get => _snapToGrid; set { _snapToGrid = value; OnPropertyChanged(); } }

            private bool _isGridVisible = true;
            public bool IsGridVisible
            {
                get => _isGridVisible;
                set
                {
                    if (_isGridVisible == value) return;
                    _isGridVisible = value;
                    OnPropertyChanged();
                }
            }

            private const int MaxUndoHistory = 50;
            private readonly Stack<DfdSaveData> undoStack = new Stack<DfdSaveData>();
            private readonly Stack<DfdSaveData> redoStack = new Stack<DfdSaveData>();
            private bool isRestoringHistory = false;

            public bool CanUndo => undoStack.Count > 0;
            public bool CanRedo => redoStack.Count > 0;

            private bool _isDirty = false;
            public bool IsDirty
            {
                get => _isDirty;
                private set
                {
                    if (_isDirty == value) return;
                    _isDirty = value;
                    OnPropertyChanged();
                }
            }

            public void MarkClean()
            {
                IsDirty = false;
            }

            public void MarkDirty()
            {
                IsDirty = true;
            }

            public void ClearUndoRedoHistory()
            {
                undoStack.Clear();
                redoStack.Clear();
                NotifyUndoRedoChanged();
            }

            public MainViewModel()
            {
                AddSheet("Sheet1", false);
            }

            public void CreateNewDocument()
            {
                Sheets.Clear();
                nodeCount = 1;
                firstSelectedNode = null;
                copiedNodeData = null;
                copiedNodePasteCount = 1;
                CurrentMode = EditorMode.Process;

                AddSheet("Sheet1", false);
                ClearUndoRedoHistory();
                MarkClean();

                OnPropertyChanged(nameof(Nodes));
                OnPropertyChanged(nameof(Connections));
                OnPropertyChanged(nameof(BranchPoints));
                OnPropertyChanged(nameof(CanDeleteSheet));
                OnPropertyChanged(nameof(CurrentMode));
            }

            public void SaveUndoState()
            {
                if (isRestoringHistory) return;
                undoStack.Push(CloneSaveData(GetSaveData()));
                TrimUndoHistory();
                redoStack.Clear();
                MarkDirty();
                NotifyUndoRedoChanged();
            }

            public bool Undo()
            {
                if (!CanUndo) return false;

                redoStack.Push(CloneSaveData(GetSaveData()));
                var previous = undoStack.Pop();
                RestoreSaveData(previous);
                MarkDirty();
                NotifyUndoRedoChanged();
                return true;
            }

            public bool Redo()
            {
                if (!CanRedo) return false;

                undoStack.Push(CloneSaveData(GetSaveData()));
                TrimUndoHistory();
                var next = redoStack.Pop();
                RestoreSaveData(next);
                MarkDirty();
                NotifyUndoRedoChanged();
                return true;
            }

            private void NotifyUndoRedoChanged()
            {
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }

            private void TrimUndoHistory()
            {
                if (undoStack.Count <= MaxUndoHistory) return;

                var items = undoStack.Reverse().Skip(undoStack.Count - MaxUndoHistory).ToList();
                undoStack.Clear();
                foreach (var item in items)
                {
                    undoStack.Push(item);
                }
            }

            private DfdSaveData CloneSaveData(DfdSaveData source)
            {
                var options = new JsonSerializerOptions();
                options.Converters.Add(new JsonStringEnumConverter());
                string json = JsonSerializer.Serialize(source, options);
                return JsonSerializer.Deserialize<DfdSaveData>(json, options) ?? new DfdSaveData();
            }
    
            public void AddSheet(string name = null, bool recordUndo = true)
            {
                if (recordUndo) SaveUndoState();
                string sheetName = string.IsNullOrWhiteSpace(name) ? GetNextSheetName() : name;
                var sheet = new DiagramSheetViewModel(sheetName);
                Sheets.Add(sheet);
                SelectedSheet = sheet;
                OnPropertyChanged(nameof(CanDeleteSheet));
            }
    
            public void DeleteCurrentSheet()
            {
                if (Sheets.Count <= 1 || SelectedSheet == null) return;
                SaveUndoState();
    
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
    
                    bool found = sheet.Nodes.Any(n => IsSameSearchTarget(n, sourceNode, targetText));
    
                    sheet.IsSearchHit = found;
                    if (found) hitSheetCount++;
                }
    
                return hitSheetCount;
            }

            public (DiagramSheetViewModel Sheet, NodeViewModel Node)? FindFirstSameNodeInOtherSheets(NodeViewModel sourceNode)
            {
                if (sourceNode == null) return null;

                string targetText = NormalizeSearchText(sourceNode.Text);

                foreach (var sheet in Sheets)
                {
                    if (sheet == SelectedSheet) continue;

                    var foundNode = sheet.Nodes.FirstOrDefault(n => IsSameSearchTarget(n, sourceNode, targetText));
                    if (foundNode != null)
                    {
                        return (sheet, foundNode);
                    }
                }

                return null;
            }

            private bool IsSameSearchTarget(NodeViewModel node, NodeViewModel sourceNode, string normalizedSourceText)
            {
                return node.Type == sourceNode.Type
                    && NormalizeSearchText(node.Text) == normalizedSourceText;
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
                OnPropertyChanged(nameof(HasCopiedNode));
                return true;
            }
    
            public bool PasteCopiedNode()
            {
                if (copiedNodeData == null) return false;
                double offset = 20 * copiedNodePasteCount;
                return PasteCopiedNodeAt(copiedNodeData.X + offset, copiedNodeData.Y + offset);
            }

            public bool PasteCopiedNodeAt(double targetX, double targetY)
            {
                if (SelectedSheet == null || copiedNodeData == null) return false;
                SaveUndoState();

                double pasteX = targetX;
                double pasteY = targetY;

                // クリック位置がコピー元と完全に同じ場合は、貼り付いたことが見えるように右下へずらす。
                // 連続で貼り付けた場合は、20pxずつ追加でずらす。
                if (IsSameCoordinate(pasteX, copiedNodeData.X) && IsSameCoordinate(pasteY, copiedNodeData.Y))
                {
                    double offset = 20 * copiedNodePasteCount;
                    pasteX += offset;
                    pasteY += offset;
                }

                double offsetX = pasteX - copiedNodeData.X;
                double offsetY = pasteY - copiedNodeData.Y;
                var pastedNode = CreateNodeFromData(copiedNodeData, offsetX, offsetY);
    
                ResetSelection();
                pastedNode.IsSelected = true;
                Nodes.Add(pastedNode);
    
                copiedNodePasteCount++;
                return true;
            }

            private bool IsSameCoordinate(double a, double b)
            {
                return Math.Abs(a - b) < 0.001;
            }
    
            public bool DuplicateSelectedNode()
            {
                var selectedNode = Nodes?.FirstOrDefault(n => n.IsSelected);
                if (selectedNode == null) return false;
                SaveUndoState();
    
                var copiedData = CreateNodeDataCopy(selectedNode);
                var pastedNode = CreateNodeFromData(copiedData, 20, 20);
    
                ResetSelection();
                pastedNode.IsSelected = true;
                Nodes.Add(pastedNode);
    
                // 直後にCtrl+Vした場合も、複製元と同じシンボルを続けて貼り付けられるようにする。
                copiedNodeData = copiedData;
                copiedNodePasteCount = 2;
                OnPropertyChanged(nameof(HasCopiedNode));
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
                    LinkUrl = node.LinkUrl,
                    IsFileFormatVisible = node.IsFileFormatVisible,
                    IsDashed = node.IsDashed,
                    TailTargetX = node.TailTargetX,
                    TailTargetY = node.TailTargetY,
                    StrokeColor = node.StrokeColor,
                    FillColor = node.FillColor,
                    ImageDataBase64 = node.ImageDataBase64
                };
            }
    
            private NodeViewModel CreateNodeFromData(NodeData data, double offsetX, double offsetY)
            {
                var node = new NodeViewModel
                {
                    Id = Guid.NewGuid(),
                    Type = data.Type,
                    X = data.X + offsetX,
                    Y = data.Y + offsetY,
                    Width = data.Width > 0 ? data.Width : 100,
                    Height = data.Height > 0 ? data.Height : 50,
                    Text = data.Text,
                    FileFormat = data.FileFormat ?? string.Empty,
                    LinkUrl = data.LinkUrl ?? string.Empty,
                    IsFileFormatVisible = data.IsFileFormatVisible,
                    IsDashed = data.IsDashed ?? (data.Type == EditorMode.CategoryFrame),
                    TailTargetX = (data.TailTargetX ?? 0) + offsetX,
                    TailTargetY = (data.TailTargetY ?? 0) + offsetY,
                    StrokeColor = string.IsNullOrWhiteSpace(data.StrokeColor) ? GetDefaultStrokeColor(data.Type) : data.StrokeColor,
                    FillColor = string.IsNullOrWhiteSpace(data.FillColor) ? GetDefaultFillColor(data.Type) : data.FillColor,
                    ImageDataBase64 = data.ImageDataBase64 ?? string.Empty,
                    IsSelected = false,
                    IsEditing = false
                };
    
                if (node.Type == EditorMode.StickySpeechBubble) node.InitializeTailTargetIfNeeded();
                return node;
            }
    
            private string GetDefaultStrokeColor(EditorMode type)
            {
                switch (type)
                {
                    case EditorMode.CategoryFrame:
                        return "Gray";
                    case EditorMode.StickyNote:
                    case EditorMode.StickySpeechBubble:
                        return "#D6A600";
                    case EditorMode.ImageNode:
                        return "#888888";
                    default:
                        return "#4A90E2";
                }
            }

            private string GetDefaultFillColor(EditorMode type)
            {
                switch (type)
                {
                    case EditorMode.CategoryFrame:
                    case EditorMode.ConnectableFrame:
                        return "Transparent";
                    case EditorMode.StickyNote:
                    case EditorMode.StickySpeechBubble:
                        return "#FFF4A8";
                    case EditorMode.ImageNode:
                        return "Transparent";
                    default:
                        return "White";
                }
            }

            public void AddNode(EditorMode type, double x, double y)
            {
                SaveUndoState();
                if (SelectedSheet == null) AddSheet("Sheet1", false);
    
                var node = new NodeViewModel { Type = type, X = x, Y = y, Text = $"要素 {nodeCount++}", StrokeColor = GetDefaultStrokeColor(type), FillColor = GetDefaultFillColor(type) };
    
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
                else if (type == EditorMode.StickyNote)
                {
                    node.Width = 180;
                    node.Height = 120;
                    node.Text = "付箋メモ";
                }
                else if (type == EditorMode.StickySpeechBubble)
                {
                    node.Width = 180;
                    node.Height = 120;
                    node.Text = "吹き出し付箋";
                    node.TailTargetX = x + node.Width + 60;
                    node.TailTargetY = y + node.Height + 50;
                    node.RefreshTail();
                }
    
                Nodes.Add(node);
            }

            public void AddImageNode(string imageDataBase64, double x, double y, double width, double height)
            {
                if (string.IsNullOrWhiteSpace(imageDataBase64)) return;
                SaveUndoState();
                if (SelectedSheet == null) AddSheet("Sheet1", false);

                ResetSelection();

                var node = new NodeViewModel
                {
                    Type = EditorMode.ImageNode,
                    X = x,
                    Y = y,
                    Width = width > 0 ? width : 240,
                    Height = height > 0 ? height : 180,
                    Text = string.Empty,
                    StrokeColor = GetDefaultStrokeColor(EditorMode.ImageNode),
                    FillColor = GetDefaultFillColor(EditorMode.ImageNode),
                    ImageDataBase64 = imageDataBase64,
                    IsSelected = true
                };

                Nodes.Add(node);
            }
    
            public void DeleteSelected()
            {
                if (SelectedSheet == null) return;
                if (!Nodes.Any(n => n.IsSelected) &&
                    !Connections.Any(c => c.IsSelected) &&
                    !BranchPoints.Any(b => b.IsSelected) &&
                    !Connections.Any(c => c.Waypoints.Any(w => w.IsSelected))) return;
                SaveUndoState();
    
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

                var selectedBranchPoint = BranchPoints.FirstOrDefault(b => b.IsSelected);
                if (selectedBranchPoint != null)
                {
                    var branchConnections = Connections.Where(c => c.SourceBranchPoint == selectedBranchPoint).ToList();
                    foreach (var conn in branchConnections) Connections.Remove(conn);
                    BranchPoints.Remove(selectedBranchPoint);
                }

                foreach (var conn in Connections.ToList())
                {
                    var selectedWaypoints = conn.Waypoints.Where(w => w.IsSelected).ToList();
                    foreach (var waypoint in selectedWaypoints)
                    {
                        conn.Waypoints.Remove(waypoint);
                    }
                }
            }
    
            public void ResetSelection() 
            { 
                if (SelectedSheet == null) return;
                foreach (var n in Nodes) n.IsSelected = false;
                foreach (var c in Connections)
                {
                    c.IsSelected = false;
                    foreach (var w in c.Waypoints) w.IsSelected = false;
                }
                foreach (var b in BranchPoints) b.IsSelected = false;
                firstSelectedNode = null; 
            }
    
            // 現在のシートだけをクリアする。
            public void ClearAll() 
            { 
                if (SelectedSheet == null) return;
                if (!Nodes.Any() && !Connections.Any() && !BranchPoints.Any()) return;
                SaveUndoState();
                Nodes.Clear(); 
                Connections.Clear();
                BranchPoints.Clear(); 
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
                    sheetData.Nodes.Add(new NodeData { Id = n.Id, Type = n.Type, X = n.X, Y = n.Y, Width = n.Width, Height = n.Height, Text = n.Text, FileFormat = n.FileFormat, LinkUrl = n.LinkUrl, IsFileFormatVisible = n.IsFileFormatVisible, IsDashed = n.IsDashed, TailTargetX = n.TailTargetX, TailTargetY = n.TailTargetY, StrokeColor = n.StrokeColor, FillColor = n.FillColor, ImageDataBase64 = n.ImageDataBase64 });
                }
                
                foreach (var c in sheet.Connections) 
                {
                    var cData = new ConnectionData
                    {
                        Id = c.Id,
                        SourceId = c.SourceBranchPoint == null ? c.Source.Id : Guid.Empty,
                        FromBranchPointId = c.SourceBranchPoint?.Id ?? Guid.Empty,
                        TargetId = c.Target.Id,
                        Text = c.Text,
                        IsTextVisible = c.IsTextVisible,
                        StrokeColor = c.StrokeColor,
                        IsDashed = c.IsDashed,
                        DashStyle = c.DashStyle
                    };
                    
                    foreach (var wp in c.Waypoints) 
                    {
                        cData.WaypointNodes.Add(new WaypointData { X = wp.X, Y = wp.Y, IsJump = wp.IsJump });
                    }
                    
                    sheetData.Connections.Add(cData);
                }

                foreach (var branchPoint in sheet.BranchPoints)
                {
                    sheetData.BranchPoints.Add(new BranchPointData
                    {
                        Id = branchPoint.Id,
                        ParentConnectionId = branchPoint.ParentConnection?.Id ?? Guid.Empty,
                        X = branchPoint.X,
                        Y = branchPoint.Y,
                        SegmentIndex = branchPoint.SegmentIndex,
                        SegmentT = branchPoint.SegmentT
                    });
                }
    
                return sheetData;
            }
    
            public void LoadSaveData(DfdSaveData data)
            {
                SaveUndoState();
                RestoreSaveData(data);
            }

            private void RestoreSaveData(DfdSaveData data)
            {
                isRestoringHistory = true;
                try
                {
                Sheets.Clear();
                firstSelectedNode = null;
                nodeCount = 1;
    
                if (data.Sheets != null && data.Sheets.Any())
                {
                    foreach (var sheetData in data.Sheets)
                    {
                        var sheet = new DiagramSheetViewModel(string.IsNullOrWhiteSpace(sheetData.Name) ? GetNextSheetName() : sheetData.Name);
                        LoadSheetData(sheet, sheetData.Nodes, sheetData.Connections, sheetData.BranchPoints);
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
                    LoadSheetData(sheet, data.Nodes, data.Connections, null);
                    Sheets.Add(sheet);
                    SelectedSheet = sheet;
                }
    
                if (Sheets.Count == 0)
                {
                    AddSheet("Sheet1", false);
                }
    
                OnPropertyChanged(nameof(CanDeleteSheet));
                }
                finally
                {
                    isRestoringHistory = false;
                }
            }
    
            public int ImportSaveDataAsSheets(DfdSaveData data, string sourceName = null)
            {
                if (data == null) return 0;
                SaveUndoState();
    
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
                        LoadSheetData(sheet, sheetData.Nodes, sheetData.Connections, sheetData.BranchPoints);
                        Sheets.Add(sheet);
                        importedSheets.Add(sheet);
                    }
                }
                else if ((data.Nodes != null && data.Nodes.Any()) || (data.Connections != null && data.Connections.Any()))
                {
                    // 旧JSONを1枚のシートとして取り込む。
                    var sheet = new DiagramSheetViewModel(GetUniqueSheetName(fileBaseName));
                    LoadSheetData(sheet, data.Nodes, data.Connections, null);
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
    
            private void LoadSheetData(DiagramSheetViewModel sheet, List<NodeData> nodeDataList, List<ConnectionData> connectionDataList, List<BranchPointData> branchPointDataList = null)
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
                        LinkUrl = n.LinkUrl ?? string.Empty,
                        IsFileFormatVisible = n.IsFileFormatVisible,
                        IsDashed = n.IsDashed ?? (n.Type == EditorMode.CategoryFrame),
                        TailTargetX = n.TailTargetX ?? 0,
                        TailTargetY = n.TailTargetY ?? 0,
                        StrokeColor = string.IsNullOrWhiteSpace(n.StrokeColor) ? GetDefaultStrokeColor(n.Type) : n.StrokeColor,
                        FillColor = string.IsNullOrWhiteSpace(n.FillColor) ? GetDefaultFillColor(n.Type) : n.FillColor,
                        ImageDataBase64 = n.ImageDataBase64 ?? string.Empty
                    };
                    if (node.Type == EditorMode.StickySpeechBubble) node.InitializeTailTargetIfNeeded();
                    sheet.Nodes.Add(node); 
                    dict[n.Id] = node; 
                    nodeCount++;
                }
                
                var connectionDict = new Dictionary<Guid, ConnectionViewModel>();

                var pendingBranchConnections = new List<ConnectionData>();

                foreach (var c in connectionDataList ?? new List<ConnectionData>()) 
                {
                    if (c.FromBranchPointId != Guid.Empty)
                    {
                        pendingBranchConnections.Add(c);
                        continue;
                    }

                    if (dict.TryGetValue(c.SourceId, out var src) && dict.TryGetValue(c.TargetId, out var tgt)) 
                    {
                        var conn = CreateConnectionFromData(c, src, tgt);
                        sheet.Connections.Add(conn);
                        connectionDict[conn.Id] = conn;
                    }
                }

                var branchPointDict = new Dictionary<Guid, BranchPointViewModel>();

                foreach (var branchPointData in branchPointDataList ?? new List<BranchPointData>())
                {
                    connectionDict.TryGetValue(branchPointData.ParentConnectionId, out var parentConnection);

                    var branchPoint = new BranchPointViewModel
                    {
                        Id = branchPointData.Id == Guid.Empty ? Guid.NewGuid() : branchPointData.Id,
                        ParentConnection = parentConnection,
                        X = branchPointData.X,
                        Y = branchPointData.Y
                    };

                    if (parentConnection != null)
                    {
                        if (branchPointData.SegmentIndex.HasValue && branchPointData.SegmentT.HasValue)
                        {
                            branchPoint.SegmentIndex = branchPointData.SegmentIndex.Value;
                            branchPoint.SegmentT = branchPointData.SegmentT.Value;
                            branchPoint.SyncToParentConnection();
                        }
                        else
                        {
                            // 旧保存データは X/Y しか持っていないため、現在の親線へ投影して相対位置を復元する。
                            branchPoint.ApplyProjection(parentConnection.GetNearestProjectionOnPolyline(new Point(branchPoint.X, branchPoint.Y)));
                        }
                    }

                    sheet.BranchPoints.Add(branchPoint);
                    branchPointDict[branchPoint.Id] = branchPoint;
                }

                foreach (var c in pendingBranchConnections)
                {
                    if (branchPointDict.TryGetValue(c.FromBranchPointId, out var branchPoint) &&
                        dict.TryGetValue(c.TargetId, out var tgt))
                    {
                        var conn = CreateConnectionFromData(c, branchPoint, tgt);
                        sheet.Connections.Add(conn);
                        connectionDict[conn.Id] = conn;
                    }
                }
            }
    
            private ConnectionViewModel CreateConnectionFromData(ConnectionData data, NodeViewModel source, NodeViewModel target)
            {
                var conn = new ConnectionViewModel(source, target)
                {
                    Id = data.Id == Guid.Empty ? Guid.NewGuid() : data.Id,
                    Text = data.Text ?? "データフロー",
                    IsTextVisible = data.IsTextVisible ?? true,
                    StrokeColor = string.IsNullOrWhiteSpace(data.StrokeColor) ? "Black" : data.StrokeColor,
                    DashStyle = data.DashStyle ?? (data.IsDashed ? ConnectionDashStyle.Normal : ConnectionDashStyle.Solid)
                };

                LoadWaypoints(conn, data);
                return conn;
            }

            private ConnectionViewModel CreateConnectionFromData(ConnectionData data, BranchPointViewModel sourceBranchPoint, NodeViewModel target)
            {
                var conn = new ConnectionViewModel(sourceBranchPoint, target)
                {
                    Id = data.Id == Guid.Empty ? Guid.NewGuid() : data.Id,
                    Text = data.Text ?? "データフロー",
                    IsTextVisible = data.IsTextVisible ?? true,
                    StrokeColor = string.IsNullOrWhiteSpace(data.StrokeColor) ? "Black" : data.StrokeColor,
                    DashStyle = data.DashStyle ?? (data.IsDashed ? ConnectionDashStyle.Normal : ConnectionDashStyle.Solid)
                };

                LoadWaypoints(conn, data);
                return conn;
            }

            private void LoadWaypoints(ConnectionViewModel conn, ConnectionData data)
            {
                if (data.WaypointNodes != null && data.WaypointNodes.Any())
                {
                    foreach (var wp in data.WaypointNodes)
                    {
                        conn.Waypoints.Add(new WaypointViewModel { X = wp.X, Y = wp.Y, IsJump = wp.IsJump });
                    }
                }
                else if (data.Waypoints != null)
                {
                    foreach (var pt in data.Waypoints)
                    {
                        conn.Waypoints.Add(new WaypointViewModel { X = pt.X, Y = pt.Y, IsJump = false });
                    }
                }
            }

            public void CreateBranchConnection(ConnectionViewModel parentConnection, Point branchPointPosition, NodeViewModel targetNode)
            {
                if (SelectedSheet == null || parentConnection == null || targetNode == null) return;
                if (CurrentMode != EditorMode.Arrow || targetNode.Type == EditorMode.CategoryFrame) return;

                SaveUndoState();

                ResetSelection();

                PolylineProjection projection = parentConnection.GetNearestProjectionOnPolyline(branchPointPosition);

                var branchPoint = new BranchPointViewModel
                {
                    ParentConnection = parentConnection
                };
                branchPoint.ApplyProjection(projection);

                BranchPoints.Add(branchPoint);

                var branchConnection = new ConnectionViewModel(branchPoint, targetNode);
                Connections.Add(branchConnection);

                branchConnection.IsSelected = true;
                firstSelectedNode = null;
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
                        SaveUndoState();
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
