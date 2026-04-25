using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace DfdToolWpf
{
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

            Source.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "CenterX" || e.PropertyName == "CenterY" || e.PropertyName == "Width" || e.PropertyName == "Height")
                    UpdateGeometry();
            };

            Target.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "CenterX" || e.PropertyName == "CenterY" || e.PropertyName == "Width" || e.PropertyName == "Height")
                    UpdateGeometry();
            };

            Waypoints.CollectionChanged += (s, e) =>
            {
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

            Point nextPt = Waypoints.Count > 0
                ? new Point(Waypoints[0].X + 5, Waypoints[0].Y + 5)
                : new Point(Target.CenterX, Target.CenterY);

            Point startPt = GetEdgePoint(Source, nextPt);
            pts.Add(startPt);

            foreach (var wp in Waypoints)
            {
                pts.Add(new Point(wp.X + 5, wp.Y + 5));
            }

            Point lastPt = pts[pts.Count - 1];
            Point endPt = GetEdgePoint(Target, lastPt);
            pts.Add(endPt);

            double angle = GetLastSegmentAngle(pts);

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
                        Vector vIn = current - prev;
                        vIn.Normalize();
                        Vector vOut = next - current;
                        vOut.Normalize();

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

            double arrowSize = 12;
            double arrowHalfWidth = 6;
            double cosA = Math.Cos(angle);
            double sinA = Math.Sin(angle);

            Point p2 = new Point(
                endPt.X + ((-arrowSize) * cosA - (arrowHalfWidth) * sinA),
                endPt.Y + ((-arrowSize) * sinA + (arrowHalfWidth) * cosA));

            Point p3 = new Point(
                endPt.X + ((-arrowSize) * cosA - (-arrowHalfWidth) * sinA),
                endPt.Y + ((-arrowSize) * sinA + (-arrowHalfWidth) * cosA));

            ArrowPoints = new PointCollection { endPt, p2, p3 };

            Point mid = GetPolylineMidPoint(pts);
            MidX = mid.X - 30;
            MidY = mid.Y - 10;
        }

        private double GetLastSegmentAngle(List<Point> points)
        {
            for (int i = points.Count - 1; i > 0; i--)
            {
                Vector v = points[i] - points[i - 1];
                if (v.Length > 0.0001)
                {
                    return Math.Atan2(v.Y, v.X);
                }
            }

            return 0;
        }

        private Point GetEdgePoint(NodeViewModel node, Point towardPoint)
        {
            switch (node.Type)
            {
                case EditorMode.Process:
                    return GetEllipseEdgePoint(node, towardPoint);

                case EditorMode.Database:
                    return GetDatabaseEdgePoint(node, towardPoint);

                default:
                    return GetRectangleEdgePoint(node, towardPoint);
            }
        }

        private Point GetRectangleEdgePoint(NodeViewModel node, Point towardPoint)
        {
            double cx = node.CenterX;
            double cy = node.CenterY;
            double halfW = node.Width / 2.0;
            double halfH = node.Height / 2.0;

            double dx = towardPoint.X - cx;
            double dy = towardPoint.Y - cy;

            if (Math.Abs(dx) < 0.0001 && Math.Abs(dy) < 0.0001)
            {
                return new Point(cx, cy);
            }

            double scaleX = Math.Abs(dx) > 0.0001 ? halfW / Math.Abs(dx) : double.MaxValue;
            double scaleY = Math.Abs(dy) > 0.0001 ? halfH / Math.Abs(dy) : double.MaxValue;
            double scale = Math.Min(scaleX, scaleY);

            return new Point(cx + dx * scale, cy + dy * scale);
        }

        private Point GetEllipseEdgePoint(NodeViewModel node, Point towardPoint)
        {
            double cx = node.CenterX;
            double cy = node.CenterY;
            double rx = node.Width / 2.0;
            double ry = node.Height / 2.0;

            return GetEllipsePoint(cx, cy, rx, ry, towardPoint);
        }

        private Point GetDatabaseEdgePoint(NodeViewModel node, Point towardPoint)
        {
            // MainWindow.xaml の ShapeDatabase は 120x80 の Viewbox 内に、
            // X=10..110、上楕円中心Y=18、下楕円中心Y=62 として描いている。
            // その比率に合わせて接続点を計算する。
            double left = node.X + node.Width * (10.0 / 120.0);
            double right = node.X + node.Width * (110.0 / 120.0);
            double cx = node.X + node.Width * (60.0 / 120.0);

            double topEllipseCy = node.Y + node.Height * (18.0 / 80.0);
            double bottomEllipseCy = node.Y + node.Height * (62.0 / 80.0);
            double cy = (topEllipseCy + bottomEllipseCy) / 2.0;

            double rx = node.Width * (50.0 / 120.0);
            double ry = node.Height * (10.0 / 80.0);

            double dx = towardPoint.X - cx;
            double dy = towardPoint.Y - cy;

            if (Math.Abs(dx) < 0.0001 && Math.Abs(dy) < 0.0001)
            {
                return new Point(cx, cy);
            }

            // 左右方向から接続する場合は、円柱の縦側面に接続する。
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                double x = dx < 0 ? left : right;
                double t = (x - cx) / dx;
                double y = cy + dy * t;

                y = Math.Max(topEllipseCy, Math.Min(bottomEllipseCy, y));
                return new Point(x, y);
            }

            // 上方向から接続する場合は上楕円、下方向から接続する場合は下楕円に接続する。
            if (dy < 0)
            {
                return GetEllipsePoint(cx, topEllipseCy, rx, ry, towardPoint);
            }

            return GetEllipsePoint(cx, bottomEllipseCy, rx, ry, towardPoint);
        }

        private Point GetEllipsePoint(double cx, double cy, double rx, double ry, Point towardPoint)
        {
            double dx = towardPoint.X - cx;
            double dy = towardPoint.Y - cy;

            if (Math.Abs(dx) < 0.0001 && Math.Abs(dy) < 0.0001)
            {
                return new Point(cx, cy);
            }

            double length = Math.Sqrt((dx * dx) / (rx * rx) + (dy * dy) / (ry * ry));

            if (length < 0.0001)
            {
                return new Point(cx, cy);
            }

            return new Point(cx + dx / length, cy + dy / length);
        }

        private Point GetPolylineMidPoint(List<Point> points)
        {
            double totalLength = 0;
            for (int i = 0; i < points.Count - 1; i++)
                totalLength += (points[i + 1] - points[i]).Length;

            double midLen = totalLength / 2;
            double curr = 0;

            for (int i = 0; i < points.Count - 1; i++)
            {
                double seg = (points[i + 1] - points[i]).Length;
                if (curr + seg >= midLen)
                {
                    double r = seg > 0.0001 ? (midLen - curr) / seg : 0;
                    return new Point(
                        points[i].X + (points[i + 1].X - points[i].X) * r,
                        points[i].Y + (points[i + 1].Y - points[i].Y) * r);
                }
                curr += seg;
            }

            return points[points.Count - 1];
        }
    }
}
