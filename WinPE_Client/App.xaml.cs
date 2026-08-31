using System.Configuration;
using System.Data;
using System.Windows;

namespace WinPE_Client;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (s, args) =>
        {
            try { System.IO.File.WriteAllText(@"d:\Users\Desktop\Windows-ZS\_winpe_crash.log", args.Exception?.ToString() ?? "null"); } catch { }
            args.Handled = false;
        };
        base.OnStartup(e);
    }
}

