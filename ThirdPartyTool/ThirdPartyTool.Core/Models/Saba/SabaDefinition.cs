using System.Collections.Generic;
using ThirdPartyTool.Core.Models.Enums;

namespace ThirdPartyTool.Core.Models.Saba
{
    internal class SabaDefinition
    {
        public const string SabaSuccessCode = "0";

        public static bool KickUserSuccess(string errorCode) =>
            errorCode switch
            {
                SabaSuccessCode => true,
                KickUserErrorCode.UserNotOnline => true,
                _ => false
            };

        public static TransferStatus GetTransferStatus(string status) =>
            status switch
            {
                "0" => TransferStatus.Success,
                "1" => TransferStatus.Failed,
                _ => TransferStatus.Pending
            };

        public static TransferStatus GetCheckTransferStatus(string errorCode, string status)
        {
            return errorCode switch
            {
                SabaSuccessCode => GetTransferStatus(status),
                "2" => TransferStatus.Failed, // 會員不存在
                _ => TransferStatus.Pending
            };
        }

        public static IEnumerable<string> CheckTransferErrorErrorCode
            => new List<string>
            {
                "1", // 執行過程中失敗
                "7", // wallet_id 錯誤
                "9" // 廠商識別碼錯誤
            };

        public static bool SabaCommonSuccess(string errorCode) =>
            errorCode == SabaSuccessCode;
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
        public const string Failed = "1";
        
        public const string UserNotExists = "2";
        
        public const string UserNotOnline = "3";

        public const string VendorIdInvalid = "9";

        public const string SystemMaintenance = "10";
    }
}
