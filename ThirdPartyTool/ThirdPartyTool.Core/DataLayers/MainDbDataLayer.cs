using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
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

        public string CreateTpTransferInfo(TpTransferInfoModel input, IDbTransaction trans)
        {
            var parameter = new DynamicParameters();
            parameter.Add("TpId", input.TpId);
            parameter.Add("CustId", input.CustId);
            parameter.Add("UserName", input.UserName);
            parameter.Add("SiteId", input.SiteId);
            parameter.Add("Direction", input.Direction);
            parameter.Add("Amount", input.Amount);
            parameter.Add("Memo", input.Memo);
            parameter.Add("ThirdId", input.ThirdId);
            parameter.Add("Prefix", input.Prefix);
            parameter.Add("ExtensionInfo", input.ExtensionInfo);
            parameter.Add("BonusId", input.BonusId);

            return _dbClient.QueryFirst<string>("TpTransferInfo", trans, parameter);
        }

        public bool SetTpTransferInfo(SetTpTransferInfoModel input, IDbTransaction trans)
        {
            var parameter = new DynamicParameters();
            parameter.Add("TransferId", input.TransferId);
            parameter.Add("ThirdId", input.ThirdId);
            parameter.Add("Memo", input.Memo);
            parameter.Add("ForStatus", (int)input.ForStatus);
            parameter.Add("Status", (int)input.Status);

            return _dbClient.Execute("TpTransferInfo", trans, parameter) > 0;
        }

        public (bool Success, decimal AfterAmount) SetWalletBalance(SetWalletBalanceModel input, IDbTransaction trans)
        {
            var parameter = new DynamicParameters();
            parameter.Add("CustId", input.CustId);
            parameter.Add("UserName", input.UserName);
            parameter.Add("SiteId", input.SiteId);
            parameter.Add("Amount", input.Amount);
            parameter.Add("TYPE", input.Type);
            parameter.Add("RefTransId", input.RefTransId);
            parameter.Add("Is1stDepoist", dbType: DbType.Byte, direction: ParameterDirection.Output);
            parameter.Add("AfterAmount", dbType: DbType.Decimal, direction: ParameterDirection.Output);

            var success = _dbClient.Execute("Wallet_Set", trans, parameter) > 0;
            var afterAmount = success ? parameter.Get<decimal>("AfterAmount") : 0;

            return (success, afterAmount);
        }

        public MySqlConnection GetMySqlConnection()
        {
            return _dbClient.GetMySqlConnection();
        }
    }
}