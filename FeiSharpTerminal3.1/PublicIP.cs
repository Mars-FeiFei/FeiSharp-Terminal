public class PublicIpFetcher
{
    // 可用的公共IP查询服务列表
    private static readonly string[] IpServices = new[]
    {
        "https://api.ipify.org",
        "https://ipinfo.io/ip",
        "https://checkip.amazonaws.com",
        "https://icanhazip.com"
    };

    public static async Task<string> GetPublicIpAsync()
    {
        using (var httpClient = new HttpClient())
        {
            foreach (var service in IpServices)
            {
                try
                {
                    // 设置超时时间（3秒）
                    httpClient.Timeout = TimeSpan.FromSeconds(3);
                    var response = await httpClient.GetStringAsync(service);
                    return response.Trim();
                }
                catch
                {
                    // 如果当前服务失败，尝试下一个
                    continue;
                }
            }
        }
        throw new Exception("All IP services failed");
    }
}