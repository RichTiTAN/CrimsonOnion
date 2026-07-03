using Avalonia;
using System;

namespace CrimsonOnion;

class Program
{
    private static System.Threading.Mutex? _mutex;

    [STAThread]
    public static void Main(string[] args)
    {
        const string appName = "CrimsonOnion_SingleInstanceMutex";
        _mutex = new System.Threading.Mutex(true, appName, out bool createdNew);

        if (!createdNew)
        {
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
            
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
