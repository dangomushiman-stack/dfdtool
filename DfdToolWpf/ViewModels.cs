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
    public enum EditorMode { Process, Entity, DataStore, Arrow, DashEntity, CategoryFrame }

    public class DfdSaveData
    {
        public List<NodeData> Nodes { get; set; } = new List<NodeData>();
        public List<ConnectionData> Connections { get; set; } = new List<ConnectionData>();
    }

    public class NodeData
    {
        public Guid Id { get; set; }
        public EditorMode Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }  // 保存項目
        public double Height { get; set; } // 保存項目
        public string Text { get; set; }
    }

    public class ConnectionData
    {
        public Guid SourceId { get; set; }
        public Guid TargetId { get; set; }
        public string Text { get; set; }
        public List<Point> Waypoints { get; set; } = new List<Point>();
    }

    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class WaypointViewModel : ViewModelBase
    {
        private double _x, _y;
        public double X { get => _x; set { _x = value; OnPropertyChanged(); } }
        public double Y { get => _y; set { _y = value; OnPropertyChanged(); } }
    }

    public class NodeViewModel : ViewModelBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        private double _x, _y;
        private double _width = 100;  // デフォルト値
        private double _height = 50;  // デフォルト値
        private string _text;
        private bool _isSelected, _isEditing;

        public EditorMode Type { get; set; }
        public double X { get => _x; set { _x = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterX)); } }
        public double Y { get => _y; set { _y = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterY)); } }
        
        public double Width 
        { 
            get => _width; 
            set { if (value > 0) _width = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterX)); } 
        }
        public double Height 
        { 
            get => _height; 
            set { if (value > 0) _height = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterY)); } 
        }

        public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
        public bool IsEditing { get => _isEditing; set { _isEditing = value; OnPropertyChanged(); } }

        public double CenterX => X + Width / 2; 
        public double CenterY => Y + Height / 2; 
    }

    public class ConnectionViewModel : ViewModelBase
    {
        public NodeViewModel Source { get; }
        public NodeViewModel Target { get; }
        public ObservableCollection<WaypointViewModel> Waypoints { get; } = new ObservableCollection<WaypointViewModel>();

        private PointCollection _linePoints = new PointCollection();
        public PointCollection LinePoints { get => _linePoints; set { _linePoints = value; OnPropertyChanged(); } }

        private PointCollection _arrowPoints = new PointCollection();
        public PointCollection ArrowPoints { get => _arrowPoints; set { _arrowPoints = value; OnPropertyChanged(); } }

        private double _midX, _midY;
        public double MidX { get => _midX; set { _midX = value; OnPropertyChanged(); } }
        public double MidY { get => _midY; set { _midY = value; OnPropertyChanged(); } }

        private string _text = "データフロー";
        public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }

        private bool _isEditing;
        public bool IsEditing { get => _isEditing; set { _isEditing = value; OnPropertyChanged(); } }

        public ConnectionViewModel(NodeViewModel source, NodeViewModel target)
        {
            Source = source; Target = target;
            Source.PropertyChanged += (s, e) => { if (e.PropertyName == "CenterX" || e.PropertyName == "CenterY") UpdateGeometry(); };
            Target.PropertyChanged += (s, e) => { if (e.PropertyName == "CenterX" || e.PropertyName == "CenterY") UpdateGeometry(); };
            Waypoints.CollectionChanged += (s, e) => {
                if (e.NewItems != null) foreach (WaypointViewModel item in e.NewItems) item.PropertyChanged += (s2, e2) => UpdateGeometry();
                UpdateGeometry();
            };
            UpdateGeometry();
        }

        public void UpdateGeometry()
        {
            var pts = new PointCollection();
            pts.Add(new Point(Source.CenterX, Source.CenterY));
            foreach (var wp in Waypoints) pts.Add(new Point(wp.X + 5, wp.Y + 5));

            double targetX = Target.CenterX, targetY = Target.CenterY;
            Point lastPt = pts[pts.Count - 1];
            double angle = Math.Atan2(targetY - lastPt.Y, targetX - lastPt.X);
            double reverseAngle = angle + Math.PI;

            double dist = GetEdgeDistance(reverseAngle, Target.Type);
            Point p1 = new Point(targetX + dist * Math.Cos(reverseAngle), targetY + dist * Math.Sin(reverseAngle));
            pts.Add(p1);
            LinePoints = pts;

            double arrowSize = 12, arrowHalfWidth = 6, cosA = Math.Cos(angle), sinA = Math.Sin(angle);
            Point p2 = new Point(p1.X + ((-arrowSize) * cosA - (arrowHalfWidth) * sinA), p1.Y + ((-arrowSize) * sinA + (arrowHalfWidth) * cosA));
            Point p3 = new Point(p1.X + ((-arrowSize) * cosA - (-arrowHalfWidth) * sinA), p1.Y + ((-arrowSize) * sinA + (-arrowHalfWidth) * cosA));
            ArrowPoints = new PointCollection { p1, p2, p3 };

            Point mid = GetPolylineMidPoint(pts);
            MidX = mid.X - 30; MidY = mid.Y - 10;
        }

        private Point GetPolylineMidPoint(PointCollection points)
        {
            double totalLength = 0;
            for (int i = 0; i < points.Count - 1; i++) totalLength += (points[i+1] - points[i]).Length;
            double midLen = totalLength / 2, curr = 0;
            for (int i = 0; i < points.Count - 1; i++) {
                double seg = (points[i+1] - points[i]).Length;
                if (curr + seg >= midLen) {
                    double r = (midLen - curr) / seg;
                    return new Point(points[i].X + (points[i+1].X - points[i].X) * r, points[i].Y + (points[i+1].Y - points[i].Y) * r);
                }
                curr += seg;
            }
            return points[points.Count - 1];
        }

        private double GetEdgeDistance(double angle, EditorMode type)
        {
            if (type == EditorMode.CategoryFrame) return 0;
            double w = 50, h = 25;
            if (type == EditorMode.Process) {
                double cos = Math.Cos(angle), sin = Math.Sin(angle); return (w * h) / Math.Sqrt(h * h * cos * cos + w * w * sin * sin);
            } else {
                double cos = Math.Abs(Math.Cos(angle)), sin = Math.Abs(Math.Sin(angle));
                double tx = cos > 0.0001 ? w / cos : double.MaxValue; double ty = sin > 0.0001 ? h / sin : double.MaxValue; return Math.Min(tx, ty);
            }
        }
    }

    public class MainViewModel : ViewModelBase
    {
        public ObservableCollection<NodeViewModel> Nodes { get; } = new ObservableCollection<NodeViewModel>();
        public ObservableCollection<ConnectionViewModel> Connections { get; } = new ObservableCollection<ConnectionViewModel>();
        public EditorMode CurrentMode { get; set; } = EditorMode.Process;
        private int nodeCount = 1;
        private NodeViewModel firstSelectedNode = null;

        public void AddNode(EditorMode type, double x, double y)
            => Nodes.Add(new NodeViewModel { Type = type, X = x, Y = y, Text = $"要素 {nodeCount++}" });

        public void DeleteSelectedNode()
        {
            var selectedNode = Nodes.FirstOrDefault(n => n.IsSelected);
            if (selectedNode != null)
            {
                var relatedConnections = Connections.Where(c => c.Source == selectedNode || c.Target == selectedNode).ToList();
                foreach (var conn in relatedConnections) Connections.Remove(conn);
                Nodes.Remove(selectedNode);
            }
        }

        public void ResetSelection() { if (firstSelectedNode != null) firstSelectedNode.IsSelected = false; firstSelectedNode = null; }
        public void ClearAll() { Nodes.Clear(); Connections.Clear(); nodeCount = 1; ResetSelection(); }

        public DfdSaveData GetSaveData()
        {
            var data = new DfdSaveData();
            foreach (var n in Nodes) 
                data.Nodes.Add(new NodeData { Id = n.Id, Type = n.Type, X = n.X, Y = n.Y, Width = n.Width, Height = n.Height, Text = n.Text });
            foreach (var c in Connections) {
                var cData = new ConnectionData { SourceId = c.Source.Id, TargetId = c.Target.Id, Text = c.Text };
                foreach (var wp in c.Waypoints) cData.Waypoints.Add(new Point(wp.X, wp.Y));
                data.Connections.Add(cData);
            }
            return data;
        }

        public void LoadSaveData(DfdSaveData data)
        {
            ClearAll();
            var dict = new Dictionary<Guid, NodeViewModel>();
            foreach (var n in data.Nodes) {
                // 保存データにWidth/Heightがない場合(0の場合)を考慮して最小値を設定
                var node = new NodeViewModel { 
                    Id = n.Id, Type = n.Type, X = n.X, Y = n.Y, 
                    Width = n.Width > 0 ? n.Width : 100, 
                    Height = n.Height > 0 ? n.Height : 50, 
                    Text = n.Text 
                };
                Nodes.Add(node); dict[n.Id] = node; nodeCount++;
            }
            foreach (var c in data.Connections) {
                if (dict.TryGetValue(c.SourceId, out var src) && dict.TryGetValue(c.TargetId, out var tgt)) {
                    var conn = new ConnectionViewModel(src, tgt) { Text = c.Text ?? "データフロー" };
                    foreach (var pt in c.Waypoints) conn.Waypoints.Add(new WaypointViewModel { X = pt.X, Y = pt.Y });
                    Connections.Add(conn);
                }
            }
        }

        public void HandleNodeClick(NodeViewModel clickedNode)
        {
            if (CurrentMode != EditorMode.Arrow || clickedNode.Type == EditorMode.CategoryFrame) return;
            if (firstSelectedNode == null) { firstSelectedNode = clickedNode; clickedNode.IsSelected = true; }
            else {
                if (firstSelectedNode != clickedNode) Connections.Add(new ConnectionViewModel(firstSelectedNode, clickedNode));
                firstSelectedNode.IsSelected = false; firstSelectedNode = null;
            }
        }
    }
}