using System;
using System.Text;

namespace CXP.Utilities
{
    public partial class Utils
    {
        static Object lockObj = new Object();
        private static long RandomeNumberRequestCounter = new Random().Next(1, 99);

        /// <summary>
        /// Generates a random numeric string from DateTime ticks + request counter (utilizing Interlocked.Increment to statically maintain counter)
        /// </summary>
        /// <param name="length">Length of random string</param>
        /// <returns></returns>
        public static string GenerateRandomNumberString(int length)
        {
            lock (lockObj)
            {
                var ticks = DateTime.UtcNow.Ticks.ToString();

                ticks += RandomeNumberRequestCounter;
                ticks += Thread.CurrentThread.ManagedThreadId;

                // Source: https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked.increment?view=net-7.0
                Interlocked.Increment(ref RandomeNumberRequestCounter);

                int startIndex = ticks.Length - length;

                var result = ticks.Substring(startIndex);

                // Upstream conversion to long will be messed up with a '0' as first char. So replace it.
                if (result[0] == '0')
                {
                    var r = new Random();
                    result = result.Substring(1).Insert(0, r.Next(1, 9).ToString());
                }

                return result;
            }
        }

        /// <summary>
        /// Generates a random numeric string from DateTime ticks + optional left or right padding.
        /// </summary>
        /// <param name="length">Length of random string</param>
        /// <param name="leftPadding">String to pad to the left of DateTime Ticks prior to substring using length</param>
        /// <param name="rightPadding">String to pad to the right of DateTime Ticks prior to substring using length</param>
        /// <returns></returns>
        public static string GenerateRandomNumberString(int length, string? leftPadding = null, string? rightPadding = null)
        {
            var ticks = DateTime.UtcNow.Ticks.ToString();

            if (leftPadding != null) 
                ticks = leftPadding + ticks;

            if (rightPadding != null)
                ticks = ticks + rightPadding;

            int startIndex = ticks.Length - length;

            return ticks.Substring(startIndex);
        }
    }
}
