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
                    IsDashed = node.IsDashed,
                    TailTargetX = node.TailTargetX,
                    TailTargetY = node.TailTargetY
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
                    IsFileFormatVisible = data.IsFileFormatVisible,
                    IsDashed = data.IsDashed ?? (data.Type == EditorMode.CategoryFrame),
                    TailTargetX = (data.TailTargetX ?? 0) + offsetX,
                    TailTargetY = (data.TailTargetY ?? 0) + offsetY,
                    IsSelected = false,
                    IsEditing = false
                };
    
                if (node.Type == EditorMode.StickySpeechBubble) node.InitializeTailTargetIfNeeded();
                return node;
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
                    sheetData.Nodes.Add(new NodeData { Id = n.Id, Type = n.Type, X = n.X, Y = n.Y, Width = n.Width, Height = n.Height, Text = n.Text, FileFormat = n.FileFormat, IsFileFormatVisible = n.IsFileFormatVisible, IsDashed = n.IsDashed, TailTargetX = n.TailTargetX, TailTargetY = n.TailTargetY });
                }
                
                foreach (var c in sheet.Connections) 
                {
                    var cData = new ConnectionData { SourceId = c.Source.Id, TargetId = c.Target.Id, Text = c.Text, IsDashed = c.IsDashed, DashStyle = c.DashStyle };
                    
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
                        IsDashed = n.IsDashed ?? (n.Type == EditorMode.CategoryFrame),
                        TailTargetX = n.TailTargetX ?? 0,
                        TailTargetY = n.TailTargetY ?? 0
                    };
                    if (node.Type == EditorMode.StickySpeechBubble) node.InitializeTailTargetIfNeeded();
                    sheet.Nodes.Add(node); 
                    dict[n.Id] = node; 
                    nodeCount++;
                }
                
                foreach (var c in connectionDataList ?? new List<ConnectionData>()) 
                {
                    if (dict.TryGetValue(c.SourceId, out var src) && dict.TryGetValue(c.TargetId, out var tgt)) 
                    {
                        var conn = new ConnectionViewModel(src, tgt) { Text = c.Text ?? "データフロー", DashStyle = c.DashStyle ?? (c.IsDashed ? ConnectionDashStyle.Normal : ConnectionDashStyle.Solid) };
                        
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
