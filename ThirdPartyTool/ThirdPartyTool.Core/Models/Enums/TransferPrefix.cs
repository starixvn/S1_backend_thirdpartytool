using System.ComponentModel;

namespace ThirdPartyTool.Core.Models.Enums
{
    public enum TransferPrefix
    {
        [Description("Transfer")]
        T = 0,
        [Description("Auto Transfer")]
        AT = 1,
        [Description("Bonus Cancel Transfer")]
        BT = 2,
        /// <summary>
        /// 轉帳制轉單一錢包強制轉回
        /// </summary>
        [Description("Force Transfer")]
        FT = 3
    }
}