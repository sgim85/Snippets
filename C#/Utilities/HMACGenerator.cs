using System.Security.Cryptography;
using System.Text;

namespace CXP.Utilities
{
    public partial class Utils
    {
        private static string HMAC_PrivateKey = "O9VQ9FKxsDaCNFg91tiQz88X9XL8ukHa";

        /// <summary>
        /// Creates a Hash-based message authentication code (HMAC)
        /// </summary>
        /// <param name="input">Data from which to create HMAC</param>
        /// <param name="key">Key to use in the HMAC function</param>
        /// <returns></returns>
        public static string GenerateHMAC(dynamic input)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(HMAC_PrivateKey)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Validates a provied HMAC code by comparing it to a provided input data and key
        /// </summary>
        public static bool ValidateHMAC(string currentHMAC, dynamic input)
        {
            var newCode = GenerateHMAC(input);

            if (newCode == currentHMAC)
                return true;
            
            return false;
        }
    }
}
