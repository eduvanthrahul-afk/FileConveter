using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace OrbitConvert;

public partial class App : Application
{
    private MainWindow? _window;
    private Forms.NotifyIcon? _trayIcon;
    private ShiftDragHook? _keyboardHook;
    private Mutex? _instanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _instanceMutex = new Mutex(initiallyOwned: false, name: @"Local\OrbitConvert.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("OrbitConvert is already running. Open it from the notification area.",
                "OrbitConvert", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _window = new MainWindow();
        _keyboardHook = new ShiftDragHook(() =>
            Dispatcher.BeginInvoke(new Action(() => _window?.ShowCentered())));

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open OrbitConvert", null, (_, _) => _window?.ShowCentered(activate: true));
        menu.Items.Add("Exit", null, (_, _) => Shutdown());

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "OrbitConvert — Shift while dragging a file",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => _window?.ShowCentered(activate: true);
        _window.ShowCentered(activate: true);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _keyboardHook?.Dispose();
        _trayIcon?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
