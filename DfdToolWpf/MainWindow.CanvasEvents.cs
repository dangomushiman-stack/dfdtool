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
                HandleCanvasClick(e.GetPosition(MainCanvas));
                e.Handled = true;
                return;
            }

            if (e.OriginalSource is Canvas || (e.OriginalSource is Rectangle bg && bg.Width == 100000))
            {
                HandleCanvasClick(e.GetPosition(MainCanvas));
                e.Handled = true;
            }
        }

        private void HandleCanvasClick(Point pos)
        {
            ViewModel.ResetSelection();
            
            if (ViewModel.CurrentMode != EditorMode.Arrow)
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
