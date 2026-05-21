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
            var selectedBranchPoints = _rangeSelectionController.CompleteBranchPoints(endPoint, ViewModel.BranchPoints);
            var selectedWaypoints = _rangeSelectionController.CompleteWaypoints(endPoint, ViewModel.Connections);

            RangeSelectionRectangle.Visibility = Visibility.Collapsed;
            MainCanvas.ReleaseMouseCapture();

            ViewModel.ResetSelection();
            foreach (var node in selectedNodes)
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

            foreach (var waypoint in selectedWaypoints)
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
            
            if (ViewModel.CurrentMode != EditorMode.Arrow && ViewModel.CurrentMode != EditorMode.Select)
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
