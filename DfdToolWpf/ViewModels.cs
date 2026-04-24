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
    public enum EditorMode { Process, Entity, DataStore, Arrow, DashEntity, CategoryFrame, ConnectableFrame }

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
        public double Width { get; set; }
        public double Height { get; set; }
        public string Text { get; set; }
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
        private bool _isSelected, _isEditing, _isDashed;

        public EditorMode Type { get; set; }
        
        public double X { get => _x; set { _x = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterX)); } }
        public double Y { get => _y; set { _y = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterY)); } }
        public double Width { get => _width; set { if (value > 0) _width = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterX)); } }
        public double Height { get => _height; set { if (value > 0) _height = value; OnPropertyChanged(); OnPropertyChanged(nameof(CenterY)); } }
        public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
        public bool IsEditing { get => _isEditing; set { _isEditing = value; OnPropertyChanged(); } }
        public bool IsDashed { get => _isDashed; set { _isDashed = value; OnPropertyChanged(); } }

        public double CenterX => X + Width / 2; 
        public double CenterY => Y + Height / 2; 
    }

    public class ConnectionViewModel : ViewModelBase
    {
        public NodeViewModel Source { get; }
        public NodeViewModel Target { get; }
        public ObservableCollection<WaypointViewModel> Waypoints { get; } = new ObservableCollection<WaypointViewModel>();

        private Geometry _lineGeometry;
        public Geometry LineGeometry { get => _lineGeometry; set { _lineGeometry = value; OnPropertyChanged(); } }

        private PointCollection _arrowPoints = new PointCollection();
        public PointCollection ArrowPoints { get => _arrowPoints; set { _arrowPoints = value; OnPropertyChanged(); } }

        private double _midX, _midY;
        public double MidX { get => _midX; set { _midX = value; OnPropertyChanged(); } }
        public double MidY { get => _midY; set { _midY = value; OnPropertyChanged(); } }

        private string _text = "データフロー";
        public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }

        private bool _isEditing;
        public bool IsEditing { get => _isEditing; set { _isEditing = value; OnPropertyChanged(); } }

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

        public ConnectionViewModel(NodeViewModel source, NodeViewModel target)
        {
            Source = source; 
            Target = target;
            
            Source.PropertyChanged += (s, e) => { if (e.PropertyName == "CenterX" || e.PropertyName == "CenterY" || e.PropertyName == "Width" || e.PropertyName == "Height") UpdateGeometry(); };
            Target.PropertyChanged += (s, e) => { if (e.PropertyName == "CenterX" || e.PropertyName == "CenterY" || e.PropertyName == "Width" || e.PropertyName == "Height") UpdateGeometry(); };
            
            Waypoints.CollectionChanged += (s, e) => {
                if (e.NewItems != null) 
                {
                    foreach (WaypointViewModel item in e.NewItems) 
                        item.PropertyChanged += (s2, e2) => UpdateGeometry();
                }
                UpdateGeometry();
            };
            
            UpdateGeometry();
        }

        public void UpdateGeometry()
        {
            var pts = new List<Point>();
            
            Point nextPt = Waypoints.Count > 0 ? new Point(Waypoints[0].X + 5, Waypoints[0].Y + 5) : new Point(Target.CenterX, Target.CenterY);
            double sourceAngle = Math.Atan2(nextPt.Y - Source.CenterY, nextPt.X - Source.CenterX);
            double sourceDist = GetEdgeDistance(sourceAngle, Source);
            Point startPt = new Point(Source.CenterX + sourceDist * Math.Cos(sourceAngle), Source.CenterY + sourceDist * Math.Sin(sourceAngle));
            pts.Add(startPt);
            
            foreach (var wp in Waypoints) 
            {
                pts.Add(new Point(wp.X + 5, wp.Y + 5));
            }

            Point lastPt = pts[pts.Count - 1]; 
            double angle = Math.Atan2(Target.CenterY - lastPt.Y, Target.CenterX - lastPt.X);
            double reverseAngle = angle + Math.PI;
            double targetDist = GetEdgeDistance(reverseAngle, Target);
            Point endPt = new Point(Target.CenterX + targetDist * Math.Cos(reverseAngle), Target.CenterY + targetDist * Math.Sin(reverseAngle));
            pts.Add(endPt);

            var geo = new PathGeometry();
            var figure = new PathFigure { StartPoint = pts[0], IsClosed = false };

            for (int i = 1; i < pts.Count; i++)
            {
                Point current = pts[i];
                Point prev = pts[i - 1];

                bool isJump = false;
                if (i - 1 < Waypoints.Count) 
                {
                    isJump = Waypoints[i - 1].IsJump;
                }

                if (isJump && i < pts.Count - 1)
                {
                    Point next = pts[i + 1];
                    
                    double maxR = Math.Min((current - prev).Length / 2.1, (next - current).Length / 2.1);
                    double r = Math.Min(12, maxR);

                    if (r > 1) 
                    {
                        Vector vIn = current - prev; vIn.Normalize();
                        Vector vOut = next - current; vOut.Normalize();

                        Point startJump = current - vIn * r;
                        Point endJump = current + vOut * r;

                        figure.Segments.Add(new LineSegment(startJump, true));
                        figure.Segments.Add(new ArcSegment(endJump, new Size(r, r), 0, false, SweepDirection.Clockwise, true));
                        continue;
                    }
                }
                
                figure.Segments.Add(new LineSegment(current, true));
            }

            geo.Figures.Add(figure);
            LineGeometry = geo;

            double arrowSize = 12, arrowHalfWidth = 6, cosA = Math.Cos(angle), sinA = Math.Sin(angle);
            Point p2 = new Point(endPt.X + ((-arrowSize) * cosA - (arrowHalfWidth) * sinA), endPt.Y + ((-arrowSize) * sinA + (arrowHalfWidth) * cosA));
            Point p3 = new Point(endPt.X + ((-arrowSize) * cosA - (-arrowHalfWidth) * sinA), endPt.Y + ((-arrowSize) * sinA + (-arrowHalfWidth) * cosA));
            ArrowPoints = new PointCollection { endPt, p2, p3 };

            Point mid = GetPolylineMidPoint(pts);
            MidX = mid.X - 30; 
            MidY = mid.Y - 10;
        }

        private Point GetPolylineMidPoint(List<Point> points)
        {
            double totalLength = 0;
            for (int i = 0; i < points.Count - 1; i++) totalLength += (points[i+1] - points[i]).Length;
            
            double midLen = totalLength / 2, curr = 0;
            
            for (int i = 0; i < points.Count - 1; i++) 
            {
                double seg = (points[i+1] - points[i]).Length;
                if (curr + seg >= midLen) 
                {
                    double r = (midLen - curr) / seg;
                    return new Point(points[i].X + (points[i+1].X - points[i].X) * r, points[i].Y + (points[i+1].Y - points[i].Y) * r);
                }
                curr += seg;
            }
            return points[points.Count - 1];
        }

        private double GetEdgeDistance(double angle, NodeViewModel target)
        {
            double w = target.Width / 2;
            double h = target.Height / 2;
            
            if (target.Type == EditorMode.Process) 
            {
                double cos = Math.Cos(angle), sin = Math.Sin(angle); 
                return (w * h) / Math.Sqrt(h * h * cos * cos + w * w * sin * sin);
            } 
            else 
            {
                double cos = Math.Abs(Math.Cos(angle)), sin = Math.Abs(Math.Sin(angle));
                double tx = cos > 0.0001 ? w / cos : double.MaxValue; 
                double ty = sin > 0.0001 ? h / sin : double.MaxValue; 
                return Math.Min(tx, ty);
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

        // ★追加：グリッドスナップ機能の状態（初期値はON）
        private bool _snapToGrid = true;
        public bool SnapToGrid { get => _snapToGrid; set { _snapToGrid = value; OnPropertyChanged(); } }

        public void AddNode(EditorMode type, double x, double y)
        {
            Nodes.Add(new NodeViewModel { Type = type, X = x, Y = y, Text = $"要素 {nodeCount++}" });
        }

        public void DeleteSelected()
        {
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
            foreach (var n in Nodes) n.IsSelected = false;
            foreach (var c in Connections) c.IsSelected = false;
            firstSelectedNode = null; 
        }

        public void ClearAll() 
        { 
            Nodes.Clear(); 
            Connections.Clear(); 
            nodeCount = 1; 
            ResetSelection(); 
        }

        public DfdSaveData GetSaveData()
        {
            var data = new DfdSaveData();
            
            foreach (var n in Nodes) 
            {
                data.Nodes.Add(new NodeData { Id = n.Id, Type = n.Type, X = n.X, Y = n.Y, Width = n.Width, Height = n.Height, Text = n.Text, IsDashed = n.IsDashed });
            }
            
            foreach (var c in Connections) 
            {
                var cData = new ConnectionData { SourceId = c.Source.Id, TargetId = c.Target.Id, Text = c.Text };
                
                foreach (var wp in c.Waypoints) 
                {
                    cData.WaypointNodes.Add(new WaypointData { X = wp.X, Y = wp.Y, IsJump = wp.IsJump });
                }
                
                data.Connections.Add(cData);
            }
            
            return data;
        }

        public void LoadSaveData(DfdSaveData data)
        {
            ClearAll();
            var dict = new Dictionary<Guid, NodeViewModel>();
            
            foreach (var n in data.Nodes) 
            {
                var node = new NodeViewModel 
                { 
                    Id = n.Id, Type = n.Type, X = n.X, Y = n.Y, Width = n.Width > 0 ? n.Width : 100, Height = n.Height > 0 ? n.Height : 50, Text = n.Text,
                    IsDashed = n.IsDashed ?? (n.Type == EditorMode.CategoryFrame) 
                };
                Nodes.Add(node); 
                dict[n.Id] = node; 
                nodeCount++;
            }
            
            foreach (var c in data.Connections) 
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
                    
                    Connections.Add(conn);
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
            if (!Nodes.Any() && !Connections.Any()) return Rect.Empty;

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