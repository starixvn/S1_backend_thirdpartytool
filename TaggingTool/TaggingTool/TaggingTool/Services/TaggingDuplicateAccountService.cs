using Newtonsoft.Json;
using TaggingTool.Core.Models.Customer;
using TaggingTool.Core.Models.Enums;
using TaggingTool.Core.Service;

namespace TaggingTool.Services;

public class TaggingDuplicateAccountService
{
    private readonly CustomerService _customerService;
    private readonly ILogger<TaggingDuplicateAccountService> _logger;
    
    public TaggingDuplicateAccountService(IServiceProvider provider)
    {
        _customerService = provider.GetRequiredService<CustomerService>();
        _logger = provider.GetRequiredService<ILogger<TaggingDuplicateAccountService>>();
    }
    
    public async Task<bool> DoTagging(SiteEnum site)
    {
        _logger.LogInformation($"{DateTime.Now:HH:mm:ss} || DoTagging [{site}] Start...");
        var failedCnt = 0;
        
        try
        {
                // step 1: 撈帳戶名
                var accountNameList = await _customerService.GetAllAccountNameList((int)site);
                _logger.LogInformation($"{DateTime.Now:HH:mm:ss} || Get {site} AccountNameList");
                
                // step 2: 比對
                foreach (var account in accountNameList)
                {
                    if (account.IsDuplicate || string.IsNullOrWhiteSpace(account.AccountName))
                        continue;
                    
                    var patterns = new[]
                    {
                        account.AccountName,
                        account.AccountName.Replace(" ", string.Empty)
                    };
                    
                    var isDuplicate = accountNameList.Any(x => x.CustId != account.CustId
                        && patterns.Any(y => y.Equals(x.AccountName, StringComparison.CurrentCultureIgnoreCase)));

                    if (isDuplicate)
                    {
                        var duplicate = accountNameList
                            .Where(x => !x.IsDuplicate
                                        && patterns.Any(y => y.Equals(x.AccountName, StringComparison.CurrentCultureIgnoreCase)));
                        
                        foreach (var d in duplicate)
                        {
                            d.IsDuplicate = true;
                        
                            Console.WriteLine($"({d.CustId}) {d.AccountName} is duplicate");
                        }
                    }
                }
                
                var duplicateCustList = accountNameList.Where(x => x.IsDuplicate)
                    .Select(x => x.CustId)
                    .ToList();
                
                _logger.LogInformation($"{DateTime.Now:HH:mm:ss} || DuplicateCustList: {duplicateCustList.Count}");
                
                // step 3: 更新 TagList
                var idx = 0;
                const int cnt = 1000;
                while (idx * cnt < duplicateCustList.Count)
                {
                    var tagList = new List<CustTagList>();
                    var custList = duplicateCustList.Skip(idx * cnt)
                        .Take(cnt)
                        .ToList();
                    _logger.LogInformation($"Processing: {idx * cnt + custList.Count}/{duplicateCustList.Count}...");
                    
                    try
                    {
                        // 取出用戶的 TagList
                        var custTagList = await _customerService.GetCustTagList(custList);
                        foreach (var custTag in custTagList)
                        {
                            var json = JsonConvert.DeserializeObject<List<int>>(custTag.StrTagList ?? "[]");
                            if (!json.Contains((int) TagEnum.Duplicate))
                            {
                                json.Add((int) TagEnum.Duplicate);
                                tagList.Add(new CustTagList
                                {
                                    CustId = custTag.CustId,
                                    TagList = json
                                });
                            }
                        }

                        _logger.LogInformation($"Processing tagging users: {JsonConvert.SerializeObject(tagList.Select(x=>x.CustId))}");
                        // 壓 tag
                        var result = await _customerService.SetCustInfoTagList(tagList);

                        _logger.LogInformation($"tagging users: {result}");
                        Thread.Sleep(200);
                    }
                    catch (Exception ex)
                    {
                        failedCnt += custList.Count;
                        _logger.LogError(ex, ex.Message,
                            $"{DateTime.Now:HH:mm:ss} || Exception when update tag, customers: {JsonConvert.SerializeObject(custList)}");
                    }
                    
                    idx++;
                }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message, $"{DateTime.Now:HH:mm:ss} || DoTagging Exception.");
            return false;
        }

        _logger.LogInformation($"{DateTime.Now:HH:mm:ss} || DoTagging End, Failed Count: {failedCnt}.");
        return true;
    }
}