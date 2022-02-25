using System;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ThirdPartyTool.Core.Helpers.Interfaces
{
    public interface IHttpHelper
    {
        Task<(TResult Result, HttpStatusCode StatusCode)> GetAsync<TResult>(string url, string query)
            where TResult : class;

        (TResult Result, HttpStatusCode StatusCode) Post<TBody, TResult>(string url, TBody body)
            where TResult : class;

        Task<(TResult Result, HttpStatusCode StatusCode)> PostAsync<TBody, TResult>(string url, TBody body)
            where TResult : class;

        Task<(TResult Result, HttpStatusCode StatusCode)> PostUrlEncodedAsync<TBody, TResult>(string url, TBody body);

        (TResult Result, HttpStatusCode StatusCode) Post<TBody, TResult>(string url, TBody body, Func<XDocument, TResult> selector)
            where TResult : class;

        (TResult Result, HttpStatusCode StatusCode) PostJson<TBody, TResult>(string url, TBody data)
            where TResult : class;

        (TResult Result, HttpStatusCode StatusCode) PostJson<TResult>(string url, string data, string loggerContent = @"")
            where TResult : class;

        Task<(TResult Result, HttpStatusCode StatusCode)> PostJsonAsync<TBody, TResult>(string url, TBody data)
            where TResult : class;

        IHttpHelper WithOAuthBearerToken(string token);

        IHttpHelper WithHeader(string key, string value);
    }
}
