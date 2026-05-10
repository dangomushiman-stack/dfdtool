using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DfdToolWpf
{
    public partial class MainWindow
    {
        private void SelectNodeAndCenterInView(NodeViewModel node)
        {
            if (node == null) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                ViewModel.ResetSelection();
                node.IsSelected = true;
                CenterNodeInView(node);
                MainCanvas.Focus();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void CenterNodeInView(NodeViewModel node)
        {
            if (node == null) return;

            double scaleX = Math.Abs(MainScale.ScaleX) < 0.0001 ? 1.0 : MainScale.ScaleX;
            double scaleY = Math.Abs(MainScale.ScaleY) < 0.0001 ? 1.0 : MainScale.ScaleY;

            double viewportWidth = ViewportContainer.ActualWidth > 0 ? ViewportContainer.ActualWidth : ActualWidth;
            double viewportHeight = ViewportContainer.ActualHeight > 0 ? ViewportContainer.ActualHeight : ActualHeight;

            double nodeCenterX = node.X + node.Width / 2.0;
            double nodeCenterY = node.Y + node.Height / 2.0;

            MainTranslate.X = viewportWidth / 2.0 - nodeCenterX * scaleX;
            MainTranslate.Y = viewportHeight / 2.0 - nodeCenterY * scaleY;
        }

        private bool PasteCopiedNodeAtCurrentPosition()
        {
            Point pastePoint = GetCurrentPastePointOnCanvas();
            return ViewModel.PasteCopiedNodeAt(Snap(pastePoint.X), Snap(pastePoint.Y));
        }

        private Point GetCurrentPastePointOnCanvas()
        {
            return hasLastPastePointOnCanvas
                ? lastPastePointOnCanvas
                : GetViewportCenterOnCanvas();
        }

        private void UpdateCurrentPastePointFromMouse(MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source)
            {
                return;
            }

            // メニューやツールバー上のクリックでは貼り付け位置を更新しない。
            // キャンバス、ノード、接続線など MainCanvas 配下で発生したクリックだけを記憶する。
            if (!IsDescendantOf(source, MainCanvas))
            {
                return;
            }

            lastPastePointOnCanvas = e.GetPosition(MainCanvas);
            hasLastPastePointOnCanvas = true;
        }

        private bool IsDescendantOf(DependencyObject source, DependencyObject ancestor)
        {
            DependencyObject? current = source;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor))
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }
    }
}
