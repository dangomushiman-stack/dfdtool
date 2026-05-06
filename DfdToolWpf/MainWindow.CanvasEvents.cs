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
            if (e.OriginalSource is Canvas || (e.OriginalSource is Rectangle bg && bg.Width == 100000))
            {
                ViewModel.ResetSelection();
                
                if (ViewModel.CurrentMode != EditorMode.Arrow)
                {
                    Point pos = e.GetPosition(MainCanvas);
                    
                    if (ViewModel.CurrentMode == EditorMode.CategoryFrame || ViewModel.CurrentMode == EditorMode.ConnectableFrame) 
                    {
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
}
