using System.Security.Cryptography;
using System.Text;

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
        
        public static string AesEncrypt(string str, string key, bool isEncodeUrl = true)
        {
            if (string.IsNullOrEmpty(str)) return null;
            var toEncryptArray = Encoding.UTF8.GetBytes(str);

            var rm = new RijndaelManaged
            {
                Key = Encoding.UTF8.GetBytes(key),
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            };

            var cTransform = rm.CreateEncryptor();
            var resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

            var base64Str = Convert.ToBase64String(resultArray, 0, resultArray.Length);

            return isEncodeUrl ? System.Web.HttpUtility.UrlEncode(base64Str) : base64Str;
        }
        
        public static string ToMD5(string encodeStr)
        {
            using var cryptoMD5 = MD5.Create();
            var bytes = Encoding.UTF8.GetBytes(encodeStr);
            var hash = cryptoMD5.ComputeHash(bytes);
            var md5 = BitConverter.ToString(hash)
                .Replace("-", string.Empty);

            return md5;
        }
    }
}