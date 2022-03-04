namespace ThirdPartyTool.Core.Extensions
{
    internal static class CommonExtension
    {
        public static decimal ToOurAmount(this decimal amount, decimal exchangeRate)
        {
            if (exchangeRate > 1)
            {
                return amount * exchangeRate;
            }

            return amount;
        }

        public static decimal ToTpAmount(this decimal amount, decimal exchangeRate)
        {
            if (exchangeRate > 1)
            {
                return amount / exchangeRate;
            }

            return amount;
        }
    }
}