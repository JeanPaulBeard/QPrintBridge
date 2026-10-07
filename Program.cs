using System.Windows.Forms;

namespace QPrintBridge;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (Environment.UserInteractive)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ServiceForm());
            return;
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = ServiceManager.ServiceName;
        });
        builder.Services.AddHostedService<Worker>();

        using var host = builder.Build();
        host.Run();
    }
}
