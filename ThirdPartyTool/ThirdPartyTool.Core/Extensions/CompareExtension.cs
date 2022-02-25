using System.Net;

namespace ThirdPartyTool.Core.Extensions
{
    public static class CompareExtension
    {
        public static bool IsSuccessStatus(this HttpStatusCode httpStatus)
            => (int)httpStatus >= 200 && (int)httpStatus < 300;

        public static bool IsJson(string str)
        {
            if (string.IsNullOrEmpty(str))
                return true;

            var value = str.Trim();

            return (value.StartsWith("{") && value.EndsWith("}"))
                   || (value.StartsWith("[") && value.EndsWith("]"));
        }
    }
}
