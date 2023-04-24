using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ThirdPartyTool.Core.Models;
using ThirdPartyTool.Core.Models.Enums;

namespace ThirdPartyTool.Core.DataLayers
{
    public class ReportDbDataLayer
    {
        private readonly MySqlClient _dbClient;

        public ReportDbDataLayer(IServiceProvider provider)
        {
            _dbClient = provider.GetServices<MySqlClient>().First(p => p.DataBase == DataBase.Main); //prd 防火牆只開 maindb，懶得改了
        }

        public IEnumerable<ThirdPartyModel> GetAllTpList()
        {
            return _dbClient.Query<ThirdPartyModel>("Service_TpList_GetAll");
        }

        /// <summary>
        /// 取得指定第三方有交易紀錄的客戶資訊清單
        /// </summary>
        /// <param name="tpId">第三方ID</param>
        /// <param name="siteId">平台ID</param>
        /// <returns></returns>
        public IEnumerable<TpCustInfoModel> GetTpAllCustInfoList(int tpId, int siteId)
        {
            var parameter = new DynamicParameters();
            parameter.Add("_TpId", tpId);
            parameter.Add("_SiteId", siteId);
            var sql = @"
                SELECT
                    TC.TpId,
                    TC.CustId,
                    TC.SiteId,
                    C.CurrencyId,
                    TC.UserName,
                    TC.TpUserName,
                    TC.TpPwd,
                    TCY.TpCurrencyId,
                    TCY.ExchangeRate,
                    C.IsTest,
                    C.ActStatus,
                    TC.`Status` AS TpCustStatus,
                    C.`Status` AS CustStatus
                FROM TpCurrency AS TCY
                INNER JOIN Customer AS C
                    ON C.CurrencyId = TCY.CurrencyId
                RIGHT OUTER JOIN TpCustInfo AS TC
                    ON TC.CustId = C.CustId AND TC.TpId = TCY.TpId
                WHERE TC.TpId = @_TpId
		            AND TC.SiteId = @_SiteId
                    AND TC.LastTradingTime IS NOT NULL;";
            
            return _dbClient.Query<TpCustInfoModel>(sql, parameters: parameter, commandType: CommandType.Text);
        }
        
        /// <summary>
        /// 取得指定第三方有交易紀錄但一段時間沒上線的客戶資訊清單
        /// </summary>
        public IEnumerable<TpCustInfoModel> GetTpInactiveCustInfoList(int tpId, int siteId, DateTimeOffset lastLoginTime)
        {
            var parameter = new DynamicParameters();
            parameter.Add("_TpId", tpId);
            parameter.Add("_SiteId", siteId);
            parameter.Add("_LastLoginTime", lastLoginTime.UtcDateTime);
            var sql = @"
                SELECT
                    TC.TpId,
                    TC.CustId,
                    TC.SiteId,
                    C.CurrencyId,
                    TC.UserName,
                    TC.TpUserName,
                    TC.TpPwd,
                    TCY.TpCurrencyId,
                    TCY.ExchangeRate,
                    C.IsTest,
                    C.ActStatus,
                    TC.`Status` AS TpCustStatus,
                    C.`Status` AS CustStatus
                FROM TpCurrency AS TCY
                INNER JOIN Customer AS C
                    ON C.CurrencyId = TCY.CurrencyId
                RIGHT OUTER JOIN TpCustInfo AS TC
                    ON TC.CustId = C.CustId AND TC.TpId = TCY.TpId
                WHERE TC.TpId = @_TpId
		            AND TC.SiteId = @_SiteId
                    AND TC.LastTradingTime IS NOT NULL
                    AND C.LastLoginTime <= @_LastLoginTime;";
            
            return _dbClient.Query<TpCustInfoModel>(sql, parameters: parameter, commandType: CommandType.Text);
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