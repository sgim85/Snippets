namespace CXP.Utilities
{
    public partial class Utils
    {
        public static string PostalCodeAddSpace(string str)
        {
            if (string.IsNullOrEmpty(str) || str.Length != 6)
                return str;

            return str.Substring(0, 3) + " " + str.Substring(3);
        }

        public static string PostalCodeRemoveSpaceAndDashes(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            return str.Replace("-", "").Replace(" ", "").ToUpper();
        }
    }
}
