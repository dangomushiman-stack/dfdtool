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
        private void FileFormatTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                Keyboard.ClearFocus();
                MainCanvas.Focus();
                e.Handled = true;
            }
        }

        private void FileFormatTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // ファイル形式欄は常時編集可能にするため、通常テキストの編集状態は変更しない。
        }

        private void TextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) 
        { 
            if (sender is TextBox tb && tb.IsVisible) 
            { 
                tb.Focus(); 
                tb.SelectAll(); 
            } 
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e) 
        { 
            // Shift + Enter は TextBox 標準の改行入力に任せる。
            if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                return;
            }

            // Enter は編集終了。Esc も編集終了。
            if (e.Key == Key.Enter || e.Key == Key.Escape)
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
