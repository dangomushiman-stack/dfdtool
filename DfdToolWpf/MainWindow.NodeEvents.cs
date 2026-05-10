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
using DfdToolWpf.Services;

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

                // Ctrl+クリックは、図形に設定されたURLを既定ブラウザで開く。
                // 通常の選択・文字編集・ドラッグ開始より優先して処理する。
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    NodeViewModel linkTarget = _frameHitTestResolver.IsFrame(node)
                        ? _frameHitTestResolver.ResolveFrameForRightClick(ViewModel.Nodes, canvasPoint) ?? node
                        : node;

                    OpenUrlForNode(linkTarget, showMessageIfMissing: false);
                    e.Handled = true;
                    return;
                }

                // 枠をクリックしたときは、実際に押された位置を基準に対象枠を解決する。
                // これにより、枠が重なっている場合でも「枠線・タイトルを押した枠」を優先できる。
                if (_frameHitTestResolver.IsFrame(node) && ViewModel.CurrentMode != EditorMode.Arrow)
                {
                    NodeViewModel? frameForLeftClick = _frameHitTestResolver.ResolveFrameForLeftClick(ViewModel.Nodes, canvasPoint);

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

                        // 範囲選択などで複数選択されている状態の図形をドラッグ開始する場合は、
                        // ここで単独選択に戻さない。
                        // 単独選択に戻してしまうと Node_DragStarted 時点で選択数が1になり、
                        // 複数図形ドラッグではなく、クリックした1図形だけの移動になってしまう。
                        bool preserveMultiSelectionForDrag = ShouldPreserveMultiSelectionForDrag(frameForLeftClick);
                        if (!preserveMultiSelectionForDrag)
                        {
                            SelectOnlyNode(frameForLeftClick);
                        }

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
                    if (_frameHitTestResolver.IsFrameBodyOnlyLeftClick(ViewModel.Nodes, canvasPoint))
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

                // 範囲選択などで複数選択されている状態の図形をドラッグ開始する場合は、
                // ここで選択を1つに絞らず、そのまま Thumb の DragStarted/DragDelta に渡す。
                if (!ShouldPreserveMultiSelectionForDrag(node))
                {
                    SelectOnlyNode(node);
                }
            }
        }

        private bool ShouldPreserveMultiSelectionForDrag(NodeViewModel node)
        {
            return node.IsSelected && (ViewModel.Nodes?.Count(n => n.IsSelected) ?? 0) > 1;
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

            if (!_frameHitTestResolver.IsFrame(node))
            {
                return false;
            }

            return _frameHitTestResolver.IsFrameBodyOnlyLeftClick(ViewModel.Nodes, e.GetPosition(MainCanvas));
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

                if (node.IsSelected && (ViewModel.Nodes?.Count(n => n.IsSelected) ?? 0) > 1)
                {
                    multiDragStartPositions = ViewModel.Nodes
                        .Where(n => n.IsSelected)
                        .ToDictionary(n => n, n => new Point(n.X, n.Y));
                    multiDragAccumulatedX = 0;
                    multiDragAccumulatedY = 0;
                    return;
                }

                multiDragStartPositions = null;
                dragRawX = node.X;
                dragRawY = node.Y;
            }
        }

        // --- ★変更：移動中にスナップ計算を適用 ---

        private void Node_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (ViewModel.CurrentMode != EditorMode.Arrow && ((Thumb)sender).DataContext is NodeViewModel node)
            {
                if (multiDragStartPositions != null && node.IsSelected && multiDragStartPositions.Count > 1)
                {
                    multiDragAccumulatedX += e.HorizontalChange;
                    multiDragAccumulatedY += e.VerticalChange;

                    foreach (var item in multiDragStartPositions)
                    {
                        item.Key.X = Snap(item.Value.X + multiDragAccumulatedX);
                        item.Key.Y = Snap(item.Value.Y + multiDragAccumulatedY);
                    }

                    e.Handled = true;
                    return;
                }

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
                NodeViewModel? targetNode = _frameHitTestResolver.IsFrame(node)
                    ? _frameHitTestResolver.ResolveFrameForRightClick(ViewModel.Nodes, canvasPoint) ?? node
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
            if (ViewModel.CopySelectedNode())
            {
                MarkInternalSymbolCopiedToClipboard();
            }
            else
            {
                MessageBox.Show("コピーするシンボルを選択してください。", "シンボルコピー");
            }
        }

        private void MenuItem_PasteSymbol_Click(object sender, RoutedEventArgs e)
        {
            if (!PasteCopiedNodeAtCurrentPosition())
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

        private void MenuItem_OpenUrl_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                var node = GetNodeFromContextMenuItem(item);
                if (node != null)
                {
                    OpenUrlForNode(node, showMessageIfMissing: true);
                }
            }
        }

        private void MenuItem_SetUrl_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                var node = GetNodeFromContextMenuItem(item);
                if (node == null) return;

                string? input = ShowUrlInputDialog(node.LinkUrl);
                if (input == null)
                {
                    return;
                }

                string trimmed = input.Trim();
                if (trimmed.Length == 0)
                {
                    ViewModel.SaveUndoState();
                    node.LinkUrl = string.Empty;
                    return;
                }

                string? normalizedUrl = _urlLinkService.NormalizeHttpUrl(trimmed);
                if (normalizedUrl == null)
                {
                    MessageBox.Show("URLが正しくありません。\nhttp:// または https:// で始まるURLを指定してください。", "URLリンク");
                    return;
                }

                ViewModel.SaveUndoState();
                node.LinkUrl = normalizedUrl;
            }
        }

        private void MenuItem_ClearUrl_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                var node = GetNodeFromContextMenuItem(item);
                if (node == null) return;

                if (string.IsNullOrWhiteSpace(node.LinkUrl))
                {
                    return;
                }

                ViewModel.SaveUndoState();
                node.LinkUrl = string.Empty;
            }
        }

        private void OpenUrlForNode(NodeViewModel node, bool showMessageIfMissing)
        {
            string? normalizedUrl = _urlLinkService.NormalizeHttpUrl(node.LinkUrl);
            if (normalizedUrl == null)
            {
                if (showMessageIfMissing)
                {
                    MessageBox.Show("この図形にはURLリンクが設定されていません。", "URLリンク");
                }
                return;
            }

            try
            {
                _urlLinkService.OpenUrl(normalizedUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show("URLを開けませんでした。\n" + ex.Message, "URLリンク");
            }
        }

        private string? ShowUrlInputDialog(string currentUrl)
        {
            var dialog = new Window
            {
                Title = "URLリンクを設定",
                Owner = this,
                Width = 480,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize
            };

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "URL:",
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(label, 0);
            root.Children.Add(label);

            var textBox = new TextBox
            {
                Text = currentUrl ?? string.Empty,
                MinWidth = 430
            };
            Grid.SetRow(textBox, 1);
            root.Children.Add(textBox);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            okButton.Click += (_, _) =>
            {
                dialog.DialogResult = true;
                dialog.Close();
            };

            var cancelButton = new Button
            {
                Content = "キャンセル",
                Width = 90,
                IsCancel = true
            };

            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            dialog.Content = root;
            textBox.SelectAll();
            textBox.Focus();

            return dialog.ShowDialog() == true ? textBox.Text : null;
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
                        return;
                    }

                    var firstHit = ViewModel.FindFirstSameNodeInOtherSheets(node);
                    if (firstHit.HasValue)
                    {
                        ViewModel.SelectedSheet = firstHit.Value.Sheet;
                        SelectNodeAndCenterInView(firstHit.Value.Node);
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
