using System;
using System.Linq;
using System.Text;

namespace SYN.BC.Driver.PLC.MCProtocolDriver
{
    public static class Utility
    {
        /// <summary>
        /// 將byte[] 轉換成MC Protocol的string
        /// </summary>
        /// <param name="byteAry">byte array</param>
        /// <returns>string</returns>
        public static string ByteToMCString(byte[] byteAry)
        {
            StringBuilder sb = new StringBuilder();
            for(int i = 0;i<=byteAry.Length - 1; i++)
            {
                sb.Append((byteAry[i] / 16).ToString("X"));
                sb.Append((byteAry[i] % 16).ToString("X"));
            }
            return sb.ToString();
        }

        public static byte[] HexStringToByteArray(string HexStr)
        {
            return Enumerable.Range(0, HexStr.Length)
                                 .Where(x => x % 2 == 0)
                                 .Select(x => Convert.ToByte(HexStr.Substring(x, 2), 16))
                                 .ToArray();
        }

        /// <summary>Swaps two bytes in a byte array</summary>
        /// <param name="buf">The array in which elements are to be swapped</param>
        /// <param name="i">The index of the first element to be swapped</param>
        /// <param name="j">The index of the second element to be swapped</param>
        public static void SwapBytes(this byte[] buf, int i, int j)
        {
            byte temp = buf[i];
            buf[i] = buf[j];
            buf[j] = temp;
        }

        /// <summary>
        /// 取得MC Protocol回傳的Code
        /// </summary>
        /// <param name="Str">MC Protocol回傳的字串</param>
        /// <returns>Complete Code</returns>
        public static string GetCompleteCode(string Str)
        {
            return Str?.Substring(18, 4);
        }
    }
}
