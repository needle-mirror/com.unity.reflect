namespace Unity.Labs.Utils
{
    public static class StringExtensions
    {
        /// <summary>
        /// Capitalizes the first letter of a string
        /// </summary>
        /// <param name="str">String to be capitalized</param>
        /// <returns></returns>
        public static string FirstToUpper(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            if (str.Length == 1)
                return char.ToUpper(str[0]).ToString();
            
            return char.ToUpper(str[0]) + str.Substring(1);
        }
    }
}
