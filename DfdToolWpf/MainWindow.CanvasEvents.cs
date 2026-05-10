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
            if (!isRangeSelecting)
            {
                return;
            }

            UpdateRangeSelectionRectangle(e.GetPosition(MainCanvas));
            e.Handled = true;
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isRangeSelecting)
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
            isRangeSelecting = true;
            rangeSelectionStartPoint = startPoint;

            Canvas.SetLeft(RangeSelectionRectangle, startPoint.X);
            Canvas.SetTop(RangeSelectionRectangle, startPoint.Y);
            RangeSelectionRectangle.Width = 0;
            RangeSelectionRectangle.Height = 0;
            RangeSelectionRectangle.Visibility = Visibility.Visible;

            MainCanvas.CaptureMouse();
        }

        private void UpdateRangeSelectionRectangle(Point currentPoint)
        {
            Rect rect = CreateNormalizedRect(rangeSelectionStartPoint, currentPoint);
            Canvas.SetLeft(RangeSelectionRectangle, rect.X);
            Canvas.SetTop(RangeSelectionRectangle, rect.Y);
            RangeSelectionRectangle.Width = rect.Width;
            RangeSelectionRectangle.Height = rect.Height;
        }

        private void CompleteRangeSelection(Point endPoint)
        {
            Rect rect = CreateNormalizedRect(rangeSelectionStartPoint, endPoint);

            isRangeSelecting = false;
            RangeSelectionRectangle.Visibility = Visibility.Collapsed;
            MainCanvas.ReleaseMouseCapture();

            ApplyRangeSelection(rect);
        }

        private Rect CreateNormalizedRect(Point p1, Point p2)
        {
            return new Rect(
                Math.Min(p1.X, p2.X),
                Math.Min(p1.Y, p2.Y),
                Math.Abs(p2.X - p1.X),
                Math.Abs(p2.Y - p1.Y));
        }

        private void ApplyRangeSelection(Rect selectionRect)
        {
            // クリック同然の小さな範囲なら、通常のキャンバスクリックと同じく選択解除だけにする。
            if (selectionRect.Width < 4 && selectionRect.Height < 4)
            {
                ViewModel.ResetSelection();
                return;
            }

            ViewModel.ResetSelection();

            if (ViewModel.Nodes == null)
            {
                return;
            }

            foreach (var node in ViewModel.Nodes)
            {
                Rect nodeRect = new Rect(node.X, node.Y, node.Width, node.Height);
                if (selectionRect.IntersectsWith(nodeRect))
                {
                    node.IsSelected = true;
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
