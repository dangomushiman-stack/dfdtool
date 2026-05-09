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

        // 吹き出し先端ドラッグ用：グリッドスナップONでも細かいDragDeltaを累積する
        private double tailRawX;
        private double tailRawY;

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
                if (!ViewModel.CopySelectedNode())
                {
                    MessageBox.Show("コピーするシンボルを選択してください。", "シンボルコピー");
                }
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
            {
                if (!ViewModel.PasteCopiedNode())
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