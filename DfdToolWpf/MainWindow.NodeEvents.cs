using System;
using System.Collections.Generic;
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
        private void Node_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // リサイズハンドル上のクリックは、親ノードの選択・キャンバス扱い処理で止めない。
            // ここで e.Handled=true にすると、内側の ResizeThumb に DragStarted/DragDelta が届かず、
            // 選択状態なのにサイズ変更できない状態になる。
            if (IsOriginalSourceInsideNamedElement(e.OriginalSource as DependencyObject, "ResizeThumb"))
            {
                return;
            }

            if (((Thumb)sender).DataContext is NodeViewModel node)
            {
                Point canvasPoint = e.GetPosition(MainCanvas);

                // 枠をクリックしたときは、実際に押された位置を基準に対象枠を解決する。
                // これにより、枠が重なっている場合でも「枠線・タイトルを押した枠」を優先できる。
                if (IsFrame(node) && ViewModel.CurrentMode != EditorMode.Arrow)
                {
                    NodeViewModel? frameForLeftClick = ResolveFrameForLeftClick(canvasPoint);

                    if (frameForLeftClick != null)
                    {
                        if (e.ClickCount == 2)
                        {
                            ViewModel.SaveUndoState();
                            ViewModel.ResetSelection();
                            frameForLeftClick.IsSelected = true;
                            frameForLeftClick.IsEditing = true;
                            e.Handled = true;
                            return;
                        }

                        SelectOnlyNode(frameForLeftClick);

                        // 枠線・タイトルを押したときは、Thumb 自体の DragStarted/DragDelta に
                        // イベントを渡す必要がある。ここで e.Handled = true にすると、
                        // 枠は選択できても、そのままドラッグ移動できなくなる。
                        //
                        // ただし、ヒットテスト上の sender と解決した枠が違う場合は、
                        // そのまま通すと別の枠がドラッグされる可能性があるため、
                        // そのクリックでは選択だけにして止める。選択後は対象枠が前面に出るので、
                        // 次のドラッグ操作で正しい枠を移動できる。
                        if (ReferenceEquals(frameForLeftClick, node))
                        {
                            return;
                        }

                        e.Handled = true;
                        return;
                    }

                    // 枠線・タイトルではなく、枠のボディだけをクリックした場合はキャンバス扱いにする。
                    // 図形配置モードなら、枠の内側にもそのまま図形を配置できる。
                    if (IsFrameBodyOnlyLeftClick(canvasPoint))
                    {
                        HandleCanvasClick(canvasPoint);
                        e.Handled = true;
                        return;
                    }
                }

                if (e.ClickCount == 2)
                {
                    ViewModel.SaveUndoState();
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

                SelectOnlyNode(node);
            }
        }

        private bool IsFrameBodyCanvasClick(MouseButtonEventArgs e)
        {
            // 矢印モードでは、接続枠を接続対象としてクリックしたいのでキャンバス扱いにしない。
            if (ViewModel.CurrentMode == EditorMode.Arrow)
            {
                return false;
            }

            if (!TryGetNodeThumbFromOriginalSource(e.OriginalSource as DependencyObject, out Thumb? _, out NodeViewModel? node))
            {
                return false;
            }

            if (!IsFrame(node))
            {
                return false;
            }

            return IsFrameBodyOnlyLeftClick(e.GetPosition(MainCanvas));
        }

        private bool TryGetNodeThumbFromOriginalSource(DependencyObject? source, out Thumb? thumb, out NodeViewModel? node)
        {
            DependencyObject? current = source;

            while (current != null)
            {
                if (current is Thumb t && t.DataContext is NodeViewModel n)
                {
                    thumb = t;
                    node = n;
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            thumb = null;
            node = null;
            return false;
        }

        private bool IsOriginalSourceInsideNamedElement(DependencyObject? source, string elementName)
        {
            DependencyObject? current = source;

            while (current != null)
            {
                if (current is FrameworkElement element && element.Name == elementName)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }


        private enum FrameHitArea
        {
            None,
            Border,
            Title,
            Body
        }

        private bool IsFrame(NodeViewModel node)
        {
            return node.Type == EditorMode.CategoryFrame
                || node.Type == EditorMode.ConnectableFrame;
        }

        private FrameHitArea GetFrameHitArea(NodeViewModel frame, Point canvasPoint)
        {
            if (!IsFrame(frame))
            {
                return FrameHitArea.None;
            }

            const double borderHitWidth = 8.0;
            const double titleHitHeight = 28.0;

            double x = canvasPoint.X - frame.X;
            double y = canvasPoint.Y - frame.Y;

            if (x < 0 || y < 0 || x > frame.Width || y > frame.Height)
            {
                return FrameHitArea.None;
            }

            bool onBorder =
                x <= borderHitWidth ||
                y <= borderHitWidth ||
                x >= frame.Width - borderHitWidth ||
                y >= frame.Height - borderHitWidth;

            bool onTitle = y <= titleHitHeight;

            if (onBorder)
            {
                return FrameHitArea.Border;
            }

            if (onTitle)
            {
                return FrameHitArea.Title;
            }

            return FrameHitArea.Body;
        }

        private List<(NodeViewModel Node, FrameHitArea Area, int Index)> GetFramesAt(Point canvasPoint)
        {
            return ViewModel.Nodes
                .Select((node, index) => (Node: node, Area: GetFrameHitArea(node, canvasPoint), Index: index))
                .Where(x => x.Area != FrameHitArea.None)
                .ToList();
        }

        private NodeViewModel? ResolveFrameForLeftClick(Point canvasPoint)
        {
            var frames = GetFramesAt(canvasPoint)
                .Where(x => x.Area == FrameHitArea.Border || x.Area == FrameHitArea.Title)
                .ToList();

            return ResolveFrameByContainmentThenFront(frames);
        }

        private NodeViewModel? ResolveFrameForRightClick(Point canvasPoint)
        {
            var frames = GetFramesAt(canvasPoint);
            return ResolveFrameByContainmentThenFront(frames);
        }

        private NodeViewModel? ResolveFrameByContainmentThenFront(
            List<(NodeViewModel Node, FrameHitArea Area, int Index)> frames)
        {
            if (frames.Count == 0)
            {
                return null;
            }

            // クリック位置にある枠の中に「完全な入れ子関係」がある場合は、
            // 作成順やZ順ではなく、より内側の枠を優先する。
            //
            // 例：
            //   外側枠 A の中に内側枠 B が完全に入っている
            //   → A と B の両方がクリック位置にあっても、B を選ぶ。
            //
            // 一方、枠同士が一部だけ重なっているだけなら Depth は全て 0 になるので、
            // その場合は従来通り Layer → Nodes内の後ろ順で前面の枠を選ぶ。
            var rankedFrames = frames
                .Select(frame =>
                {
                    int containmentDepth = frames.Count(other =>
                        !ReferenceEquals(other.Node, frame.Node) &&
                        ContainsFrame(other.Node, frame.Node));

                    return (frame.Node, frame.Area, frame.Index, ContainmentDepth: containmentDepth);
                })
                .ToList();

            int maxDepth = rankedFrames.Max(x => x.ContainmentDepth);

            IEnumerable<(NodeViewModel Node, FrameHitArea Area, int Index, int ContainmentDepth)> candidates =
                maxDepth > 0
                    ? rankedFrames.Where(x => x.ContainmentDepth == maxDepth)
                    : rankedFrames;

            return candidates
                .OrderByDescending(x => x.Node.Layer)
                .ThenByDescending(x => x.Index)
                .Select(x => x.Node)
                .FirstOrDefault();
        }

        private bool ContainsFrame(NodeViewModel outer, NodeViewModel inner)
        {
            if (!IsFrame(outer) || !IsFrame(inner) || ReferenceEquals(outer, inner))
            {
                return false;
            }

            const double tolerance = 0.5;

            return inner.X >= outer.X - tolerance
                && inner.Y >= outer.Y - tolerance
                && inner.X + inner.Width <= outer.X + outer.Width + tolerance
                && inner.Y + inner.Height <= outer.Y + outer.Height + tolerance;
        }

        private bool IsFrameBodyOnlyLeftClick(Point canvasPoint)
        {
            var frames = GetFramesAt(canvasPoint);

            return frames.Any()
                && frames.All(x => x.Area == FrameHitArea.Body);
        }

        private void SelectOnlyNode(NodeViewModel node)
        {
            ViewModel.ResetSelection();
            node.IsSelected = true;
        }

        // --- ★追加：図形のドラッグ開始時に元の位置を記憶 ---

        private void Node_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (((Thumb)sender).DataContext is NodeViewModel node)
            {
                ViewModel.SaveUndoState();
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
                ViewModel.SaveUndoState();
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
                Point canvasPoint = Mouse.GetPosition(MainCanvas);

                // 右クリックでは、枠のボディ部も枠として扱う。
                // クリック位置にある枠を集め、完全な入れ子関係なら内側の枠を優先し、
                // 単なる重なりなら Layer → Nodes内の後ろ順で前面の枠を選ぶ。
                NodeViewModel? targetNode = IsFrame(node)
                    ? ResolveFrameForRightClick(canvasPoint) ?? node
                    : node;

                SelectOnlyNode(targetNode);

                if (grid.ContextMenu != null)
                {
                    grid.ContextMenu.DataContext = targetNode;
                }
            }
        }

        private void MenuItem_CopySymbol_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CopySelectedNode())
            {
                MessageBox.Show("コピーするシンボルを選択してください。", "シンボルコピー");
            }
        }

        private void MenuItem_PasteSymbol_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.PasteCopiedNode())
            {
                MessageBox.Show("貼り付けるシンボルがコピーされていません。", "シンボルコピー");
            }
        }

        private void MenuItem_DuplicateSymbol_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.DuplicateSelectedNode())
            {
                MessageBox.Show("複製するシンボルを選択してください。", "シンボルコピー");
            }
        }

        private void MenuItem_FileFormatVisible_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                var node = GetNodeFromContextMenuItem(item);
                if (node != null)
                {
                    ViewModel.SaveUndoState();
                    node.IsFileFormatVisible = item.IsChecked;

                    if (node.IsFileFormatVisible && string.IsNullOrWhiteSpace(node.FileFormat))
                    {
                        node.FileFormat = ".txt";
                    }
                }
            }
        }


        private NodeViewModel? GetNodeFromContextMenuItem(MenuItem item)
        {
            // 右クリック時に NodeGrid_ContextMenuOpening で選択したノードを最優先する。
            // 枠が重なっていて PlacementTarget が外側枠になっていても、メニュー操作対象を揃えるため。
            var selectedNode = ViewModel.Nodes?.FirstOrDefault(n => n.IsSelected);
            if (selectedNode != null)
            {
                return selectedNode;
            }

            DependencyObject? current = item;
            while (current != null)
            {
                if (current is ContextMenu contextMenu)
                {
                    if (contextMenu.DataContext is NodeViewModel nodeFromContextMenu)
                    {
                        return nodeFromContextMenu;
                    }

                    if (contextMenu.PlacementTarget is FrameworkElement element && element.DataContext is NodeViewModel nodeFromPlacementTarget)
                    {
                        return nodeFromPlacementTarget;
                    }
                }

                current = LogicalTreeHelper.GetParent(current) ?? TryGetVisualParent(current);
            }

            return null;
        }

        private DependencyObject? TryGetVisualParent(DependencyObject current)
        {
            try
            {
                return VisualTreeHelper.GetParent(current);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private void MenuItem_SetNodeStrokeColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string color)
            {
                var node = GetNodeFromContextMenuItem(item);
                if (node != null)
                {
                    ViewModel.SaveUndoState();
                    node.StrokeColor = color;
                }
            }
        }

        private void MenuItem_SetNodeFillColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string color)
            {
                var node = GetNodeFromContextMenuItem(item);
                if (node != null)
                {
                    ViewModel.SaveUndoState();
                    node.FillColor = color;
                }
            }
        }

        private void MenuItem_SearchSameSymbolText_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                var node = GetNodeFromContextMenuItem(item);
                if (node != null)
                {
                    int hitSheetCount = ViewModel.MarkSheetsContainingSameNode(node);

                    if (hitSheetCount == 0)
                    {
                        MessageBox.Show("他のシートには、同じシンボルと同じ文字列のオブジェクトは見つかりませんでした。", "検索結果");
                    }
                }
            }
        }

        private void MenuItem_ClearSearchMarks_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearSheetSearchMarks();
        }

        private void MenuItem_Solid_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                var node = GetNodeFromContextMenuItem(item);
                if (node != null)
                {
                    ViewModel.SaveUndoState();
                    node.IsDashed = false;
                }
            }
        }

        private void MenuItem_Dashed_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                var node = GetNodeFromContextMenuItem(item);
                if (node != null)
                {
                    ViewModel.SaveUndoState();
                    node.IsDashed = true;
                }
            }
        }

        private void MenuItem_MakeStickySpeechBubble_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                var node = GetNodeFromContextMenuItem(item);
                if (node != null && node.Type == EditorMode.StickyNote)
                {
                    ViewModel.SaveUndoState();
                    node.Type = EditorMode.StickySpeechBubble;
                    if (node.Height < 100) node.Height = 100;
                    node.InitializeTailTargetIfNeeded();
                    node.OnTypeChangedForView();
                }
            }
        }

        private void MenuItem_MakeStickyNote_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                var node = GetNodeFromContextMenuItem(item);
                if (node != null && node.Type == EditorMode.StickySpeechBubble)
                {
                    ViewModel.SaveUndoState();
                    node.Type = EditorMode.StickyNote;
                    node.OnTypeChangedForView();
                }
            }
        }

        private void CalloutTailThumb_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is NodeViewModel node)
            {
                ViewModel.ResetSelection();
                node.IsSelected = true;

                // ここで e.Handled = true にすると、Thumb がドラッグ開始処理を受け取れず、
                // 丸ハンドルを押せても DragDelta が発生しない場合がある。
            }
        }

        private void CalloutTailThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is NodeViewModel node)
            {
                ViewModel.SaveUndoState();
                ViewModel.ResetSelection();
                node.IsSelected = true;

                // SnapToGrid がONのとき、DragDeltaの小さな移動量を丸めて消さないように
                // ドラッグ開始時の実座標から累積して計算する。
                tailRawX = node.TailTargetX;
                tailRawY = node.TailTargetY;
            }
        }

        private void CalloutTailThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is NodeViewModel node)
            {
                tailRawX += e.HorizontalChange;
                tailRawY += e.VerticalChange;

                node.TailTargetX = Snap(tailRawX);
                node.TailTargetY = Snap(tailRawY);
                e.Handled = true;
            }
        }
    }
}
