using System.ComponentModel;

namespace ThirdPartyTool.Core.Models.Enums
{
    public enum ActionType
    {
        [Description("Kick users")]
        KickUsers = 0,

        [Description("Transfer back")]
        TransferBack = 1,
        
        [Description("Transfer retry")]
        TransferRetry = 2,

        [Description("Get Balance")]
        GetBalance = 3,
        
        [Description("Kick users and transfer back")]
        KickUserAndTransferBack = 4,

        [Description("Exit")]
        Exit = -1,

        Undefined = -9
    }
}