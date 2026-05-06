using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace DfdToolWpf
{
    public partial class MainWindow
    {
        private void SetSelectedConnectionDashStyle(object sender, ConnectionDashStyle dashStyle)
        {
            if (sender is MenuItem item && item.Parent is ContextMenu menu && menu.PlacementTarget is FrameworkElement element && element.DataContext is ConnectionViewModel conn)
            {
                ViewModel.SaveUndoState();
                conn.DashStyle = dashStyle;
            }
        }

        private void MenuItem_ConnectionSolid_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedConnectionDashStyle(sender, ConnectionDashStyle.Solid);
        }

        private void MenuItem_ConnectionCoarseDash_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedConnectionDashStyle(sender, ConnectionDashStyle.Coarse);
        }

        private void MenuItem_ConnectionNormalDash_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedConnectionDashStyle(sender, ConnectionDashStyle.Normal);
        }

        private void MenuItem_ConnectionFineDash_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedConnectionDashStyle(sender, ConnectionDashStyle.Fine);
        }

        private void ConnectionPath_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Shapes.Path path && path.DataContext is ConnectionViewModel conn)
            {
                ViewModel.ResetSelection();
                conn.IsSelected = true;

                if (e.ClickCount == 2)
                {
                    Point p = e.GetPosition(MainCanvas);
                    InsertWaypoint(conn, p, false);
                }
                e.Handled = true;
            }
        }

        private void ConnectionPath_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Shapes.Path path && path.DataContext is ConnectionViewModel conn)
            {
                ViewModel.ResetSelection();
                conn.IsSelected = true;

                // 右クリックでは接続線を選択し、ContextMenu で線種を変更できるようにする。
                // 以前の「右クリックでジャンプ中継点追加」は、メニュー操作と競合するためここでは行わない。
                e.Handled = false;
            }
        }

        private void InsertWaypoint(ConnectionViewModel conn, Point p, bool isJump)
        {
            int insertIndex = 0;
            double minDistance = double.MaxValue;
            
            var linePts = new System.Collections.Generic.List<Point>();
            linePts.Add(new Point(conn.Source.CenterX, conn.Source.CenterY));
            foreach (var w in conn.Waypoints) linePts.Add(new Point(w.X + 5, w.Y + 5));
            linePts.Add(new Point(conn.Target.CenterX, conn.Target.CenterY));

            for (int i = 0; i < linePts.Count - 1; i++)
            {
                double dist = DistanceToSegment(p, linePts[i], linePts[i+1]);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    insertIndex = i; 
                }
            }
            
            // ★変更：新しく追加する中継点もスナップさせる
            double rawX = p.X - 5;
            double rawY = p.Y - 5;
            
            ViewModel.SaveUndoState();
            conn.Waypoints.Insert(insertIndex, new WaypointViewModel { X = Snap(rawX), Y = Snap(rawY), IsJump = isJump });
        }

        private double DistanceToSegment(Point p, Point v, Point w)
        {
            double l2 = (v.X - w.X) * (v.X - w.X) + (v.Y - w.Y) * (v.Y - w.Y);
            if (l2 == 0) return (p.X - v.X) * (p.X - v.X) + (p.Y - v.Y) * (p.Y - v.Y);
            double t = Math.Max(0, Math.Min(1, ((p.X - v.X) * (w.X - v.X) + (p.Y - v.Y) * (w.Y - v.Y)) / l2));
            Point proj = new Point(v.X + t * (w.X - v.X), v.Y + t * (w.Y - v.Y));
            return Math.Sqrt((p.X - proj.X) * (p.X - proj.X) + (p.Y - proj.Y) * (p.Y - proj.Y));
        }

        private void Waypoint_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var el = (Ellipse)sender;
            if (el.DataContext is WaypointViewModel wp)
            {
                if (e.ClickCount == 2) 
                { 
                    foreach (var c in ViewModel.Connections) 
                    {
                        if (c.Waypoints.Contains(wp)) 
                        { 
                            ViewModel.SaveUndoState();
                            c.Waypoints.Remove(wp); 
                            break; 
                        } 
                    }
                    e.Handled = true; 
                    return; 
                }
                
                ViewModel.SaveUndoState();
                isDragging = true; 
                selectedElement = el; 
                clickPosition = e.GetPosition(el); 
                el.CaptureMouse(); 
                e.Handled = true;
            }
        }

        private void Waypoint_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Ellipse el && el.DataContext is WaypointViewModel wp)
            {
                ViewModel.SaveUndoState();
                wp.IsJump = !wp.IsJump; 
                e.Handled = true;
            }
        }

        private void ConnectionLabel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && ((Grid)sender).DataContext is ConnectionViewModel conn) 
            { 
                ViewModel.SaveUndoState();
                conn.IsEditing = true; 
                e.Handled = true; 
            }
        }
    }
}
