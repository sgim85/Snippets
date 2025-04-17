namespace CXP.Utilities
{
    public partial class Utils
    {
        public static string RemoveNonNumericCharactersFromPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return phoneNumber;

            return phoneNumber.Trim().Replace(" ", "").Replace(@"/", "").Replace(@"-", "").Replace("(", "").Replace(")", "");
        }
    }
}
