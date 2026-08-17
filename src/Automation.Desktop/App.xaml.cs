using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using Automation.Desktop.ViewModels;
using Automation.Infrastructure;
using Automation.Platform;
using Automation.Platform.Contracts.Runs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Automation.Desktop;

public partial class App : Application
{
    private IHost? _host;
    private Mutex? _singleInstanceMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            if (!TryAcquireSingleInstanceMutex())
            {
                MessageBox.Show(
                    "โปรแกรมกำลังเปิดใช้งานอยู่แล้ว\nGlobalsoft Automation is already running.",
                    "Globalsoft Automation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddAutomationPlatform();
            builder.Services.AddAutomationInfrastructure(builder.Configuration);
            builder.Services.AddSingleton<MainWindow>();
            builder.Services.AddSingleton<MainWindowViewModel>();
            builder.Services.AddSingleton<AutomationCatalogViewModel>();

            _host = builder.Build();
            await _host.StartAsync();

            var historyStore = _host.Services.GetRequiredService<IRunHistoryStore>();
            await historyStore.InitializeAsync();
            await historyStore.MarkRunningAsInterruptedAsync(DateTimeOffset.UtcNow);

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception exception)
        {
            try
            {
                await StopHostAsync();
            }
            catch (Exception cleanupException)
            {
                TraceShutdownFailure(cleanupException);
            }
            finally
            {
                try
                {
                    MessageBox.Show(
                        $"ไม่สามารถเปิดโปรแกรมได้\n\n{exception.Message}",
                        "Globalsoft Automation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    Shutdown(-1);
                }
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Task.Run(StopHostAsync).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            TraceShutdownFailure(exception);
        }
        finally
        {
            try
            {
                ReleaseSingleInstanceMutex();
            }
            finally
            {
                base.OnExit(e);
            }
        }
    }

    private bool TryAcquireSingleInstanceMutex()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var userSid = identity.User?.Value
            ?? throw new InvalidOperationException("Unable to identify the current Windows user.");
        var mutex = new Mutex(
            initiallyOwned: true,
            name: $@"Local\GlobalsoftAutomation-{userSid}",
            createdNew: out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return false;
        }

        _singleInstanceMutex = mutex;
        return true;
    }

    private void ReleaseSingleInstanceMutex()
    {
        var mutex = _singleInstanceMutex;
        if (mutex is null)
        {
            return;
        }

        _singleInstanceMutex = null;
        try
        {
            mutex.ReleaseMutex();
        }
        catch (Exception exception)
        {
            TraceShutdownFailure(exception);
        }
        finally
        {
            mutex.Dispose();
        }
    }

    private async Task StopHostAsync()
    {
        var host = _host;
        if (host is null)
        {
            return;
        }

        _host = null;
        try
        {
            await host.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            host.Dispose();
        }
    }

    private static void TraceShutdownFailure(Exception exception)
    {
        try
        {
            Trace.TraceError("Host shutdown cleanup failed: {0}", exception.GetType().FullName);
        }
        catch
        {
            // Shutdown cleanup failures must not escape the WPF lifecycle.
        }
    }
}
