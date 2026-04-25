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
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel { get; set; }
        
        // 操作用の状態変数
        private bool isDragging = false;
        private UIElement selectedElement = null;
        private Point clickPosition;

        // パン（視点移動）用の状態変数
        private bool isPanning = false;
        private Point panStartPoint;
        private double startOffsetX;
        private double startOffsetY;

        // ★追加：グリッドスナップ計算用（ドラッグ開始時の正確な位置を記憶する）
        private double dragRawX;
        private double dragRawY;
        private double resizeRawW;
        private double resizeRawH;

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            this.DataContext = ViewModel;
        }

        private void BtnMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.Tag != null)
            {
                ViewModel.CurrentMode = (EditorMode)Enum.Parse(typeof(EditorMode), btn.Tag.ToString());
                ViewModel.ResetSelection();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.DeleteSelected();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && !ViewModel.Nodes.Any(n => n.IsEditing) && !ViewModel.Connections.Any(c => c.IsEditing))
            {
                ViewModel.DeleteSelected();
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearAll();
            MainScale.ScaleX = 1; 
            MainScale.ScaleY = 1;
            MainTranslate.X = 0; 
            MainTranslate.Y = 0;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog { Filter = "DFD JSON File|*.json" };
            if (sfd.ShowDialog() == true)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                options.Converters.Add(new JsonStringEnumConverter());
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
                    var options = new JsonSerializerOptions();
                    options.Converters.Add(new JsonStringEnumConverter());
                    var data = JsonSerializer.Deserialize<DfdSaveData>(json, options);
                    if (data != null)
                    {
                        ViewModel.LoadSaveData(data); 
                    }
                } 
                catch (Exception ex)
                {
                    MessageBox.Show("読み込みに失敗しました。\n" + ex.Message);
                }
            }
        }

        private void BtnExportImage_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ResetSelection();
            Rect bounds = ViewModel.GetDiagramBounds();
            if (bounds.IsEmpty)
            {
                MessageBox.Show("出力する図形がありません。", "エラー");
                return;
            }
            bounds.Inflate(50, 50);

            var sfd = new SaveFileDialog { Filter = "PNG画像|*.png", DefaultExt = ".png" };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    Transform originalTransform = MainCanvas.RenderTransform;
                    Size originalSize = new Size(MainCanvas.ActualWidth, MainCanvas.ActualHeight);

                    MainCanvas.RenderTransform = new TranslateTransform(-bounds.X, -bounds.Y);
                    MainCanvas.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    MainCanvas.Arrange(new Rect(new Point(0, 0), MainCanvas.DesiredSize));
                    MainCanvas.UpdateLayout();

                    RenderTargetBitmap rtb = new RenderTargetBitmap((int)bounds.Width, (int)bounds.Height, 96d, 96d, PixelFormats.Pbgra32);

                    DrawingVisual bgVisual = new DrawingVisual();
                    using (DrawingContext dc = bgVisual.RenderOpen())
                    {
                        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, bounds.Width, bounds.Height));
                    }
                    
                    rtb.Render(bgVisual);
                    rtb.Render(MainCanvas);

                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));
                    using (var stream = File.Create(sfd.FileName))
                    {
                        encoder.Save(stream);
                    }

                    MainCanvas.RenderTransform = originalTransform;
                    MainCanvas.Measure(originalSize);
                    MainCanvas.Arrange(new Rect(new Point(0, 0), originalSize));
                    MainCanvas.UpdateLayout();
                    
                    MessageBox.Show("画像を保存しました。", "出力完了");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("画像の保存に失敗しました。\n" + ex.Message, "エラー");
                }
            }
        }

        // グリッド（20px）に合わせて数値を丸める計算式
        private double Snap(double value)
        {
            return ViewModel.SnapToGrid ? Math.Round(value / 20) * 20 : value;
        }

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
                            IsDashed = ViewModel.CurrentMode == EditorMode.CategoryFrame 
                        });
                    } 
                    else 
                    {
                        ViewModel.AddNode(ViewModel.CurrentMode, Snap(pos.X - 50), Snap(pos.Y - 25));
                    }
                }
            }
        }

        private void Node_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (((Thumb)sender).DataContext is NodeViewModel node)
            {
                if (e.ClickCount == 2)
                {
                    ViewModel.ResetSelection();
                    node.IsSelected = true;
                    node.IsEditing = true;
                    e.Handled = true;
                    return;
                }

                if (ViewModel.CurrentMode == EditorMode.Arrow)
                {
                    ViewModel.HandleNodeClick(node);
                    e.Handled = true;
                    return;
                }

                ViewModel.ResetSelection();
                node.IsSelected = true;
            }
        }

        // --- ★追加：図形のドラッグ開始時に元の位置を記憶 ---
        private void Node_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (((Thumb)sender).DataContext is NodeViewModel node)
            {
                dragRawX = node.X;
                dragRawY = node.Y;
            }
        }

        // --- ★変更：移動中にスナップ計算を適用 ---
        private void Node_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (ViewModel.CurrentMode != EditorMode.Arrow && ((Thumb)sender).DataContext is NodeViewModel node)
            {
                dragRawX += e.HorizontalChange;
                dragRawY += e.VerticalChange;

                node.X = Snap(dragRawX);
                node.Y = Snap(dragRawY);
            }
        }

        // --- ★追加：サイズ変更開始時に元のサイズを記憶 ---
        private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (((Thumb)sender).DataContext is NodeViewModel node)
            {
                resizeRawW = node.Width;
                resizeRawH = node.Height;
            }
        }

        // --- ★変更：サイズ変更中にスナップ計算を適用 ---
        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is NodeViewModel node)
            {
                resizeRawW += e.HorizontalChange;
                resizeRawH += e.VerticalChange;
                
                double targetW = Snap(resizeRawW);
                double targetH = Snap(resizeRawH);
                
                if (targetW >= 40) node.Width = targetW;
                if (targetH >= 40) node.Height = targetH;
                
                e.Handled = true;
            }
        }

        private void NodeGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is Grid grid && grid.DataContext is NodeViewModel node)
            {
                if (node.Type != EditorMode.CategoryFrame && node.Type != EditorMode.ConnectableFrame)
                {
                    e.Handled = true; 
                }
            }
        }

        private void MenuItem_Solid_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Parent is ContextMenu menu && menu.PlacementTarget is Grid grid && grid.DataContext is NodeViewModel node)
            {
                node.IsDashed = false;
            }
        }

        private void MenuItem_Dashed_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Parent is ContextMenu menu && menu.PlacementTarget is Grid grid && grid.DataContext is NodeViewModel node)
            {
                node.IsDashed = true;
            }
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

                Point p = e.GetPosition(MainCanvas);
                InsertWaypoint(conn, p, true);
                e.Handled = true; 
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
                            c.Waypoints.Remove(wp); 
                            break; 
                        } 
                    }
                    e.Handled = true; 
                    return; 
                }
                
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
                wp.IsJump = !wp.IsJump; 
                e.Handled = true;
            }
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

        private void ConnectionLabel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && ((Grid)sender).DataContext is ConnectionViewModel conn) 
            { 
                conn.IsEditing = true; 
                e.Handled = true; 
            }
        }

        private void TextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) 
        { 
            if (sender is TextBox tb && tb.IsVisible) 
            { 
                tb.Focus(); 
                tb.SelectAll(); 
            } 
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e) 
        {
            // Enter は TextBox の改行入力に使う。
            // 編集終了は Esc または Ctrl+Enter にする。
            bool finishEditing = e.Key == Key.Escape ||
                                 (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control));

            if (finishEditing)
            {
                if (((FrameworkElement)sender).DataContext is NodeViewModel n) n.IsEditing = false;
                if (((FrameworkElement)sender).DataContext is ConnectionViewModel c) c.IsEditing = false;
                MainCanvas.Focus();
                e.Handled = true;
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e) 
        { 
            if (((FrameworkElement)sender).DataContext is NodeViewModel n) n.IsEditing = false; 
            if (((FrameworkElement)sender).DataContext is ConnectionViewModel c) c.IsEditing = false; 
        }
    }
}