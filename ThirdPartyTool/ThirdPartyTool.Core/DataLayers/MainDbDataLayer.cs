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
        private const int TypePosition = 2000;

        public MainDbDataLayer(IServiceProvider provider)
        {
            var a = provider.GetServices<MySqlClient>();
            _dbClient = a.First(p => p.DataBase == DataBase.Main);
        }

        public string CreateTpTransferInfo(TpTransferInfoModel input)
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

            return _dbClient.QueryFirst<string>("Service_TpTransferInfo_Ins", parameter);
        }

        public bool SetTpTransferInfo(SetTpTransferInfoModel input, IDbTransaction trans)
        {
            var parameter = new DynamicParameters();
            parameter.Add("TransferId", input.TransferId);
            parameter.Add("ThirdId", input.ThirdId);
            parameter.Add("Memo", input.Memo);
            parameter.Add("ForStatus", (int)input.ForStatus);
            parameter.Add("Status", (int)input.Status);

            return _dbClient.Execute("TpTransferInfo_Set", trans, parameter) > 0;
        }

        public (bool Success, decimal AfterAmount) SetWalletBalance(SetWalletBalanceModel input, IDbTransaction trans)
        {
            var parameter = new DynamicParameters();
            parameter.Add("CustId", input.CustId);
            parameter.Add("UserName", input.UserName);
            parameter.Add("SiteId", input.SiteId);
            parameter.Add("Amount", input.Amount);
            parameter.Add("TYPE", TypePosition + input.TpId);
            parameter.Add("Memo", input.Memo);
            parameter.Add("RefTransId", input.RefTransId);
            parameter.Add("Is1stDepoist", dbType: DbType.Byte, direction: ParameterDirection.Output);
            parameter.Add("AfterAmount", dbType: DbType.Decimal, direction: ParameterDirection.Output);

            var success = _dbClient.Execute("Wallet_Set", trans, parameter) > 0;
            var afterAmount = success ? parameter.Get<decimal>("AfterAmount") : 0;

            return (success, afterAmount);
        }

        public decimal SetSabaBonus(int custId, string transId, IDbTransaction trans)
        {
            var parameter = new DynamicParameters();
            parameter.Add("CustId", custId);
            parameter.Add("TransId", transId);

            return _dbClient.QueryFirst<decimal>("Service_SabaBonus_Set", trans, parameter);
        }

        public MySqlConnection GetMySqlConnection()
        {
            return _dbClient.GetMySqlConnection();
        }
    }
}