using System;
using System.Runtime.Serialization;

namespace ThirdPartyTool.Core.Exceptions
{
    public class CustomException : Exception
    {
        public CustomException()
        {
        }

        public CustomException(string message)
            : base(message)
        {
        }

        public CustomException(string message, Exception inner)
            : base(message, inner)
        {
        }

        public CustomException(string message, string code)
            : base(message)
        {
            Code = code;
        }

        protected CustomException(SerializationInfo info, StreamingContext context)
        {
        }

        /// <summary>
        /// Error Code
        /// </summary>
        public string Code { get; set; } = "9000";
    }
}
