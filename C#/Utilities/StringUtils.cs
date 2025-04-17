using System.Text.RegularExpressions;

namespace CXP.Utilities
{
    public partial class Utils
    {
        public static string? RemoveSpacesAndLineBreaks(string text)
        {
            if (text == null)
                return text;

            return text.Trim().Replace(" ", "").ReplaceLineEndings(""); ;
        }

        public static string? RemoveLineBreaks(string text)
        {
            if (text == null)
                return text;

            return text.Trim().ReplaceLineEndings(""); ;
        }

        public static string RemoveDashesAndSpaces(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            return str.Replace("-", "").Replace(" ", "");
        }

        public static bool AreEqualAlphanumerically(string str1, string str2)
        {
            if (str1 == null || str2 == null)
                return false;

            return RemoveDashesAndSpaces(str1).ToLower().Trim() == RemoveDashesAndSpaces(str2).ToLower().Trim();
        }

        /// <summary>
        /// This function is only to be used for logging functions, e.g. app insights.
        /// It trims the text size of a very large property value in the source json. E.g. base64 string representation of a file.
        /// </summary>
        /// <param name="inputJson">input json</param>
        /// <returns>trimed json</returns>
        public static string TrimLargeJsonPropertyValue(string inputJson)
        {
            if (string.IsNullOrEmpty(inputJson))
                return inputJson;

            inputJson = inputJson.Replace(" ", "").Replace(Environment.NewLine, "");

            // In the json the Regex matches ("PROPERTY_NAME":")(PROPERTY_VALUE_first_200_chars)(PROPERTY_VALUE_remaining_chars)("closing quote)
            // That's Group 1, 2, 3, 4. Ditch group 3 (Keep only first 600 chars of the very large property value)
            var rgx = "(\"[\\w\\-_]+\"\\s*:\\s*\")(.{600})(.+)(\")";
            var newTrimedJson = Regex.Replace(inputJson, rgx, match => {
                if (match.Groups.Count == 5)
                {
                    var newMatch = $"{match.Groups[1].Value}{match.Groups[2].Value}........{match.Groups[4].Value}";
                    return newMatch;
                }
                return match.Value;
            }, RegexOptions.IgnoreCase);

            return newTrimedJson;
        }
    }
}