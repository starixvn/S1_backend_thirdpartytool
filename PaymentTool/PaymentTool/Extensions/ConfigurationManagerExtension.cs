namespace PaymentTool.Extensions;

public static class ConfigurationManagerExtension
{
    public static ConfigurationManager AddRemoteJsonConfig(this ConfigurationManager configuration, IEnumerable<string> remoteUrls)
    {
        if (remoteUrls is null)
        {
            return configuration;
        }

        using var client = new HttpClient();
        Stream result = null;
        foreach (var url in remoteUrls)
        {
            if (string.IsNullOrEmpty(url))
            {
                continue;
            }

            try
            {
                result = client.GetAsync(url).Result.Content.ReadAsStreamAsync().Result;
                break;
            }
            catch { }
        }

        configuration.AddJsonStream(result);
        return configuration;
    }

    public static string[] CheckLocalConfig(this ConfigurationManager configuration, IHostEnvironment environment, string[] args, string[] remoteUrls)
    {
        if (remoteUrls is null)
        {
            return remoteUrls;
        }

        if (!environment.EnvironmentName.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            return remoteUrls;
        }

        //Local環境檢查傳入的CommandLine參數，指定第一個參數作為Group，加在ConfigService後面
        var group = args.Any() ? args[0] : string.Empty;
        for (int i = 0; i < remoteUrls.Length; i++)
        {
            remoteUrls[i] += group;
        }

        return remoteUrls;
    }
}  