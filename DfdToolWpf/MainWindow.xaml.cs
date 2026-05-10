using System;
using System.IO;
using System.Collections.Generic;
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
        private const string InternalSymbolClipboardFormat = "DfdToolWpf.InternalSymbolCopy";

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

        // 吹き出し先端ドラッグ用：グリッドスナップONでも細かいDragDeltaを累積する
        private double tailRawX;
        private double tailRawY;

        // 範囲選択用
        private bool isRangeSelecting = false;
        private Point rangeSelectionStartPoint;

        // 複数選択ノードのグループドラッグ用
        private Dictionary<NodeViewModel, Point>? multiDragStartPositions;
        private double multiDragAccumulatedX;
        private double multiDragAccumulatedY;

        // 画像貼り付け位置用。最後にマウスがあったキャンバス座標を覚えておく。
        // 値がない場合は表示領域の中央に貼り付ける。
        private Point? currentPastePointOnCanvas;

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            this.DataContext = ViewModel;
            ViewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.IsDirty))
                {
                    UpdateWindowTitle();
                }
            };
            Closing += MainWindow_Closing;
            UpdateWindowTitle();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!ConfirmSaveIfDirty())
            {
                e.Cancel = true;
            }
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

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Undo();
        }

        private void BtnRedo_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Redo();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (IsTextEditingNow()) return;

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                ViewModel.Undo();
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
            {
                ViewModel.Redo();
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
            {
                BtnOverwriteSave_Click(sender, e);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.S)
            {
                BtnSave_Click(sender, e);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
            {
                if (ViewModel.CopySelectedNode())
                {
                    MarkInternalSymbolCopiedToClipboard();
                }
                else
                {
                    MessageBox.Show("コピーするシンボルを選択してください。", "シンボルコピー");
                }
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
            {
                if (ShouldPasteCopiedSymbolFirst())
                {
                    ViewModel.PasteCopiedNode();
                }
                else if (Clipboard.ContainsImage())
                {
                    PasteImageFromClipboard();
                }
                else if (!ViewModel.PasteCopiedNode())
                {
                    MessageBox.Show("貼り付けるシンボルがコピーされていません。", "シンボルコピー");
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete)
            {
                ViewModel.DeleteSelected();
                e.Handled = true;
            }
        }

        private bool ShouldPasteCopiedSymbolFirst()
        {
            if (!ViewModel.HasCopiedNode)
            {
                return false;
            }

            try
            {
                // アプリ内で図形コピーした直後は、OSクリップボードに画像が残っていても図形を優先する。
                // その後、外部アプリなどで画像をコピーし直した場合は、このマーカーが消えるため画像貼り付けを優先できる。
                return Clipboard.ContainsData(InternalSymbolClipboardFormat);
            }
            catch
            {
                return false;
            }
        }

        private void MarkInternalSymbolCopiedToClipboard()
        {
            try
            {
                var data = new DataObject();
                data.SetData(InternalSymbolClipboardFormat, "1");
                Clipboard.SetDataObject(data, true);
            }
            catch
            {
                // クリップボードが一時的に他プロセスに使用されている場合でも、
                // アプリ内コピー自体は有効なので何もしない。
            }
        }

        private void UpdateCurrentPastePointFromMouse()
        {
            try
            {
                currentPastePointOnCanvas = Mouse.GetPosition(MainCanvas);
            }
            catch
            {
                // MainCanvas がまだ初期化中などの場合は、貼り付け時に中央へフォールバックする。
            }
        }

        private Point GetCurrentPastePointOnCanvas()
        {
            return currentPastePointOnCanvas ?? GetViewportCenterOnCanvas();
        }

        private bool IsTextEditingNow()
        {
            if (Keyboard.FocusedElement is TextBox) return true;

            bool nodeOrConnectionEditing =
                (ViewModel.Nodes?.Any(n => n.IsEditing) ?? false) ||
                (ViewModel.Connections?.Any(c => c.IsEditing) ?? false);

            bool sheetNameEditing = ViewModel.Sheets.Any(s => s.IsNameEditing);

            return nodeOrConnectionEditing || sheetNameEditing;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearAll();
            MainScale.ScaleX = 1; 
            MainScale.ScaleY = 1;
            MainTranslate.X = 0; 
            MainTranslate.Y = 0;
        }

        private double Snap(double value)
        {
            return ViewModel.SnapToGrid ? Math.Round(value / 20) * 20 : value;
        }
    }
}