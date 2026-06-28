using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DfdToolWpf
{
    public partial class MainWindow
    {
        private sealed class JumpTargetInfo
        {
            public JumpTargetInfo(DiagramSheetViewModel sheet, NodeViewModel node)
            {
                Sheet = sheet;
                Node = node;
            }

            public DiagramSheetViewModel Sheet { get; }
            public NodeViewModel Node { get; }
        }

        private void JumpMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menu)
            {
                return;
            }

            menu.Items.Clear();

            var targets = ViewModel.Sheets
                .SelectMany(sheet => sheet.Nodes
                    .Where(node => !string.IsNullOrWhiteSpace(node.JumpLabel))
                    .Select(node => new JumpTargetInfo(sheet, node)))
                .OrderBy(target => GetDefaultJumpLabel(target.Node), StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(target => target.Sheet.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (!targets.Any())
            {
                menu.Items.Add(new MenuItem
                {
                    Header = "ジャンプラベルが設定された図形はありません",
                    IsEnabled = false
                });
                return;
            }

            foreach (var target in targets)
            {
                var item = new MenuItem
                {
                    Header = BuildJumpTargetHeader(target),
                    ToolTip = $"{target.Sheet.Name} / X:{target.Node.X:0}, Y:{target.Node.Y:0}",
                    Tag = target
                };
                item.Click += JumpTargetMenuItem_Click;
                menu.Items.Add(item);
            }
        }

        private void JumpTargetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item || item.Tag is not JumpTargetInfo target)
            {
                return;
            }

            if (!ViewModel.Sheets.Contains(target.Sheet) || !target.Sheet.Nodes.Contains(target.Node))
            {
                MessageBox.Show("ジャンプ先の図形が見つかりませんでした。", "ジャンプ");
                return;
            }

            ViewModel.SelectedSheet = target.Sheet;
            SelectNodeAndCenterInView(target.Node);
        }

        private string BuildJumpTargetHeader(JumpTargetInfo target)
        {
            string label = GetDefaultJumpLabel(target.Node);
            string sheetName = string.IsNullOrWhiteSpace(target.Sheet.Name) ? "Sheet" : target.Sheet.Name.Trim();
            return $"{label}  ({sheetName})";
        }

        private void MenuItem_SetJumpLabel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item)
            {
                return;
            }

            var node = GetNodeFromContextMenuItem(item);
            if (node == null)
            {
                return;
            }

            string newLabel = GetDefaultJumpLabel(node);
            if (node.JumpLabel == newLabel)
            {
                return;
            }

            ViewModel.SaveUndoState();
            node.JumpLabel = newLabel;
            ViewModel.MarkDirty();
        }

        private void MenuItem_ClearJumpLabel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem item)
            {
                return;
            }

            var node = GetNodeFromContextMenuItem(item);
            if (node == null || string.IsNullOrWhiteSpace(node.JumpLabel))
            {
                return;
            }

            ViewModel.SaveUndoState();
            node.JumpLabel = string.Empty;
            ViewModel.MarkDirty();
        }

        private string? ShowJumpLabelInputDialog(string currentLabel)
        {
            var dialog = new Window
            {
                Title = "ジャンプラベルを設定",
                Owner = this,
                Width = 420,
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
                Text = "ジャンプラベル（空欄で解除）:",
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(label, 0);
            root.Children.Add(label);

            var textBox = new TextBox
            {
                Text = currentLabel ?? string.Empty,
                MinWidth = 370
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

        private string GetDefaultJumpLabel(NodeViewModel node)
        {
            string title = GetNodeDisplayTitle(node);
            return string.IsNullOrWhiteSpace(title) ? "ジャンプ先" : title;
        }

        private string GetNodeDisplayTitle(NodeViewModel node)
        {
            string text = node.Type == EditorMode.Table ? node.TableHeaderText : node.Text;
            text = (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Split('\n').FirstOrDefault()?.Trim() ?? string.Empty;
        }
    }
}
