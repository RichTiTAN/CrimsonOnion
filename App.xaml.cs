using System.Windows;

namespace CrimsonOnion
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            // Ensure only one instance runs
            using (var currentProcess = System.Diagnostics.Process.GetCurrentProcess())
            {
                var procName = currentProcess.ProcessName;
                var existing = System.Diagnostics.Process.GetProcessesByName(procName);
                if (existing.Length > 1)
                {
                    // Bring existing window to front via WinAPI
                    foreach (var p in existing)
                    {
                        if (p.Id != currentProcess.Id && p.MainWindowHandle != IntPtr.Zero)
                        {
                            ShowWindow(p.MainWindowHandle, 9); // SW_RESTORE
                            SetForegroundWindow(p.MainWindowHandle);
                        }
                        p.Dispose();
                    }
                    Shutdown();
                    return;
                }
                foreach (var p in existing) p.Dispose();
            }

            base.OnStartup(e);

            DispatcherUnhandledException += (s, ex) =>
            {
                ex.Handled = true;
                System.Diagnostics.Debug.WriteLine($"Unhandled: {ex.Exception}");
            };
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
