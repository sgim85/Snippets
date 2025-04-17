using System.Text;
using System.Text.RegularExpressions;

namespace CXP.Utilities
{
    public partial class Utils
    {
        public static string? MaskAllCharsExceptLastN(string str, int n = 0)
        {
            if (string.IsNullOrEmpty(str))
                return null;

            StringBuilder sb = new StringBuilder();

            Char[] charArr = str.ToCharArray();

            for (int i = 0; i < charArr.Length - n; i++)
            {
                sb.Append('*');
            }

            sb.Append(str.Substring(str.Length - n));

            return sb.ToString();
        }
        
        public static string? MaskJsonValues(string json, int maskValueMinLength = 6, List<string>? propertiesToSkip = null)
        {
            if (string.IsNullOrEmpty(json))
                return json;

            try
            {
                var defaultPropsToSkip = new[] { "TransactionId" };
                if (propertiesToSkip == null)
                    propertiesToSkip = new List<string>();

                propertiesToSkip.AddRange(defaultPropsToSkip);

                var pattern = @"""(?<property>[^""]+)""\s*:\s*(?<quote>"")?(?<value>[\w\d\- ]+)(?<quote2>"")?(?=\s*(,|\}))";

                var regex = new Regex(pattern,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace |
                    RegexOptions.Compiled);

                json = regex.Replace(json, match =>
                {
                    var propertyName = match.Groups["property"].Value;
                    var value = match.Groups["value"].Value;

                    if (match.Groups.Count < 3 || propertiesToSkip.Any(p =>
                            string.Equals(p, propertyName, StringComparison.OrdinalIgnoreCase)) ||
                        !Regex.IsMatch(value, @"\d+") ||
                        value.Length <= maskValueMinLength)
                    {
                        return match.Value;
                    }

                    var maskedValue = MaskAllCharsExceptLastN(value, maskValueMinLength);

                    if (!string.IsNullOrWhiteSpace(maskedValue) && maskedValue.Length > 200)
                        maskedValue = $"{maskedValue.Take(200).ToString()}....";

                    return match.Value.Replace(value, maskedValue);
                });
            }
            catch (Exception)
            {
            }

            return json;
        }
    }
}