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
using DfdToolWpf.Services;

namespace DfdToolWpf
{
    public partial class MainWindow : Window
    {
        private const string InternalSymbolClipboardFormat = "DfdToolWpf.InternalSymbolCopy";

        // 左クリック・右クリックしたキャンバス座標を、貼り付け位置として記憶する。
        private Point lastPastePointOnCanvas;
        private bool hasLastPastePointOnCanvas = false;

        private MainViewModel ViewModel { get; set; }
        private readonly UrlLinkService _urlLinkService = new();
        private readonly FrameHitTestResolver _frameHitTestResolver = new();
        private readonly RangeSelectionController _rangeSelectionController = new();

        // Undo/Redo中は、シート選択変更イベントで表示位置をリセットしない。
        private bool suppressViewportResetOnSheetSelection = false;
        
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

        // 分岐点ドラッグ用：グリッドスナップONでも細かいDragDeltaを累積する
        private double branchPointRawX;
        private double branchPointRawY;

        // 接点編集モードの接点ハンドルドラッグ用
        private Point connectionAnchorRawPoint;

        // 矢印モードで「既存矢印 → 図形」を選んで分岐線を作るための一時状態
        private ConnectionViewModel? pendingBranchParentConnection;
        private Point pendingBranchPointPosition;

        // 複数選択ノードのグループドラッグ用
        private Dictionary<NodeViewModel, Point>? multiDragStartPositions;
        private double multiDragAccumulatedX;
        private double multiDragAccumulatedY;

        // 範囲選択された分岐点のグループドラッグ用
        private Dictionary<BranchPointViewModel, Point>? branchPointDragStartPositions;
        private double branchPointDragAccumulatedX;
        private double branchPointDragAccumulatedY;

        // 範囲選択された線分の折り曲げ点（中継点）のグループドラッグ用
        private Dictionary<WaypointViewModel, Point>? waypointDragStartPositions;
        private double waypointDragAccumulatedX;
        private double waypointDragAccumulatedY;

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
            // 範囲選択モードでクリックした直後に別モードへ切り替えると、
            // MouseCapture や範囲選択中フラグが残り、次のモードのクリックを
            // MainCanvas が奪ってしまうことがある。
            // モード切替時は必ず範囲選択状態を終了してから切り替える。
            CancelRangeSelectionIfNeeded();

            if (sender is FrameworkElement btn && btn.Tag != null)
            {
                ViewModel.CurrentMode = (EditorMode)Enum.Parse(typeof(EditorMode), btn.Tag.ToString());
                pendingBranchParentConnection = null;
                ViewModel.ResetSelection();
                NotifyAllConnectionAnchorHandleVisibilityChanged();
            }
        }

        private void NotifyAllConnectionAnchorHandleVisibilityChanged()
        {
            if (ViewModel.Connections == null) return;

            foreach (var connection in ViewModel.Connections)
            {
                connection.NotifyAnchorHandleVisibilityChanged();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.DeleteSelected();
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            RunHistoryOperationPreservingViewport(() => ViewModel.Undo());
        }

        private void BtnRedo_Click(object sender, RoutedEventArgs e)
        {
            RunHistoryOperationPreservingViewport(() => ViewModel.Redo());
        }

        private void RunHistoryOperationPreservingViewport(Func<bool> historyOperation)
        {
            double scaleX = MainScale.ScaleX;
            double scaleY = MainScale.ScaleY;
            double translateX = MainTranslate.X;
            double translateY = MainTranslate.Y;

            suppressViewportResetOnSheetSelection = true;
            try
            {
                bool changed = historyOperation();
                if (!changed)
                {
                    return;
                }

                RestoreViewport(scaleX, scaleY, translateX, translateY);

                // Undo/Redo による SelectedSheet 復元や TabControl の再描画が後から走っても、
                // 表示位置が中央・原点へ戻らないよう、レイアウト更新後にも再適用する。
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RestoreViewport(scaleX, scaleY, translateX, translateY);
                    MainCanvas.Focus();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            finally
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    suppressViewportResetOnSheetSelection = false;
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
        }

        private void RestoreViewport(double scaleX, double scaleY, double translateX, double translateY)
        {
            MainScale.ScaleX = scaleX;
            MainScale.ScaleY = scaleY;
            MainTranslate.X = translateX;
            MainTranslate.Y = translateY;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (IsTextEditingNow()) return;

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
            {
                BtnNew_Click(sender, e);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                RunHistoryOperationPreservingViewport(() => ViewModel.Undo());
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
            {
                RunHistoryOperationPreservingViewport(() => ViewModel.Redo());
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
                    PasteCopiedNodeAtCurrentPosition();
                }
                else if (Clipboard.ContainsImage())
                {
                    PasteImageFromClipboard();
                }
                else if (!PasteCopiedNodeAtCurrentPosition())
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