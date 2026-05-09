using System;
using System.IO;
using System.Windows;

namespace DfdToolWpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var window = new MainWindow();
        window.Show();

        if (e.Args.Length == 0) return;

        string fileName = e.Args[0];
        string extension = Path.GetExtension(fileName);

        if (File.Exists(fileName) &&
            (extension.Equals(".dfdj", StringComparison.OrdinalIgnoreCase) ||
             extension.Equals(".json", StringComparison.OrdinalIgnoreCase)))
        {
            window.LoadFromFile(fileName);
        }
        else
        {
            MessageBox.Show($"対応していないファイル、または存在しないファイルです。\n{fileName}", "ファイルを開けません");
        }
    }
}
