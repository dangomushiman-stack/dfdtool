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
        private void BtnAddSheet_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AddSheet();
            MainScale.ScaleX = 1;
            MainScale.ScaleY = 1;
            MainTranslate.X = 0;
            MainTranslate.Y = 0;
        }

        private void BtnDeleteSheet_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.DeleteCurrentSheet();
            MainScale.ScaleX = 1;
            MainScale.ScaleY = 1;
            MainTranslate.X = 0;
            MainTranslate.Y = 0;
        }

        private void SheetTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source == sender)
            {
                ViewModel.ResetSelection();
                MainScale.ScaleX = 1;
                MainScale.ScaleY = 1;
                MainTranslate.X = 0;
                MainTranslate.Y = 0;
                MainCanvas.Focus();
            }
        }

        private void SheetNameTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // シングルクリックはTabControl本来のシート切替に任せる。
            // ダブルクリック時だけシート名編集に入る。
            if (e.ClickCount == 2 && ((FrameworkElement)sender).DataContext is DiagramSheetViewModel sheet)
            {
                sheet.IsNameEditing = true;
                e.Handled = true;
            }
        }

        private void SheetNameTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBox tb && tb.IsVisible)
            {
                tb.Focus();
                tb.SelectAll();
            }
        }

        private void SheetNameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                if (((FrameworkElement)sender).DataContext is DiagramSheetViewModel sheet)
                {
                    sheet.IsNameEditing = false;
                }

                Keyboard.ClearFocus();
                MainCanvas.Focus();
                e.Handled = true;
            }
        }

        private void SheetNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is DiagramSheetViewModel sheet)
            {
                sheet.IsNameEditing = false;
            }
        }
    }
}
