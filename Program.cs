using QPrintBridge;

var builder = Host.CreateApplicationBuilder(args);

// Habilita que el worker corra como Servicio de Windows
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "QPrintBridge Service";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
