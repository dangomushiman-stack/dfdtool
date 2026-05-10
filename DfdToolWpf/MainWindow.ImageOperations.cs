using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace DfdToolWpf
{
    public partial class MainWindow
    {
        private void MenuItem_PasteImage_Click(object sender, RoutedEventArgs e)
        {
            if (!PasteImageFromClipboard())
            {
                MessageBox.Show("クリップボードに画像がありません。", "画像貼り付け");
            }
        }

        private void MenuItem_InsertImageFile_Click(object sender, RoutedEventArgs e)
        {
            InsertImageFromFile();
        }

        private bool PasteImageFromClipboard()
        {
            if (!Clipboard.ContainsImage())
            {
                return false;
            }

            BitmapSource? image = Clipboard.GetImage();
            if (image == null)
            {
                return false;
            }

            byte[] bytes = EncodeBitmapSourceAsPng(image);
            AddImageNodeFromBytes(bytes, image.PixelWidth, image.PixelHeight);
            return true;
        }

        private void InsertImageFromFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "画像ファイルを挿入",
                Filter = "画像ファイル (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|すべてのファイル (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(dialog.FileName);
                var size = GetImagePixelSize(bytes);
                AddImageNodeFromBytes(bytes, size.Width, size.Height);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"画像を読み込めませんでした。\n{ex.Message}", "画像ファイルを挿入", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddImageNodeFromBytes(byte[] bytes, double pixelWidth, double pixelHeight)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }

            var size = GetDisplayImageSize(pixelWidth, pixelHeight);
            Point center = GetCurrentPastePointOnCanvas();

            double x = Snap(center.X - size.Width / 2.0);
            double y = Snap(center.Y - size.Height / 2.0);

            ViewModel.AddImageNode(Convert.ToBase64String(bytes), x, y, size.Width, size.Height);
        }

        private static byte[] EncodeBitmapSourceAsPng(BitmapSource image)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }

        private static (double Width, double Height) GetImagePixelSize(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            return (bitmap.PixelWidth, bitmap.PixelHeight);
        }

        private static (double Width, double Height) GetDisplayImageSize(double pixelWidth, double pixelHeight)
        {
            const double defaultWidth = 240;
            const double defaultHeight = 180;
            const double maxWidth = 420;
            const double maxHeight = 320;
            const double minWidth = 80;
            const double minHeight = 60;

            if (pixelWidth <= 0 || pixelHeight <= 0)
            {
                return (defaultWidth, defaultHeight);
            }

            double scale = Math.Min(maxWidth / pixelWidth, maxHeight / pixelHeight);
            scale = Math.Min(1.0, scale);

            double width = pixelWidth * scale;
            double height = pixelHeight * scale;

            if (width < minWidth)
            {
                double ratio = minWidth / width;
                width *= ratio;
                height *= ratio;
            }

            if (height < minHeight)
            {
                double ratio = minHeight / height;
                width *= ratio;
                height *= ratio;
            }

            return (width, height);
        }

        private Point GetViewportCenterOnCanvas()
        {
            double scaleX = Math.Abs(MainScale.ScaleX) < 0.0001 ? 1.0 : MainScale.ScaleX;
            double scaleY = Math.Abs(MainScale.ScaleY) < 0.0001 ? 1.0 : MainScale.ScaleY;

            double viewportCenterX = ViewportContainer.ActualWidth > 0 ? ViewportContainer.ActualWidth / 2.0 : ActualWidth / 2.0;
            double viewportCenterY = ViewportContainer.ActualHeight > 0 ? ViewportContainer.ActualHeight / 2.0 : ActualHeight / 2.0;

            return new Point(
                (viewportCenterX - MainTranslate.X) / scaleX,
                (viewportCenterY - MainTranslate.Y) / scaleY);
        }
    }
}
