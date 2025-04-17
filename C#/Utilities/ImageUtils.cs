using System.Text.RegularExpressions;

namespace CXP.Utilities
{
    public partial class Utils
    {
        public static string ReplaceImageTagUrlWithBase64Image(string htmlBody)
        {
            if (string.IsNullOrEmpty(htmlBody))
                return string.Empty;

            try
            {
                htmlBody = Regex.Replace(htmlBody, "(?<=\\<img\\s)src=\"(\\s*https:\\/\\/cxp\\.mgcs.+?)\"", eval => {
                    if (eval.Groups.Count > 1)
                    {
                        var imageUrl = eval.Groups[1].Value;
                        var httpClient = new HttpClient();
                        byte[] fileBytes = httpClient.GetByteArrayAsync(imageUrl).GetAwaiter().GetResult();
                        string base64Img = Convert.ToBase64String(fileBytes);
                        return $"src=\"data:image/png;base64, {base64Img}\"";
                    }
                    return eval.Groups[0].Value;
                });
            }
            catch (Exception ex){}
            return htmlBody;
        }
    }
}
