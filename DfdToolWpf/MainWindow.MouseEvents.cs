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
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            UpdateCurrentPastePointFromMouse(e);
            base.OnPreviewMouseLeftButtonDown(e);
        }

        protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
        {
            UpdateCurrentPastePointFromMouse(e);
            base.OnPreviewMouseRightButtonDown(e);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 1.1 : 1 / 1.1;
            
            if (MainScale.ScaleX * zoomFactor < 0.2 || MainScale.ScaleX * zoomFactor > 5.0) return;
            
            Point mousePos = e.GetPosition(ViewportContainer);
            
            MainTranslate.X = (MainTranslate.X - mousePos.X) * zoomFactor + mousePos.X;
            MainTranslate.Y = (MainTranslate.Y - mousePos.Y) * zoomFactor + mousePos.Y;
            
            MainScale.ScaleX *= zoomFactor;
            MainScale.ScaleY *= zoomFactor;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Canvas || (e.OriginalSource is Rectangle bg && bg.Width == 100000))
            {
                isPanning = true;
                panStartPoint = e.GetPosition(ViewportContainer);
                startOffsetX = MainTranslate.X;
                startOffsetY = MainTranslate.Y;
                ViewportContainer.CaptureMouse();
            }
        }

        // --- ★変更：中継点の移動にもスナップ計算を適用 ---

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (isPanning)
            {
                Point currentPoint = e.GetPosition(ViewportContainer);
                Vector delta = currentPoint - panStartPoint;
                MainTranslate.X = startOffsetX + delta.X;
                MainTranslate.Y = startOffsetY + delta.Y;
            }
            
            if (isDragging && selectedElement is Ellipse el && el.DataContext is WaypointViewModel wp)
            {
                Point pos = e.GetPosition(MainCanvas);
                double rawX = pos.X - clickPosition.X; 
                double rawY = pos.Y - clickPosition.Y;
                
                wp.X = Snap(rawX);
                wp.Y = Snap(rawY);
            }
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e) 
        { 
            if (isPanning) 
            { 
                isPanning = false; 
                ViewportContainer.ReleaseMouseCapture(); 
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) 
        { 
            isDragging = false; 
            selectedElement?.ReleaseMouseCapture(); 
            selectedElement = null; 
        }
    }
}
