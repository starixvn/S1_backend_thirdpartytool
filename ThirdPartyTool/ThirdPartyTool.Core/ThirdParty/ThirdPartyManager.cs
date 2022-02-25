using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThirdPartyTool.Core.DataLayers;
using ThirdPartyTool.Core.Models;

namespace ThirdPartyTool.Core.ThirdParty
{
    public class ThirdPartyManager
    {
        private readonly ILogger<ThirdPartyManager> _logger;

        private readonly ReportDbDataLayer _reportDbDataLayer;

        private readonly MainDbDataLayer _mainDbDataLayer;

        public ThirdPartyManager(IServiceProvider provider)
        {
            _logger = provider.GetRequiredService<ILogger<ThirdPartyManager>>();
            _reportDbDataLayer = provider.GetRequiredService<ReportDbDataLayer>();
            _mainDbDataLayer = provider.GetRequiredService<MainDbDataLayer>();
        }

        public IEnumerable<ThirdPartyModel> GetAllTpList()
        {
            return _reportDbDataLayer.GetAllTpList();
        }
    }
}