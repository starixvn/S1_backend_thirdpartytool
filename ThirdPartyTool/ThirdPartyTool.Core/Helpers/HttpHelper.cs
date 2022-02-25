using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Flurl.Http;
using Flurl.Http.Content;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog.Context;
using ThirdPartyTool.Core.Exceptions;
using ThirdPartyTool.Core.Extensions;
using ThirdPartyTool.Core.Helpers.Interfaces;

namespace ThirdPartyTool.Core.Helpers
{
    public sealed class HttpHelper : IHttpHelper
    {

        private readonly Dictionary<string, string> _header;
        private readonly JsonNetSerializer _serializer;
        private readonly ILogger<HttpHelper> _logger;
        private string _authToken = string.Empty;

        public HttpHelper(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<HttpHelper>>();
            _serializer = serviceProvider.GetRequiredService<JsonNetSerializer>();
            _header = new Dictionary<string, string>();
        }

        public async Task<(TResult Result, HttpStatusCode StatusCode)> GetAsync<TResult>(string url, string query)
            where TResult : class
        {
            var tResult = default(TResult);
            var result = await GetDataAsync(url, query);

            _header.Clear();

            if (result.StatusCode == HttpStatusCode.OK)
                tResult = _serializer.DeserializeFormStream<TResult>(result.ContentStream);

            return (tResult, result.StatusCode);
        }

        public (TResult Result, HttpStatusCode StatusCode) Post<TBody, TResult>(string url, TBody body)
            where TResult : class
        {
            var returnObj = PostData(url, body, ContentType.Text);

            if (!CompareExtension.IsJson(returnObj.Result))
            {
                throw new CustomException("Invalid JSON string, cannot deserialize object.");
            }

            var result = JsonConvert.DeserializeObject<TResult>(returnObj.Result);

            return (result, returnObj.StatusCode);
        }

