namespace ThirdPartyTool.Extensions
{
    public static class ConfigurationManagerExtension
    {
        public static ConfigurationManager AddRemoteJsonConfig(this ConfigurationManager configuration, string remoteUrl)
        {
            if (string.IsNullOrEmpty(remoteUrl))
            {
                return configuration;
            }

            using var client = new HttpClient();
            var result = client.GetAsync(remoteUrl).Result.Content.ReadAsStreamAsync().Result;
            configuration.AddJsonStream(result);
            return configuration;
        }
    }  
}