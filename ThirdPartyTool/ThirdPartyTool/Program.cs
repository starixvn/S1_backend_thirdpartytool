using Serilog;
using ThirdPartyTool.Core.Models.Enums;
using ThirdPartyTool.Extensions;
using ThirdPartyTool.Services;

namespace ThirdPartyTool
{
    public class Program
    {
        private readonly IEnumerable<SiteEnum> _siteList = new[] {SiteEnum.Nova88, SiteEnum.Goal123, SiteEnum.Bwing};

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddRemoteJsonConfig(builder.Configuration.GetValue<string>("ConfigService"));

            builder.Host.UseSerilog((context, Configuration) =>
            {
                Configuration
                    .ReadFrom.Configuration(builder.Configuration)
                    .Enrich.FromLogContext();
            });

            var services = builder.Services
                .AddDatabaseConnection(builder.Configuration)
                .RegisterServices(builder.Configuration)
                .RegisterTpHelper(builder.Configuration);

            var switchToolService = services.BuildServiceProvider()
                .GetRequiredService<SwitchToolService>();
            
            _ = switchToolService.StartAsync();
        }
    }
}
