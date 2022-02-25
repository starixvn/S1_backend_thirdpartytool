using System.Net;
using System.Net.Http;
using Flurl.Http.Configuration;

namespace ThirdPartyTool.Core.Helpers
{
    public class ProxyHttpClientFactory : DefaultHttpClientFactory
    {
        private readonly string _host;
        private readonly int _port;

        public ProxyHttpClientFactory(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public override HttpMessageHandler CreateMessageHandler()
        {
            return new HttpClientHandler
            {
                Proxy = new WebProxy(_host, _port),
                UseProxy = true
            };
        }
    }
}