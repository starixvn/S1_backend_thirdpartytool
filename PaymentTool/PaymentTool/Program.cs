using Serilog;
using PaymentTool.Extensions;
using PaymentTool.Services;

namespace PaymentTool;

internal class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var remoteUrls = builder.Configuration.GetSection("ConfigService").Get<string[]>();
        remoteUrls = builder.Configuration.CheckLocalConfig(builder.Environment, args, remoteUrls);
        builder.Configuration.AddRemoteJsonConfig(remoteUrls);
            
        builder.Host.UseSerilog((context, Configuration) =>
        {
            Configuration
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext();
        });
            
        var services = builder.Services
            .AddDatabaseConnection(builder.Configuration)
            .RegisterServices(builder.Configuration);

        var depositService = services.BuildServiceProvider()
            .GetRequiredService<DepositService>();
        
        DoWork(depositService);
    }
        
    private static void DoWork(DepositService service)
    {
        SetFlowEndTime(service);
            
        Console.ReadLine();
    }
        
    private static void SetFlowEndTime(DepositService service)
    {
        const int batchCnt = 3000;

        _ = service.Set1stDepositTransId(batchCnt, 0, -1);
    }
}