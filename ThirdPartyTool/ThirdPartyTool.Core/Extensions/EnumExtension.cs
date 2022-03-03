using System;
using System.ComponentModel;

namespace ThirdPartyTool.Core.Extensions
{
    public static class EnumExtension
    {
        public static string GetDescription(this Enum value)
        {
            var filInfo = value.GetType().GetField(value.ToString());
            var attr = (DescriptionAttribute[])filInfo?.GetCustomAttributes(typeof(DescriptionAttribute), false);
            
            return attr != null
                ? attr.Length > 0 ? attr[0].Description : value.ToString()
                : string.Empty;
        }
    }
}
