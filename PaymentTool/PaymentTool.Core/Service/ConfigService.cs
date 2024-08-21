using Microsoft.Extensions.DependencyInjection;
using PaymentTool.Core.DataLayers;
using PaymentTool.Core.Models.Enums;

namespace PaymentTool.Core.Service
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
