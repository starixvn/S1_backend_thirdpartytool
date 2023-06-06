using Flurl.Http;
using ThirdPartyTool.Core.DataLayers;
using ThirdPartyTool.Core.Helpers;
using ThirdPartyTool.Core.Helpers.Interfaces;
using ThirdPartyTool.Core.Models.Enums;
using ThirdPartyTool.Core.ThirdParty;
using ThirdPartyTool.Core.ThirdParty.Interfaces;
using ThirdPartyTool.Services;

namespace ThirdPartyTool.Extensions
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddDatabaseConnection(this IServiceCollection services, IConfiguration configuration)
        {
            var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILogger<MySqlClient>>();
            return services
                .AddSingleton(param =>
                    new MySqlClient(configuration.GetConnectionString("ConnectionString"), logger)
                    {
                        DataBase = DataBase.Main
                    })
                .AddSingleton(param =>
                    new MySqlClient(configuration.GetConnectionString("ConnectionStringReport"), logger)
                    {
                        DataBase = DataBase.Report
                    })
                .AddSingleton(param =>
                    new MySqlClient(configuration.GetConnectionString("ConnectionStringWager"), logger)
                    {
                        DataBase = DataBase.Wager
                    });
        }

        public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration)
        {
            var proxyHost = configuration.GetValue<string>("ProxyHost");
            var proxyPort = configuration.GetValue<int>("ProxyPort");
            if (!string.IsNullOrEmpty(proxyHost))
            {
                FlurlHttp.Configure(settings =>
                {
                    settings.HttpClientFactory = new ProxyHttpClientFactory(proxyHost, proxyPort);
                });
            }

            return services
                .AddSingleton<IHttpHelper, HttpHelper>()
                .AddTransient<JsonNetSerializer, JsonNetSerializer>()
                .AddTransient<MainDbDataLayer>()
                .AddTransient<ReportDbDataLayer>()
                .AddSingleton<ThirdPartyManager>()
                .AddSingleton<SwitchToolService>();
        }

        public static IServiceCollection RegisterTpHelper(this IServiceCollection services, IConfiguration configuration)
        {
            var supportTpIds = configuration.GetSection("SupportTpIds")
                .Get<IEnumerable<int>>();
            var provider = services.BuildServiceProvider();
            services.AddSingleton<BaseTpHelper>();
            var tpList = provider.GetRequiredService<ThirdPartyManager>()
                .GetAllTpList()
                .Where(x => supportTpIds.Contains(x.TpId));

            services.AddSingleton(provider => tpList);

            var tpTypes = typeof(ITpHelper).Assembly.GetTypes()
                .Where(item => item.GetInterfaces().Contains(typeof(ITpHelper)));

            foreach (var tp in tpTypes)
            {
                services.AddSingleton(provider => (ITpHelper)Activator.CreateInstance(tp, provider));
            }

            return services;
        }
    }
}
