using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ThirdPartyTool.Core.DataLayers;
using ThirdPartyTool.Core.Exceptions;
using ThirdPartyTool.Core.Helpers.Interfaces;
using ThirdPartyTool.Core.Models;

namespace ThirdPartyTool.Core.ThirdParty
{
    public class BaseTpHelper
    {
        protected readonly ILogger<BaseTpHelper> Logger;
        protected readonly IHttpHelper HttpHelper;
        protected readonly MainDbDataLayer MainDbDataLayer;
        protected readonly ReportDbDataLayer ReportDataLayer;

        public BaseTpHelper(IServiceProvider provider)
        {
            Logger = provider.GetRequiredService<ILogger<BaseTpHelper>>();
            HttpHelper = provider.GetRequiredService<IHttpHelper>();
            MainDbDataLayer = provider.GetRequiredService<MainDbDataLayer>();
            ReportDataLayer = provider.GetRequiredService<ReportDbDataLayer>();
        }

        public IEnumerable<TpCustInfoModel> GeTptAllCustInfoList(int tpId, int siteId)
        {
            return ReportDataLayer.GetTpAllCustInfoList(tpId, siteId);
        }
        
        public IEnumerable<TpCustInfoModel> GetTpInactiveCustInfoList(int tpId, int siteId, DateTimeOffset lastLoginTime)
        {
            return ReportDataLayer.GetTpInactiveCustInfoList(tpId, siteId, lastLoginTime);
        }

        public IEnumerable<TpCustInfoModel> GetTpAllCustInfoByCust(int tpId, string jsonCustIds)
        {
            return ReportDataLayer.GetTpAllCustInfoByCust(tpId, jsonCustIds);
        }

        public string CreateTpTransferInfo(TpTransferInfoModel tpTransferInfo)
        {
            Logger.LogInformation(JsonConvert.SerializeObject(tpTransferInfo));
            var transId = MainDbDataLayer.CreateTpTransferInfo(tpTransferInfo);
            if (string.IsNullOrEmpty(transId))
            {
                throw new CustomException("CreateTpTransferInfo return TransId is empty.");
            }

            return transId;
        }
    }
}
