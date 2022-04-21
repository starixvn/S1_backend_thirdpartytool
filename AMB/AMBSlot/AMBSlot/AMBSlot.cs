using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using MySql.Data.MySqlClient;

namespace AMBSlot
{
    public class AMBSlot
    {
        private const int TpId = 30;
        private readonly Configuration _configuration;
        
        public AMBSlot(Configuration configuration)
        {
            _configuration = configuration;
        }

        public async Task<int> CreateMember(IEnumerable<TpCustInfo> custInfos)
        {
            var apiUrl = $"{_configuration.AmbSetting.ApiUrl}/seamless/create";
            var count = 0;
            foreach (var custInfo in custInfos)
            {
                var param = new
                {
                    username = custInfo.TpUserName,
                    agent = _configuration.AmbSetting.Agent,
                };
                var jsonParam = JsonSerializer.Serialize(param);
                var key = HMACSHA512(jsonParam, _configuration.AmbSetting.SecretKey);

                var handler = CreateHandler();
                using var httpclient = new HttpClient(handler);
                httpclient.DefaultRequestHeaders.Add("x-ambslot-signature", key);
                var content = new StringContent(jsonParam, Encoding.UTF8, "application/json");
                var responseMessage = await httpclient.PostAsync(apiUrl, content);
                var result = await responseMessage.Content.ReadAsStringAsync();
                var commonResponse = JsonSerializer.Deserialize<CommonResponse>(result);
                if (commonResponse.status.code == 0 || commonResponse.status.code == 902)
                {
                    count++;
                    Console.OutputEncoding = Encoding.UTF8;
                    Console.WriteLine(result);
                }
                else
                {
                    count = 0;
                    break;
                }
            }
            return count;
        }
        private string HMACSHA512(string input, string key)
        {
            var salt = Encoding.UTF8.GetBytes(key);
            var HASH_SIZE = 64;
            var ITERATIONS = 1000;
            
            // Generate the hash
            var pbkdf2 = new Rfc2898DeriveBytes(input, salt, ITERATIONS, HashAlgorithmName.SHA512);
            var data = pbkdf2.GetBytes(HASH_SIZE);
            return Convert.ToBase64String(data);
        }
        public IEnumerable<TpCustInfo> GetTpCustInfo()
        {
            var sql = $"SELECT t.* FROM Siebog.TpCustInfo t WHERE TpId={TpId} LIMIT 501";
            using var connection = new MySqlConnection(_configuration.DatabaseConnectionString);
            var result = connection.Query<TpCustInfo>(sql);
            return result;
        }

        private HttpClientHandler CreateHandler()
        {
            var handler = new HttpClientHandler();
            if (!string.IsNullOrEmpty(_configuration.Proxy))
            {
                var proxy = _configuration.Proxy.Split(':');
                handler.UseProxy = true;
                handler.Proxy = new WebProxy(proxy[0], int.Parse(proxy[1]));
            }

            return handler;
        }
    }
}