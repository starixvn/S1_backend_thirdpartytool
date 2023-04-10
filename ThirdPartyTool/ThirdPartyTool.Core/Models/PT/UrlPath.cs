namespace ThirdPartyTool.Core.Models.PT
{
    internal class UrlPath
    {
        /// <summary>
        /// 在線狀態
        /// </summary>
        public const string Online = "/player/online";

        /// <summary>
        /// 踢出玩家
        /// </summary>
        public const string Logout = "/player/logout";

        /// <summary>
        /// 玩家餘額
        /// </summary>
        public const string Balance = "/player/balance";

        /// <summary>
        /// 下分操作
        /// </summary>
        public const string Withdraw = "/player/withdraw";

        /// <summary>
        /// 確認轉帳狀態
        /// </summary>
        public const string CheckTransaction = "/player/checktransaction";
    }
}
