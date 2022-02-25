using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ThirdPartyTool.Core.Models;
using ThirdPartyTool.Core.Models.Enums;

namespace ThirdPartyTool.Core.DataLayers
{
    public class MainDbDataLayer
    {
        private readonly MySqlClient _dbClient;

        public MainDbDataLayer(IServiceProvider provider)
        {
            var a = provider.GetServices<MySqlClient>();
            _dbClient = a.First(p => p.DataBase == DataBase.Main);
        }

        /// <summary>
        /// 取得指定第三方客戶資訊清單
        /// </summary>
        /// <param name="tpId">第三方ID</param>
        /// <param name="siteId">平台ID</param>
        /// <returns></returns>
        public IEnumerable<TpCustInfoModel> GetTpAllCustInfoList(int tpId, int siteId)
        {
            var parameter = new DynamicParameters();
            parameter.Add("_FirstRun", true);
            parameter.Add("_TpId", tpId);
            parameter.Add("_SiteId", siteId);

            return _dbClient.Query<TpCustInfoModel>("Service_TpCustInfo_Get", parameters: parameter);
        }


        public IEnumerable<TpCustInfoModel> GetTpAllCustInfoByCust(int tpId, string jsonCustIds)
        {
            var parameter = new DynamicParameters();
            parameter.Add("_TpId", tpId);
            parameter.Add("_CustIds", jsonCustIds);

            return _dbClient.Query<TpCustInfoModel>("Service_TpCustInfoListByCust_Get", parameters: parameter);
        }
    }
}