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
        private void Node_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (((Thumb)sender).DataContext is NodeViewModel node)
            {
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

                ViewModel.ResetSelection();
                node.IsSelected = true;
            }
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
                ViewModel.ResetSelection();
                node.IsSelected = true;
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
            if (sender is MenuItem item && item.Parent is ContextMenu menu && menu.PlacementTarget is FrameworkElement element && element.DataContext is NodeViewModel node)
            {
                ViewModel.SaveUndoState();
                node.IsFileFormatVisible = item.IsChecked;

                if (node.IsFileFormatVisible && string.IsNullOrWhiteSpace(node.FileFormat))
                {
                    node.FileFormat = ".txt";
                }
            }
        }


        private NodeViewModel GetNodeFromContextMenuItem(MenuItem item)
        {
            if (item?.DataContext is NodeViewModel nodeFromDataContext)
            {
                return nodeFromDataContext;
            }

            ItemsControl current = item;
            while (current != null)
            {
                if (current is ContextMenu contextMenu && contextMenu.PlacementTarget is FrameworkElement element && element.DataContext is NodeViewModel node)
                {
                    return node;
                }

                current = ItemsControl.ItemsControlFromItemContainer(current);
            }

            return null;
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
            if (sender is MenuItem item && item.Parent is ContextMenu menu && menu.PlacementTarget is FrameworkElement element && element.DataContext is NodeViewModel node)
            {
                int hitSheetCount = ViewModel.MarkSheetsContainingSameNode(node);

                if (hitSheetCount == 0)
                {
                    MessageBox.Show("他のシートには、同じシンボルと同じ文字列のオブジェクトは見つかりませんでした。", "検索結果");
                }
            }
        }

        private void MenuItem_ClearSearchMarks_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearSheetSearchMarks();
        }

        private void MenuItem_Solid_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Parent is ContextMenu menu && menu.PlacementTarget is Grid grid && grid.DataContext is NodeViewModel node)
            {
                ViewModel.SaveUndoState();
                node.IsDashed = false;
            }
        }

        private void MenuItem_Dashed_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Parent is ContextMenu menu && menu.PlacementTarget is Grid grid && grid.DataContext is NodeViewModel node)
            {
                ViewModel.SaveUndoState();
                node.IsDashed = true;
            }
        }

        private void MenuItem_MakeStickySpeechBubble_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Parent is ContextMenu menu && menu.PlacementTarget is Grid grid && grid.DataContext is NodeViewModel node)
            {
                if (node.Type == EditorMode.StickyNote)
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
            if (sender is MenuItem item && item.Parent is ContextMenu menu && menu.PlacementTarget is Grid grid && grid.DataContext is NodeViewModel node)
            {
                if (node.Type == EditorMode.StickySpeechBubble)
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
