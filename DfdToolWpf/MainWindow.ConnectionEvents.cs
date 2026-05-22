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
            var conn = GetConnectionFromMenuItem(sender);
            if (conn == null)
            {
                return;
            }

            if (conn.DashStyle == dashStyle)
            {
                return;
            }

            ViewModel.SaveUndoState();
            conn.DashStyle = dashStyle;
            ViewModel.MarkDirty();
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

        private ConnectionViewModel? GetConnectionFromMenuItem(object sender)
        {
            // 色メニューのような入れ子 MenuItem では、sender.Parent が直接 ContextMenu ではなく
            // 親 MenuItem になる。そのため、論理ツリーを親方向へたどって ContextMenu を探す。
            DependencyObject? current = sender as DependencyObject;

            while (current != null)
            {
                if (current is FrameworkElement frameworkElement &&
                    frameworkElement.DataContext is ConnectionViewModel dataContextConnection)
                {
                    return dataContextConnection;
                }

                if (current is ContextMenu contextMenu &&
                    contextMenu.PlacementTarget is FrameworkElement placementTarget &&
                    placementTarget.DataContext is ConnectionViewModel placementConnection)
                {
                    return placementConnection;
                }

                current = LogicalTreeHelper.GetParent(current);
            }

            // 念のためのフォールバック。右クリック時に対象線分を選択状態にしているため、
            // ContextMenu から辿れない場合でも選択中の線分を対象にできる。
            return ViewModel.Connections.FirstOrDefault(c => c.IsSelected);
        }

        private void MenuItem_ConnectionFineDash_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedConnectionDashStyle(sender, ConnectionDashStyle.Fine);
        }

        private void MenuItem_SetConnectionColor_Click(object sender, RoutedEventArgs e)
        {
            var conn = GetConnectionFromMenuItem(sender);
            if (conn == null)
            {
                return;
            }

            if (sender is not MenuItem item || item.Tag is not string color || string.IsNullOrWhiteSpace(color))
            {
                return;
            }

            if (conn.StrokeColor == color)
            {
                return;
            }

            ViewModel.SaveUndoState();
            conn.StrokeColor = color;
            ViewModel.MarkDirty();
        }

        private void SetSelectedConnectionTextVisibility(object sender, bool isVisible)
        {
            var conn = GetConnectionFromMenuItem(sender);
            if (conn == null)
            {
                return;
            }

            if (conn.IsTextVisible == isVisible)
            {
                return;
            }

            ViewModel.SaveUndoState();
            conn.IsTextVisible = isVisible;
            conn.IsEditing = false;
            ViewModel.MarkDirty();
        }


        private void MenuItem_ConnectionToggleTextVisibility_Click(object sender, RoutedEventArgs e)
        {
            var conn = GetConnectionFromMenuItem(sender);
            if (conn == null)
            {
                return;
            }

            SetSelectedConnectionTextVisibility(sender, !conn.IsTextVisible);
        }

        private void MenuItem_ConnectionShowText_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedConnectionTextVisibility(sender, true);
        }

        private void MenuItem_ConnectionHideText_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedConnectionTextVisibility(sender, false);
        }

        private void ConnectionPath_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Shapes.Path path && path.DataContext is ConnectionViewModel conn)
            {
                Point p = e.GetPosition(MainCanvas);

                if (ViewModel.CurrentMode == EditorMode.Arrow)
                {
                    HandleConnectionClickInArrowMode(conn, p);
                    e.Handled = true;
                    return;
                }

                ViewModel.ResetSelection();
                conn.IsSelected = true;
                pendingBranchParentConnection = null;

                if (e.ClickCount == 2)
                {
                    InsertWaypoint(conn, p, false);
                }
                e.Handled = true;
            }
        }

        private void HandleConnectionClickInArrowMode(ConnectionViewModel conn, Point canvasPoint)
        {
            // 注意: ここで先に ResetSelection() してはいけない。
            // 図形→矢印の分岐作成では、図形クリック時に保持した firstSelectedNode を
            // ViewModel.HasPendingArrowSource / CreateConnectionFromPendingNodeToBranch が使う。
            // ResetSelection() は firstSelectedNode もクリアするため、分岐作成前に呼ぶと
            // 「図形から矢印へ」が成立しなくなる。
            if (ViewModel.HasPendingArrowSource)
            {
                ViewModel.CreateConnectionFromPendingNodeToBranch(conn, canvasPoint);
                pendingBranchParentConnection = null;
                return;
            }

            // 図形を先に選んでいない場合は、既存矢印を「分岐元」として選択する。
            // 次に図形をクリックすると、この位置に分岐点を作り、分岐点→図形の矢印を追加する。
            ViewModel.ResetSelection();
            conn.IsSelected = true;
            pendingBranchParentConnection = conn;
            // 分岐点は親線上に置く。クリック位置が線幅の範囲内で多少ずれていても、
            // 親接続線の折れ線上で最も近い点へ補正してから保持する。
            pendingBranchPointPosition = conn.GetNearestPointOnPolyline(canvasPoint);
        }

        private void ConnectionPath_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Shapes.Path path && path.DataContext is ConnectionViewModel conn)
            {
                // 複数選択済みの線分を右クリックした場合は、右クリックだけで選択を1つに絞らない。
                // 範囲選択後に右クリックしてから削除しても、複数選択を維持できるようにする。
                if (!(conn.IsSelected && GetTotalSelectedItemCount() > 1))
                {
                    ViewModel.ResetSelection();
                    conn.IsSelected = true;
                }

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
            linePts.Add(conn.GetStartReferencePoint());
            foreach (var w in conn.Waypoints) linePts.Add(new Point(w.X + 5, w.Y + 5));
            linePts.Add(conn.GetEndReferencePoint());

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

                bool preserveWaypointSelection = wp.IsSelected && GetSelectedMovableItemCount() > 1;
                var ownerConnection = FindConnectionForWaypoint(wp);

                if (!preserveWaypointSelection)
                {
                    ViewModel.ResetSelection();
                    wp.IsSelected = true;
                }

                // 折り曲げ点を動かすときは、所属する接続線も選択状態にしておく。
                // これにより折り曲げ点レイヤーが表示され続け、関係も分かりやすい。
                if (ownerConnection != null)
                {
                    ownerConnection.IsSelected = true;
                }

                waypointDragStartPositions = GetSelectedWaypoints()
                    .ToDictionary(w => w, w => new Point(w.X, w.Y));
                waypointDragAccumulatedX = 0;
                waypointDragAccumulatedY = 0;

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


        private void BranchPoint_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is BranchPointViewModel branchPoint)
            {
                ViewModel.SaveUndoState();

                bool preserveBranchPointSelection = branchPoint.IsSelected &&
                    (ViewModel.BranchPoints?.Count(p => p.IsSelected) ?? 0) > 1;

                if (!preserveBranchPointSelection)
                {
                    ViewModel.ResetSelection();
                    branchPoint.IsSelected = true;
                }

                // 分岐点を動かすときは、親接続線も選択状態にしておく。
                // 分岐点自体は IsSelected で表示されるが、親線も選択表示されると関係が分かりやすい。
                if (branchPoint.ParentConnection != null)
                {
                    branchPoint.ParentConnection.IsSelected = true;
                }

                branchPointDragStartPositions = ViewModel.BranchPoints?
                    .Where(p => p.IsSelected)
                    .ToDictionary(p => p, p => new Point(p.X, p.Y));
                branchPointDragAccumulatedX = 0;
                branchPointDragAccumulatedY = 0;

                branchPointRawX = branchPoint.X;
                branchPointRawY = branchPoint.Y;
                e.Handled = true;
            }
        }

        private void BranchPoint_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is BranchPointViewModel branchPoint)
            {
                if (branchPointDragStartPositions != null &&
                    branchPoint.IsSelected &&
                    branchPointDragStartPositions.Count > 1)
                {
                    branchPointDragAccumulatedX += e.HorizontalChange;
                    branchPointDragAccumulatedY += e.VerticalChange;

                    foreach (var item in branchPointDragStartPositions)
                    {
                        MoveBranchPointToProposedPoint(
                            item.Key,
                            new Point(item.Value.X + branchPointDragAccumulatedX, item.Value.Y + branchPointDragAccumulatedY));
                    }

                    ViewModel.MarkDirty();
                    e.Handled = true;
                    return;
                }

                branchPointRawX += e.HorizontalChange;
                branchPointRawY += e.VerticalChange;

                MoveBranchPointToProposedPoint(branchPoint, new Point(branchPointRawX, branchPointRawY));

                ViewModel.MarkDirty();
                e.Handled = true;
            }
        }

        private void MoveBranchPointToProposedPoint(BranchPointViewModel branchPoint, Point proposed)
        {
            if (branchPoint.ParentConnection != null)
            {
                // 分岐点は親接続線上に吸着させる。
                // グリッドスナップよりも「親線上に存在する」ことを優先するため、
                // ここでは Snap せず、折れ線上の最近傍点へ補正する。
                PolylineProjection projection = branchPoint.ParentConnection.GetNearestProjectionOnPolyline(proposed);
                branchPoint.ApplyProjection(projection);
            }
            else
            {
                branchPoint.X = Snap(proposed.X);
                branchPoint.Y = Snap(proposed.Y);
            }

            // 分岐点を始点に持つ接続線は BranchPointViewModel の PropertyChanged で追従する。
            // 親接続線側も選択ラベル等を最新化できるよう更新しておく。
            branchPoint.ParentConnection?.UpdateGeometry();
        }

        private void ConnectionLabel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Grid grid || grid.DataContext is not ConnectionViewModel conn)
            {
                return;
            }

            // 線分上の文字列をクリックした場合も、対応する線分をクリックしたのと同じ扱いにする。
            // 特に矢印モードでは、文字枠が線分クリックを奪うため、ここで分岐作成処理へ流す。
            if (ViewModel.CurrentMode == EditorMode.Arrow)
            {
                HandleConnectionClickInArrowMode(conn, e.GetPosition(MainCanvas));
                e.Handled = true;
                return;
            }

            if (e.ClickCount == 2) 
            { 
                ViewModel.SaveUndoState();
                conn.IsEditing = true; 
                e.Handled = true; 
            }
        }

        private void ConnectionLabel_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid grid && grid.DataContext is ConnectionViewModel conn)
            {
                // 線分上の文字列を右クリックした場合も、対応する線分を右クリックしたのと同じ扱いにする。
                // ただし複数選択済みの線分なら、右クリックだけで選択を1つに絞らない。
                if (!(conn.IsSelected && GetTotalSelectedItemCount() > 1))
                {
                    ViewModel.ResetSelection();
                    conn.IsSelected = true;
                }

                // ContextMenu は Grid.ContextMenu 側に同じ内容を持たせているので、メニュー操作も線分と同じ処理に流れる。
                e.Handled = false;
            }
        }
    }
}
