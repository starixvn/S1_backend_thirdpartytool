using System;
using System.Collections.Generic;
using System.Text;

namespace ThirdPartyTool.Core.Models
{
    public class TpTransferInfo
    {
        public int TpId { get; set; }
        public int CustId { get; set; }
        public string UserName { get; set; }
        public int SiteId { get; set; }
        public int Direction { get; set; }
        public decimal Amount { get; set; }
        public string Memo { get; set; }
        public string ThirdId { get; set; }
        public string Prefix { get; set; }
        public string ExtensionInfo { get; set; }
        public int BonusId { get; set; }
    }
}
