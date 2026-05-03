public class PublicIpFetcher
{
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
                    httpClient.Timeout = TimeSpan.FromSeconds(3);
                    var response = await httpClient.GetStringAsync(service);
                    return response.Trim();
                }
                catch
                {
                    continue;
                }
            }
        }
        throw new Exception("All IP services failed");
    }
}