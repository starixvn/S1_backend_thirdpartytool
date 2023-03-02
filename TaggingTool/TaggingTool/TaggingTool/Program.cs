using Serilog;
using TaggingTool.Core.Models.Enums;
using TaggingTool.Extensions;
using TaggingTool.Services;

namespace TaggingTool
{
    internal class Program
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
                .RegisterServices(builder.Configuration);

            var taggingService = services.BuildServiceProvider()
                .GetRequiredService<TaggingDuplicateAccountService>();
        

            DoTagging(taggingService);
        }
        
        private static void DoTagging(TaggingDuplicateAccountService service)
        {
            var site = SiteEnum.Undefined;
            while (site == SiteEnum.Undefined)
            {
                PrintMenu();
                var strSite = Console.ReadLine();
                site = ParseActionType(strSite);
            };

            _ = service.DoTagging(site);
            
            Console.ReadLine();
        }
        
        private static void PrintMenu()
        {
            Console.WriteLine("============= Select Site ==============");
            foreach (SiteEnum site in Enum.GetValues(typeof(SiteEnum)))
            {
                if (site != SiteEnum.Undefined)
                    Console.WriteLine($"\t {(int)site}: {site}");
            }
            Console.WriteLine("========================================");
        }
        
        private static SiteEnum ParseActionType(string str)
        {
            try
            {
                var num = int.Parse(str);
                return (SiteEnum)num;
            }
            catch
            {
                return SiteEnum.Undefined;
            }
        }
    }
}