        public async Task<(TResult Result, HttpStatusCode StatusCode)> PostAsync<TBody, TResult>(string url, TBody body)
            where TResult : class
        {
            try
            {
                var returnObj = await PostDataAsync(url, body, ContentType.Text);
                var result = _serializer.DeserializeFormStream<TResult>(returnObj.Result);

                return (result, returnObj.StatusCode);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task<(TResult Result, HttpStatusCode StatusCode)> PostUrlEncodedAsync<TBody, TResult>(string url, TBody body)
        {
            var (stream, statusCode) = await PostDataAsync(url, body, ContentType.UrlEncoded);
            var result = _serializer.DeserializeFormStream<TResult>(stream);

            return (result, statusCode);
        }

        public (TResult Result, HttpStatusCode StatusCode) Post<TBody, TResult>(string url, TBody body, Func<XDocument, TResult> selector)
            where TResult : class
        {
            var returnObj = PostData(url, body, ContentType.Text);
            var xml = XDocument.Parse(returnObj.Result);
            var result = selector(xml);

            return (result, returnObj.StatusCode);
        }

        public (TResult Result, HttpStatusCode StatusCode) PostJson<TBody, TResult>(string url, TBody data)
            where TResult : class
        {
            var returnObj = PostData(url, data, ContentType.Json);

            if (!CompareExtension.IsJson(returnObj.Result))
            {
                throw new CustomException("Invalid JSON string, Cannot DeserializeObject.");
            }

            var result = JsonConvert.DeserializeObject<TResult>(returnObj.Result);
            return (result, returnObj.StatusCode);
        }

        public async Task<(TResult Result, HttpStatusCode StatusCode)> PostJsonAsync<TBody, TResult>(string url, TBody data)
            where TResult : class
        {
            LogContext.PushProperty("url", url);
            _logger.LogInformation(_serializer.SerializeToString(data));
            var returnObj = await PostDataAsync(url, data, ContentType.Json);
            var result = _serializer.DeserializeFormStream<TResult>(returnObj.Result);
            return (result, returnObj.StatusCode);
        }

        public (TResult Result, HttpStatusCode StatusCode) PostJson<TResult>(string url, string data, string loggerContent = "")
            where TResult : class
        {
            var returnObj = PostJsonFromString<TResult>(url, data, loggerContent);
            return returnObj;
        }

        public IHttpHelper WithOAuthBearerToken(string token)
        {
            _authToken = token;

            return this;
        }

        public IHttpHelper WithHeader(string key, string value)
        {
            if (!_header.ContainsKey(key))
                _header.Add(key, value);

            if (_header.ContainsKey(key) && _header[key] != value)
                _header[key] = value;

            return this;
        }

        #region Private Methods



        #endregion

        private FlurlRequest CreateFlurlRequest(string url)
        {
            var request = new FlurlRequest(url)
                .AllowAnyHttpStatus();

            if (_authToken != string.Empty)
                request.WithOAuthBearerToken(_authToken);

            return request;
        }

        private (string Result, HttpStatusCode StatusCode) PostData<TBody>(string url, TBody data, string postType)
        {
            var request = CreateFlurlRequest(url);
            request = _header.Aggregate(request, (current, item)
                => current.WithHeader(item.Key, item.Value));

            IFlurlResponse response = default(FlurlResponse);
            if (postType == ContentType.Json)
            {
                response = request
                    .PostJsonAsync(data)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
            }
            else if (postType == ContentType.Text)
            {
                var content = data.ToString();
                response = request
                    .PostStringAsync(content)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
            }
            else if (postType == "form-data")
            {
                response = request
                    .PostUrlEncodedAsync(data)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
            }

            if (response == null)
                throw new CustomException("Response is null.");

            var result = response.ResponseMessage.Content
                .ReadAsStringAsync()
                .GetAwaiter()
                .GetResult();

            _header.Clear();

            return (Result: result, response.ResponseMessage.StatusCode);
        }

        private async Task<(Stream Result, HttpStatusCode StatusCode)> PostDataAsync<TBody>(string url, TBody data, string postType)
        {
            var sw = Stopwatch.StartNew();
            var request = CreateFlurlRequest(url);
            request = _header.Aggregate(request, (current, item) => current.WithHeader(item.Key, item.Value));

            _logger.LogInformation($"POST ({postType}): {url}");

            if (request.Headers.Any())
            {
                _logger.LogInformation("Headers:" 
                    + JsonConvert.SerializeObject(request.Headers.ToDictionary(x => x.Name, x => x.Value)));
            }
            
            _logger.LogInformation(JsonConvert.SerializeObject(data));
            IFlurlResponse response = default(FlurlResponse);
            if (postType == ContentType.Json)
            {
                response = await request.PostJsonAsync(data);
            }
            else if (postType == ContentType.Text)
            {
                var content = data.ToString();
                response = await request.PostStringAsync(content);
            }
            else if (postType == ContentType.UrlEncoded)
            {
                response = await request.PostUrlEncodedAsync(data);
            }

            LogContext.PushProperty("elapsed", sw.Elapsed.TotalMilliseconds);
            LogContext.PushProperty("url", url);

            if (response == null)
                throw new CustomException("Response is null.");

            _logger.LogInformation(await response.ResponseMessage.Content.ReadAsStringAsync());
            var result = await response.ResponseMessage.Content.ReadAsStreamAsync();
            _header.Clear();

            return (Result: result, response.ResponseMessage.StatusCode);
        }

        private (TResult Result, HttpStatusCode StatusCode) PostJsonFromString<TResult>(string url, string data, string loggerContent)
        {
            var sw = Stopwatch.StartNew();
            sw.Start();
            var request = CreateFlurlRequest(url);
            request = _header.Aggregate(request, (current, item) => current.WithHeader(item.Key, item.Value));

            var cancellationToken = default(CancellationToken);
            var completionOption = HttpCompletionOption.ResponseContentRead;
            var capturedJsonContent = new CapturedJsonContent(data);
            var response = request
                .SendAsync(HttpMethod.Post, capturedJsonContent, cancellationToken, completionOption)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            var result = response.ResponseMessage.Content.ReadAsStringAsync().Result;

            var returnObj = JsonConvert.DeserializeObject<TResult>(result);
            _header.Clear();
            sw.Stop();

            _logger.LogInformation($"Cost: {sw.ElapsedMilliseconds}, {loggerContent}");
            return (Result: returnObj, response.ResponseMessage.StatusCode);
        }

        private async Task<(Stream ContentStream, HttpStatusCode StatusCode)> GetDataAsync(string url, string query)
        {
            var sw = Stopwatch.StartNew();
            var urlWithQuery = $"{url}?{query}";
            var request = CreateFlurlRequest(urlWithQuery);
            request = _header.Aggregate(request, (current, item)
                => current.WithHeader(item.Key, item.Value));

            if (request.Headers.Any())
            {
                _logger.LogInformation("Headers:"
                    + JsonConvert.SerializeObject(request.Headers.ToDictionary(x => x.Name, x => x.Value)));
            }

            var response = await request.GetAsync();
            _header.Clear();

            LogContext.PushProperty("elapsed", sw.Elapsed.TotalMilliseconds);
            LogContext.PushProperty("url", urlWithQuery);
            _logger.LogInformation(await response.ResponseMessage.Content.ReadAsStringAsync());
            var stream = response.ResponseMessage.Content.ReadAsStreamAsync();

            return (await stream, response.ResponseMessage.StatusCode);
        }


    }

    internal class ContentType
    {
        public const string Text = "text";

        public const string Json = "json";

        public const string UrlEncoded = "x-www-form-urlencoded";

        public const string FormData = "form-data";
    }
}