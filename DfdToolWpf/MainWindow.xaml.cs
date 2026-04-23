using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace DfdToolWpf
{
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel { get; set; }
        private bool isDragging = false;
        private UIElement selectedElement = null;
        private Point clickPosition;

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            this.DataContext = ViewModel;
        }

        private void BtnMode_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CurrentMode = (EditorMode)Enum.Parse(typeof(EditorMode), (string)((Button)sender).Tag);
            ViewModel.ResetSelection();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e) => ViewModel.DeleteSelectedNode();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && !ViewModel.Nodes.Any(n => n.IsEditing) && !ViewModel.Connections.Any(c => c.IsEditing))
                ViewModel.DeleteSelectedNode();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e) => ViewModel.ClearAll();

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog { Filter = "DFD JSON File|*.json" };
            if (sfd.ShowDialog() == true)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(ViewModel.GetSaveData(), options);
                File.WriteAllText(sfd.FileName, json);
            }
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "DFD JSON File|*.json" };
            if (ofd.ShowDialog() == true)
            {
                try 
                {
                    string json = File.ReadAllText(ofd.FileName);
                    var data = JsonSerializer.Deserialize<DfdSaveData>(json);
                    if (data != null) ViewModel.LoadSaveData(data);
                } 
                catch (Exception ex) { MessageBox.Show("読み込みに失敗しました。\n" + ex.Message); }
            }
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Canvas && ViewModel.CurrentMode != EditorMode.Arrow)
            {
                Point pos = e.GetPosition(MainCanvas);
                if (ViewModel.CurrentMode == EditorMode.CategoryFrame) {
                    var node = new NodeViewModel { Type = EditorMode.CategoryFrame, X = pos.X - 150, Y = pos.Y - 100, Width = 300, Height = 200, Text = "カテゴリ枠" };
                    ViewModel.Nodes.Add(node);
                } else {
                    ViewModel.AddNode(ViewModel.CurrentMode, pos.X - 50, pos.Y - 25);
                }
            }
        }

        private void Node_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (((Thumb)sender).DataContext is NodeViewModel node)
            {
                foreach (var n in ViewModel.Nodes) n.IsSelected = false;
                node.IsSelected = true;
                if (e.ClickCount == 2) { node.IsEditing = true; e.Handled = true; return; }
                if (ViewModel.CurrentMode == EditorMode.Arrow) { ViewModel.HandleNodeClick(node); e.Handled = true; }
            }
        }

        private void Node_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (ViewModel.CurrentMode != EditorMode.Arrow && ((Thumb)sender).DataContext is NodeViewModel node)
            {
                node.X += e.HorizontalChange; node.Y += e.VerticalChange;
            }
        }

        // --- ★追加：リサイズ（サイズ変更）処理 ---
        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is NodeViewModel node)
            {
                // ドラッグした分だけ幅と高さを変更
                double newWidth = node.Width + e.HorizontalChange;
                double newHeight = node.Height + e.VerticalChange;

                // 極端に小さくならないよう最小サイズを制限
                if (newWidth >= 50) node.Width = newWidth;
                if (newHeight >= 50) node.Height = newHeight;

                // 移動イベント（Node_DragDelta）が発火しないよう、ここで処理を止める
                e.Handled = true;
            }
        }

        private void Polyline_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && ((Polyline)sender).DataContext is ConnectionViewModel conn)
            {
                Point p = e.GetPosition(MainCanvas);
                conn.Waypoints.Add(new WaypointViewModel { X = p.X - 5, Y = p.Y - 5 });
                e.Handled = true;
            }
        }

        private void Waypoint_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var el = (Ellipse)sender;
            if (el.DataContext is WaypointViewModel wp)
            {
                if (e.ClickCount == 2) { foreach (var c in ViewModel.Connections) if (c.Waypoints.Contains(wp)) { c.Waypoints.Remove(wp); break; } e.Handled = true; return; }
                isDragging = true; selectedElement = el; clickPosition = e.GetPosition(el); el.CaptureMouse(); e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (isDragging && selectedElement is Ellipse el && el.DataContext is WaypointViewModel wp)
            {
                Point pos = e.GetPosition(MainCanvas);
                wp.X = pos.X - clickPosition.X; wp.Y = pos.Y - clickPosition.Y;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { isDragging = false; selectedElement?.ReleaseMouseCapture(); selectedElement = null; }

        private void ConnectionLabel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && ((Grid)sender).DataContext is ConnectionViewModel conn) { conn.IsEditing = true; e.Handled = true; }
        }

        private void TextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) { if (sender is TextBox tb && tb.IsVisible) { tb.Focus(); tb.SelectAll(); } }
        private void TextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter || e.Key == Key.Escape) { if (((FrameworkElement)sender).DataContext is NodeViewModel n) n.IsEditing = false; if (((FrameworkElement)sender).DataContext is ConnectionViewModel c) c.IsEditing = false; MainCanvas.Focus(); } }
        private void TextBox_LostFocus(object sender, RoutedEventArgs e) { if (((FrameworkElement)sender).DataContext is NodeViewModel n) n.IsEditing = false; if (((FrameworkElement)sender).DataContext is ConnectionViewModel c) c.IsEditing = false; }
    }
}