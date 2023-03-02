using TaggingTool.Core.DataLayers;
using TaggingTool.Core.Models.Enums;
using TaggingTool.Core.Service;
using TaggingTool.Services;

namespace TaggingTool.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddDatabaseConnection(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<MySqlClient>>();
        var s = configuration.GetConnectionString("ConnectionString");
        
        return services
            .AddSingleton(param => new MySqlClient(configuration.GetConnectionString("ConnectionString"), logger, DataBase.Main))
            .AddSingleton(param => new MySqlClient(configuration.GetConnectionString("ConnectionStringReport"), logger, DataBase.Report));
    }
    
    public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddSingleton<CustomerService>()
            .AddSingleton<ConfigService>()
            .AddSingleton<TaggingDuplicateAccountService>();
    }
}