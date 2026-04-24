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
}