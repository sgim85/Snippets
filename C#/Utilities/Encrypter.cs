using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace CXP.Utilities
{
    public partial class Utils
    {
        private static string AES_KEY_BASE64 = "BJWOqbvlKdd/8QbT4Y1oJd09NhhZfM5sD5BHVBttzIY=";
        private static string AES_VECTOR_BASE64 = "XxLeezFxrQD+sWAWTuc62w=="; // IV
        private static int timeLimitMinutes = 30;
        private static string dateTimeFormatUTC = "yyyyMMdd HH:mm";

        public static string EncryptAndFormatInBase64(string input, bool isTimeLimited = false)
        {
            using (var aes = Aes.Create())
            {
                byte[] encrypted;

                aes.Key = Convert.FromBase64String(AES_KEY_BASE64);
                aes.IV = Convert.FromBase64String(AES_VECTOR_BASE64);

                if (isTimeLimited)
                {
                    input = $"{input}CURRENT_TIME={DateTime.UtcNow.ToString(dateTimeFormatUTC)}";
                }

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (var msEncrypt = new MemoryStream())
                {
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (var swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(input);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }

                // Base64UrlTextEncoder encodes/decodes special characters that may cause issues in url strings. Better than Convert.ToBase64String
                return Base64UrlTextEncoder.Encode(encrypted);
                //return Convert.ToBase64String(encrypted);
            }
        }

        public static string? DecryptFromBase64String(string input, bool isTimeLimited = false)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    string? plaintext = null;

                    // Base64UrlTextEncoder encodes/decodes special characters that may cause issues in url strings. Better than Convert.FromBase64String
                    var cipherText = Base64UrlTextEncoder.Decode(input);
                    //var cipherText = Convert.FromBase64String(input);

                    aes.Key = Convert.FromBase64String(AES_KEY_BASE64);
                    aes.IV = Convert.FromBase64String(AES_VECTOR_BASE64);

                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (var msDecrypt = new MemoryStream(cipherText))
                    {
                        using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (var srDecrypt = new StreamReader(csDecrypt))
                            {
                                plaintext = srDecrypt.ReadToEnd();
                            }
                        }
                    }

                    if (isTimeLimited)
                    {
                        var pattern = @"^(.+)(CURRENT_TIME=)(.+)$";
                        var match = System.Text.RegularExpressions.Regex.Match(plaintext, pattern);
                        if (match.Groups.Count < 4)
                            return null;

                        if (!DateTime.TryParseExact(match.Groups[3].Value, dateTimeFormatUTC, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                            return null;

                        if (DateTime.UtcNow.Subtract(dt).Minutes > timeLimitMinutes)
                            return null;

                        // cipher is still valid.
                        plaintext = match.Groups[1].Value;
                    }

                    // Remove the current_time tag that is postfixed to filename in the EncryptAndFormatInBase64() function 
                    plaintext = Regex.Replace(plaintext, "CURRENT_TIME=.+?(?=(&.+?)?)$", "");

                    return plaintext;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}