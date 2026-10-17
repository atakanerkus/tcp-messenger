using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace TcpMessenger.Tests.E2E.Fixtures;

/// <summary>
/// One running copy of TcpMessenger.App.exe (copied next to the test assembly by the project
/// reference) together with its UI Automation session and page object.
/// </summary>
internal sealed class AppInstance : IDisposable
{
    private static readonly TimeSpan WindowTimeout = TimeSpan.FromSeconds(30);

    private readonly Application _application;
    private readonly UIA3Automation _automation;

    private AppInstance(Application application, UIA3Automation automation, Window window)
    {
        _application = application;
        _automation = automation;
        Window = window;
        Driver = new MainWindowDriver(window);
    }

    public Window Window { get; }

    public MainWindowDriver Driver { get; }

    public static string ExePath => Path.Combine(AppContext.BaseDirectory, "TcpMessenger.App.exe");

    /// <summary>Starts the application with the form pre-filled; nothing is connected yet.</summary>
    public static AppInstance Launch(string mode, int port, bool autoReply, string host = "127.0.0.1")
    {
        if (!File.Exists(ExePath))
        {
            throw new FileNotFoundException("Build the solution first; the application is copied next to the tests.", ExePath);
        }

        var startInfo = new ProcessStartInfo(ExePath)
        {
            Arguments = $"--mode {mode} --host {host} --port {port} --autoreply {(autoReply ? "on" : "off")}",
            WorkingDirectory = AppContext.BaseDirectory,
        };

        var application = Application.Launch(startInfo);
        var automation = new UIA3Automation();
        try
        {
            var window = application.GetMainWindow(automation, WindowTimeout)
                ?? throw new TimeoutException($"Main window of {mode} instance did not appear within {WindowTimeout.TotalSeconds} s.");
            return new AppInstance(application, automation, window);
        }
        catch
        {
            application.Kill();
            automation.Dispose();
            throw;
        }
    }

    /// <summary>Saves a screenshot of the window; handy when a scenario fails.</summary>
    public string SaveScreenshot(string name)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "e2e-screenshots");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{name}.png");
        using var bitmap = Window.Capture();
        bitmap.Save(path);
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (!_application.Close())
            {
                _application.Kill();
            }
        }
        catch (Exception)
        {
            // best effort: the process may already be gone
        }

        _application.Dispose();
        _automation.Dispose();
    }
}
