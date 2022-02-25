namespace ThirdPartyTool.Core.Models.Saba
{
    internal class SabaDefinition
    {
        public static bool GetKickUserResult(string errorCode) =>
            errorCode switch
            {
                KickUserErrorCode.Success => true,
                KickUserErrorCode.UserNotExists => true,
                KickUserErrorCode.UserNotOnline => true,
                _ => false
            };
        
    }


    internal class Path
    {
        /// <summary>
        /// 登出 Saba 會員
        /// </summary>
        public const string KickUser = "/KickUser";

        /// <summary>
        /// 取得 Saba 會員餘額
        /// </summary>
        public const string CheckUserBalance = "/CheckUserBalance";

        /// <summary>
        /// 轉帳
        /// </summary>
        public const string FundTransfer = "/FundTransfer";

        /// <summary>
        /// 確認轉帳狀態
        /// </summary>
        public const string CheckFundTransfer = "/CheckFundTransfer";
    }

    internal class KickUserErrorCode
    {
        public const string Success = "0";
        
        public const string Failed = "1";
        
        public const string UserNotExists = "2";
        
        public const string UserNotOnline = "3";

        public const string VendorIdInvalid = "9";

        public const string SystemMaintenance = "10";
    }
}
