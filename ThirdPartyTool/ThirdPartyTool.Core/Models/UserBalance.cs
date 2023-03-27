using System;
using System.Collections.Generic;
using System.Text;

namespace ThirdPartyTool.Core.Models
{
    public class UserBalance
    {
        public int CustId { get; set; }
        public string UserName { get; set; }
        public string TpUserName { get; set; }
        public decimal balance { get; set; }
        public decimal bonus { get; set; }
        public decimal outstanding { get; set; }
        public int Currency { get; set; }
        public string RawMessage { get; set; }
    }
}
