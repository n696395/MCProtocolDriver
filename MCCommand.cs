using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SYN.BC.Driver.PLC.MCProtocolDriver
{
    public class MCCommand
    {
        //Command

        /// <summary>
        /// 成批讀取
        /// </summary>
        public const string BatchRead = "0401";

        /// <summary>
        /// 成批寫入
        /// </summary>
        public const string BatchWrite = "1401";

        /// <summary>
        /// 隨機讀取
        /// </summary>
        public const string RandomRead = "0403";

        /// <summary>
        /// 隨機寫入
        /// </summary>
        public const string RandomWrite = "1402";

        //Sub Command
        public const string Word = "0000";
        public const string Bit = "0001";

        public enum CommunicationMode
        {
            Binary,
            ASCII,
        }
    }
}
