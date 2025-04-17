using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Text.RegularExpressions;

namespace CXP.Utilities
{
    public partial class Utils
    {
        public string GetClientIPAddress()
        {
            string clientIpAddress = string.Empty;

            // With a load balancer in the AKS env, get actual IP from this header
            var ipHeader = _httpContextAccessor?.HttpContext?.Request?.Headers["X-Original-Forwarded-For"];

            if (ipHeader.HasValue && !StringValues.IsNullOrEmpty(ipHeader.Value))
            {
                var parts = ipHeader.Value.ToString().Split(',');
                var ipv4 = parts.FirstOrDefault(p => p.Contains('.'))?.Trim();
                var ipv6 = parts.FirstOrDefault(p => p.Contains(':') && !p.Contains('.'))?.Trim();

                if (!string.IsNullOrEmpty(ipv4))
                {
                    if (ipv4.Contains(":")) // IPv4 with port number. Remove port number
                    {
                        ipv4 = Regex.Replace(ipv4, ":\\d+\\s*$", "");
                    }

                    clientIpAddress = ipv4;
                }
                else if (!string.IsNullOrEmpty(ipv6))
                {
                    clientIpAddress = ipv6;
                }
            }
            else
            {
                var ip = _httpContextAccessor?.HttpContext.Connection.RemoteIpAddress;

                if (ip != null)
                    clientIpAddress = ip.MapToIPv4().ToString();
            }

            _logger.LogInformation($"Client IP ={clientIpAddress}");

            return clientIpAddress;
        }

        public static async Task<bool> UrlIsValid(string downloadUrl)
        {
            bool isTDLExists = false;
            try
            {
                using (var _httpClient = new HttpClient())
                {
                    byte[] fileBytes = await _httpClient.GetByteArrayAsync(downloadUrl);
                    if (fileBytes.Any())
                        isTDLExists = true;
                }
            }
            catch (Exception)
            {
            }

            return isTDLExists;
        }
    }
}