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

        private void BtnImportJsonAsSheet_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "DFD JSON File|*.json",
                Multiselect = true,
                Title = "シートとして取り込むJSONファイルを選択"
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
