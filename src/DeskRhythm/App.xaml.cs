using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DeskRhythm.Infrastructure;
using DeskRhythm.Theming;

namespace DeskRhythm;

/// <summary>
/// App bootstrap: one instance only, error logging, then hand over to <see cref="AppController"/>.
/// There is no main window. The app lives in the notification area.
/// </summary>
public partial class App : Application
{
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var testMode = e.Args.Any(a => a.Equals("--test-mode", StringComparison.OrdinalIgnoreCase));
        var launchedAtSignIn = e.Args.Any(a => a.Equals(StartupRegistration.StartupArgument, StringComparison.OrdinalIgnoreCase));

        if (!SingleInstance.TryAcquire())
        {
            // Another copy is running. It has been asked to show its status panel.
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => ErrorLog.Write(args.ExceptionObject as Exception, "Unhandled");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ErrorLog.Write(args.Exception, "Unobserved task");
            args.SetObserved();
        };

        // A few small cards do not need the GPU. Software rendering keeps the graphics
        // driver out of the process, which saves a lot of memory for an all-day tray app.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        System.Windows.Forms.Application.EnableVisualStyles();
        ThemeManager.Apply();

        _controller = new AppController(testMode);
        _controller.Start(launchedAtSignIn);

        SingleInstance.ListenForSecondLaunch(() => Dispatcher.BeginInvoke(() => _controller?.ShowStatus()));
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _controller?.SaveState();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        SingleInstance.Release();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // A background helper must not vanish from the tray because of one bad event.
        ErrorLog.Write(e.Exception, "UI");
        e.Handled = true;
    }
}
