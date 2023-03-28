using Dapper;
using System.Data.Common;
namespace SlotGameGenerator
{
    public static class SlotGameDataLayer
    {
        public static async Task<int> QueryNextGameId(this DbConnection connection, int tpId) =>
            await connection.QueryFirstAsync<int>($"SELECT MAX(SlotGame.Id)+1 AS NextId " +
                                                  $"FROM Siebog.SlotGame " +
                                                  $"WHERE SlotGame.TpId = {tpId} AND SlotGame.TypeId = 1;");

        public static async Task<IEnumerable<int>> QueryTpCurrency(this DbConnection connection, int tpId) =>
            await connection.QueryAsync<int>($"SELECT CurrencyId FROM Siebog.TpCurrency WHERE TpId={tpId};");
    }
}
