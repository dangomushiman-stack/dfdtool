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
        private string? currentFilePath;

        private void BtnOverwriteSave_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentDocument();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveAs();
        }

        private bool SaveCurrentDocument()
        {
            if (string.IsNullOrWhiteSpace(currentFilePath))
            {
                return SaveAs();
            }

            try
            {
                SaveToFile(currentFilePath);
                ViewModel.MarkClean();
                UpdateWindowTitle();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("上書き保存に失敗しました。\n" + ex.Message, "エラー");
                return false;
            }
        }

        private bool SaveAs()
        {
            var sfd = new SaveFileDialog
            {
                Filter = "DFD図ファイル (*.dfdj)|*.dfdj|JSONファイル (*.json)|*.json",
                DefaultExt = ".dfdj",
                AddExtension = true
            };
            if (!string.IsNullOrWhiteSpace(currentFilePath))
            {
                sfd.FileName = System.IO.Path.GetFileName(System.IO.Path.ChangeExtension(currentFilePath, ".dfdj"));
                sfd.InitialDirectory = System.IO.Path.GetDirectoryName(currentFilePath);
            }

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    SaveToFile(sfd.FileName);
                    currentFilePath = sfd.FileName;
                    ViewModel.MarkClean();
                    UpdateWindowTitle();
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("保存に失敗しました。\n" + ex.Message, "エラー");
                    return false;
                }
            }

            return false;
        }

        private void SaveToFile(string fileName)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            options.Converters.Add(new JsonStringEnumConverter());
            string json = JsonSerializer.Serialize(ViewModel.GetSaveData(), options);
            File.WriteAllText(fileName, json);
        }

        private void UpdateWindowTitle()
        {
            string dirtyMark = ViewModel?.IsDirty == true ? "*" : string.Empty;
            Title = string.IsNullOrWhiteSpace(currentFilePath)
                ? $"DFD Tool{dirtyMark}"
                : $"DFD Tool - {System.IO.Path.GetFileName(currentFilePath)}{dirtyMark}";
        }

        private bool ConfirmSaveIfDirty()
        {
            if (ViewModel?.IsDirty != true) return true;

            var result = MessageBox.Show(
                "ファイルに変更があります。保存しますか？",
                "未保存の変更",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Cancel) return false;
            if (result == MessageBoxResult.No) return true;

            return SaveCurrentDocument();
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmSaveIfDirty()) return;

            var ofd = new OpenFileDialog
            {
                Filter = "DFD図ファイル (*.dfdj)|*.dfdj|JSONファイル (*.json)|*.json|すべての対応ファイル (*.dfdj;*.json)|*.dfdj;*.json",
                DefaultExt = ".dfdj"
            };
            if (ofd.ShowDialog() == true)
            {
                LoadFromFile(ofd.FileName);
            }
        }

        public void LoadFromFile(string fileName)
        {
            try
            {
                string json = File.ReadAllText(fileName);
                var options = new JsonSerializerOptions();
                options.Converters.Add(new JsonStringEnumConverter());
                var data = JsonSerializer.Deserialize<DfdSaveData>(json, options);
                if (data != null)
                {
                    ViewModel.LoadSaveData(data);
                    ViewModel.ClearUndoRedoHistory();
                    ViewModel.MarkClean();
                    currentFilePath = fileName;
                    UpdateWindowTitle();

                    MainScale.ScaleX = 1;
                    MainScale.ScaleY = 1;
                    MainTranslate.X = 0;
                    MainTranslate.Y = 0;
                    MainCanvas.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("読み込みに失敗しました。\n" + ex.Message, "エラー");
            }
        }

        private void BtnImportJsonAsSheet_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "DFD図ファイル (*.dfdj)|*.dfdj|JSONファイル (*.json)|*.json|すべての対応ファイル (*.dfdj;*.json)|*.dfdj;*.json",
                Multiselect = true,
                Title = "シートとして取り込むDFD図ファイルを選択"
            };

            if (ofd.ShowDialog() != true) return;

            int importedCount = 0;
            var failedFiles = new System.Collections.Generic.List<string>();

            foreach (string fileName in ofd.FileNames)
            {
                try
                {
                    string json = File.ReadAllText(fileName);
                    var options = new JsonSerializerOptions();
                    options.Converters.Add(new JsonStringEnumConverter());
                    var data = JsonSerializer.Deserialize<DfdSaveData>(json, options);

                    if (data == null)
                    {
                        failedFiles.Add(System.IO.Path.GetFileName(fileName));
                        continue;
                    }

                    importedCount += ViewModel.ImportSaveDataAsSheets(data, System.IO.Path.GetFileNameWithoutExtension(fileName));
                }
                catch
                {
                    failedFiles.Add(System.IO.Path.GetFileName(fileName));
                }
            }

            if (importedCount > 0)
            {
                MainScale.ScaleX = 1;
                MainScale.ScaleY = 1;
                MainTranslate.X = 0;
                MainTranslate.Y = 0;
                MainCanvas.Focus();
            }

            if (failedFiles.Count > 0)
            {
                MessageBox.Show($"{importedCount} 枚のシートを取り込みました。\n取り込めなかったファイル: {string.Join(", ", failedFiles)}", "JSON取込");
            }
            else
            {
                MessageBox.Show($"{importedCount} 枚のシートを取り込みました。", "JSON取込");
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
    }
}
