using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DfdToolWpf
{
    public partial class MainWindow
    {
        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 枠の「体」は、枠ではなくキャンバスをクリックしたものとして扱う。
            // PreviewMouseLeftButtonDown 側で拾えなかった場合の保険として、
            // バブリングしてきたクリックもここで同じ判定に通す。
            if (IsFrameBodyCanvasClick(e))
            {
                Point pos = e.GetPosition(MainCanvas);
                if (ShouldStartRangeSelection())
                {
                    BeginRangeSelection(pos);
                }
                else
                {
                    HandleCanvasClick(pos);
                }
                e.Handled = true;
                return;
            }

            if (e.OriginalSource is Canvas || (e.OriginalSource is Rectangle bg && bg.Width == 100000))
            {
                Point pos = e.GetPosition(MainCanvas);

                if (ShouldStartRangeSelection())
                {
                    BeginRangeSelection(pos);
                    e.Handled = true;
                    return;
                }

                HandleCanvasClick(pos);
                e.Handled = true;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_rangeSelectionController.IsSelecting)
            {
                return;
            }

            UpdateRangeSelectionRectangle(e.GetPosition(MainCanvas));
            e.Handled = true;
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_rangeSelectionController.IsSelecting)
            {
                return;
            }

            CompleteRangeSelection(e.GetPosition(MainCanvas));
            e.Handled = true;
        }

        private bool ShouldStartRangeSelection()
        {
            // 「範囲選択」モードでは通常ドラッグで範囲選択。
            // それ以外のモードでも Shift + ドラッグで一時的に範囲選択できるようにする。
            return ViewModel.CurrentMode == EditorMode.Select || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        }

        private void CancelRangeSelectionIfNeeded()
        {
            if (_rangeSelectionController.IsSelecting)
            {
                _rangeSelectionController.Cancel();
            }

            if (RangeSelectionRectangle != null)
            {
                RangeSelectionRectangle.Visibility = Visibility.Collapsed;
                RangeSelectionRectangle.Width = 0;
                RangeSelectionRectangle.Height = 0;
            }

            if (MainCanvas != null && MainCanvas.IsMouseCaptured)
            {
                MainCanvas.ReleaseMouseCapture();
            }
        }

        private void BeginRangeSelection(Point startPoint)
        {
            _rangeSelectionController.Begin(startPoint);

            Canvas.SetLeft(RangeSelectionRectangle, startPoint.X);
            Canvas.SetTop(RangeSelectionRectangle, startPoint.Y);
            RangeSelectionRectangle.Width = 0;
            RangeSelectionRectangle.Height = 0;
            RangeSelectionRectangle.Visibility = Visibility.Visible;

            MainCanvas.CaptureMouse();
        }

        private void UpdateRangeSelectionRectangle(Point currentPoint)
        {
            Rect rect = _rangeSelectionController.Update(currentPoint);
            Canvas.SetLeft(RangeSelectionRectangle, rect.X);
            Canvas.SetTop(RangeSelectionRectangle, rect.Y);
            RangeSelectionRectangle.Width = rect.Width;
            RangeSelectionRectangle.Height = rect.Height;
        }

        private void CompleteRangeSelection(Point endPoint)
        {
            var selectedNodes = _rangeSelectionController.Complete(endPoint, ViewModel.Nodes);
            var selectedTailTargetNodes = _rangeSelectionController.CompleteCalloutTailTargets(endPoint, ViewModel.Nodes);
            var selectedBranchPoints = _rangeSelectionController.CompleteBranchPoints(endPoint, ViewModel.BranchPoints);
            var selectedWaypoints = _rangeSelectionController.CompleteWaypoints(endPoint, ViewModel.Connections);

            RangeSelectionRectangle.Visibility = Visibility.Collapsed;
            MainCanvas.ReleaseMouseCapture();

            ViewModel.ResetSelection();

            // 通常のノード矩形に加え、吹き出し付箋の差し先が範囲に入った場合も
            // その付箋を選択状態にする。
            var nodesToSelect = selectedNodes
                .Concat(selectedTailTargetNodes)
                .Distinct()
                .ToList();

            foreach (var node in nodesToSelect)
            {
                node.IsSelected = true;
            }

            foreach (var branchPoint in selectedBranchPoints)
            {
                branchPoint.IsSelected = true;
                if (branchPoint.ParentConnection != null)
                {
                    branchPoint.ParentConnection.IsSelected = true;
                }
            }

            // 範囲内に入った折り曲げ点に加えて、選択された端点同士を結ぶ線の折り曲げ点も
            // 自動的に選択する。これにより、複数シンボルをまとめて動かしたときに
            // 矢印の折れ線形状も一緒に平行移動する。
            var waypointsToSelect = selectedWaypoints
                .Concat(GetWaypointsThatShouldMoveWithSelectedGroup(
                    nodesToSelect,
                    selectedBranchPoints))
                .Distinct()
                .ToList();

            foreach (var waypoint in waypointsToSelect)
            {
                waypoint.IsSelected = true;
                var ownerConnection = FindConnectionForWaypoint(waypoint);
                if (ownerConnection != null)
                {
                    ownerConnection.IsSelected = true;
                }
            }
        }

        private void HandleCanvasClick(Point pos)
        {
            ViewModel.ResetSelection();
            
            if (ViewModel.CurrentMode != EditorMode.Arrow && ViewModel.CurrentMode != EditorMode.Select && ViewModel.CurrentMode != EditorMode.AnchorEdit)
            {
                if (ViewModel.CurrentMode == EditorMode.CategoryFrame || ViewModel.CurrentMode == EditorMode.ConnectableFrame) 
                {
                    ViewModel.SaveUndoState();
                    ViewModel.Nodes.Add(new NodeViewModel 
                    { 
                        Type = ViewModel.CurrentMode, 
                        X = Snap(pos.X - 150), 
                        Y = Snap(pos.Y - 100), 
                        Width = 300, 
                        Height = 200, 
                        Text = ViewModel.CurrentMode == EditorMode.CategoryFrame ? "カテゴリ枠" : "システム枠",
                        IsDashed = ViewModel.CurrentMode == EditorMode.CategoryFrame,
                        StrokeColor = ViewModel.CurrentMode == EditorMode.CategoryFrame ? "Gray" : "#4A90E2",
                        FillColor = "Transparent"
                    });
                } 
                else 
                {
                    ViewModel.AddNode(ViewModel.CurrentMode, Snap(pos.X - 50), Snap(pos.Y - 25));
                }
            }
        }
    }
}
