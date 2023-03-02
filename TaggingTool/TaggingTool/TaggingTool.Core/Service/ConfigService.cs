using Microsoft.Extensions.DependencyInjection;
using TaggingTool.Core.DataLayers;
using TaggingTool.Core.Models.Enums;

namespace TaggingTool.Core.Service
{
    public class ConfigService
    {
        private readonly MySqlClient _reportDbClient;

        public ConfigService(IServiceProvider provider)
        {
            _reportDbClient = provider.GetServices<MySqlClient>().First(p => p.DataBase == DataBase.Report);
        }

        public IEnumerable<T> Query<T>(string sql, object parameters = null)
        {
            return _reportDbClient.Query<T>(sql, parameters);
        }
    }
}
