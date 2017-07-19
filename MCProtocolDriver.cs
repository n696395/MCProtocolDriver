using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SYN.BC.Core.Common;
using System.Net.Sockets;
using System.Net;
using SYN.BC.Driver.PLC.Mitsubishi.MappingAnalysis;
using System.Timers;
using SYN.BC.Core.Log;

namespace SYN.BC.Driver.PLC.MCProtocolDriver
{
    public class MCProtocol : AbstractPLCDriver
    {
        private Socket _SocketTCP;
        private IPEndPoint _TargetIPPort;
        private IPEndPoint _LocalIPPort;
        private string _IP;
        private string _Port;
        private string _LocalIP = "192.168.2.2";
        private string _LocalPort = "2005";
        private Logger _log;

        private string _BasicFormat = "5000"; //ASCII Header(3E)
        private string _SubBasicFormat = "03FF00"; //請求目標模組 I/O 編號[03FF]  + 請求目標模組站號[00]
        private string _CPU_TimerStr = "0010";
        private string _NetNo="00";
        private string _PCNo = "FF";

        private const short _MAX_BIT_RW_POINT = 3584; //Bit
        private const short _MAX_WORD_RW_POINT = 960; //Word

        Timer t = new Timer(5000);//斷線檢查 (5sec)

        public MCProtocol(List<ConstructorParameter> para)
        {
            _IP = (from p in para
                        where p.Name == "IP"
                        select p.Value).FirstOrDefault();
            if (string.IsNullOrEmpty(_IP))
                throw new Exception("[IP can not be empty!]");

            _Port = (from p in para
                   where p.Name == "Port"
                   select p.Value).FirstOrDefault();
            if (string.IsNullOrEmpty(_Port))
                throw new Exception("[Port can not be empty!]");

            string NetNo = (from p in para
                            where p.Name == "NetNo"
                            select p.Value).FirstOrDefault();
            if (!string.IsNullOrEmpty(NetNo)) { _NetNo = NetNo; }

            string PCNo = (from p in para
                            where p.Name == "PCNo"
                           select p.Value).FirstOrDefault();
            if (!string.IsNullOrEmpty(NetNo)) { _PCNo = PCNo; }

            string localIP = (from p in para
                           where p.Name == "LocalIP"
                              select p.Value).FirstOrDefault();
            if (!string.IsNullOrEmpty(localIP)) { _LocalIP = localIP; }

            string localPort = (from p in para
                              where p.Name == "LocalPort"
                              select p.Value).FirstOrDefault();
            if (!string.IsNullOrEmpty(localIP)) { _LocalPort = localPort; }

        }

        ~MCProtocol()
        {
            if (_SocketTCP.Connected) { DisConnect(); }
        }

        public override bool IsConnected
        {
            get
            {
                return _SocketTCP.Connected;
            }
        }

        public override bool Connect()
        {
            try
            {
                _SocketTCP.Connect(_TargetIPPort);
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public override bool DisConnect()
        {
            try
            {
                _SocketTCP.Shutdown(SocketShutdown.Both);
                _SocketTCP.Close();

                _SocketTCP = null;
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Initial
        /// </summary>
        public override void Ini()
        {
            try
            {
                _log = new Logger(nameof(MCProtocol));

                _SocketTCP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _SocketTCP.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, new LingerOption(false, 1));
                _SocketTCP.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 2500);

                _TargetIPPort  = new IPEndPoint(IPAddress.Parse(_IP), int.Parse(_Port));
                _LocalIPPort = new IPEndPoint(IPAddress.Parse(_LocalIP), int.Parse(_LocalPort));
                //_SocketTCP.Bind(_LocalIPPort);
                t.Elapsed += T_Elapsed;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void T_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                //TODO
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public override long PutData(TransferMsg Msg)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 讀取Bit
        /// </summary>
        /// <param name="StartAddress">起始位址</param>
        /// <param name="Size">大小</param>
        /// <param name="Value">讀到的值</param>
        /// <param name="Name">記憶體名稱</param>
        /// <returns></returns>
        public override int ReadBitVal(string StartAddress, int Size, ref short[] Value, string Name = "")
        {
            byte[] SendDataByte;
            byte[] RecvDataByte = new byte[4999];
            try
            {
                if (string.IsNullOrEmpty(Name)) { throw new Exception("Device name can not be empty!"); }

                int Address = int.Parse(StartAddress);
                int ReadAddress = Address;
                string ReadString = "";
                do
                {
                    string Command = CreateCommandString(MCCommand.BatchRead, MCCommand.Bit, Name, ReadAddress.ToString(), _MAX_BIT_RW_POINT);
                    ReadAddress += _MAX_BIT_RW_POINT;
                    SendDataByte = Encoding.ASCII.GetBytes(Command);
                    _SocketTCP.Send(SendDataByte, SendDataByte.Length, SocketFlags.None);
                    int ByteRead = _SocketTCP.Receive(RecvDataByte, RecvDataByte.Length, SocketFlags.None);
                    if (ByteRead > 0)
                    {
                        string RecvStr = Encoding.ASCII.GetString(RecvDataByte, 0, ByteRead);
                        string CompleteCode = Utility.GetCompleteCode(RecvStr);
                        if (CompleteCode != "0000") { return Convert.ToInt32(CompleteCode, 16); }
                        ReadString += RecvStr.Substring(22);
                    }
                    else
                        return -1;
                } while (ReadAddress < (Address + Size));
                Value = Enumerable.Range(0, Size).Select(x => short.Parse(ReadString[x].ToString())).ToArray();
                return 0;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public override Task<int> ReadBitValAsync(string StartAddress, int Size, ref short[] Value, string Name = "")
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 讀取Word
        /// </summary>
        /// <param name="StartAddress">起始位址</param>
        /// <param name="Size">大小</param>
        /// <param name="Value">讀到的值</param>
        /// <param name="Name">記憶體名稱</param>
        /// <returns>return complete code</returns>
        public override int ReadWordVal(string StartAddress, int Size, ref short[] Value, string Name = "")
        {
            byte[] SendDataByte;
            byte[] RecvDataByte= new byte[4999];
            try
            {
                if (string.IsNullOrEmpty(Name)) { throw new Exception("Device name can not be empty!"); }

                int Address = int.Parse(StartAddress);
                int ReadAddress= Address;
                string ReadString = "";
                do
                {
                    string Command = CreateCommandString(MCCommand.BatchRead, MCCommand.Word, Name, ReadAddress.ToString(), _MAX_WORD_RW_POINT);
                    ReadAddress += _MAX_WORD_RW_POINT;
                    SendDataByte = Encoding.ASCII.GetBytes(Command);
                    _SocketTCP.Send(SendDataByte, SendDataByte.Length, SocketFlags.None);
                    int ByteRead = _SocketTCP.Receive(RecvDataByte, RecvDataByte.Length, SocketFlags.None);
                    if (ByteRead > 0)
                    {
                        string RecvStr = Encoding.ASCII.GetString(RecvDataByte, 0, ByteRead);
                        string CompleteCode = Utility.GetCompleteCode(RecvStr);
                        if (CompleteCode != "0000") { return Convert.ToInt32(CompleteCode, 16); }
                        ReadString += RecvStr.Substring(22);
                    }
                    else
                        return -1;
                } while (ReadAddress < (Address + Size));

                Value = MappingAnalysisUtility.HEXStringToShortArray(ReadString).Take(Size).ToArray();
                return 0;
                ////////////////////////////////////////////////////////
                
            }
            catch (Exception ex)
            {
                throw ex;
            }                
        }

        public override Task<int> ReadWordValAsync(string StartAddress, int Size, ref short[] Value, string Name = "")
        {
            throw new NotImplementedException();
        }

        public override long SetData(TransferMsg Msg)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 寫入Bit
        /// </summary>
        /// <param name="StartAddress">起始位址</param>
        /// <param name="Size">寫入大小</param>
        /// <param name="Value">寫入的值</param>
        /// <param name="Name">記憶體名稱</param>
        /// <returns>Return code</returns>
        public override int WriteBitVal(string StartAddress, int Size, short[] Value, string Name = "")
        {
            byte[] WriteByte;
            byte[] RecvDataByte = new byte[4999];
            try
            {
                if (string.IsNullOrEmpty(Name)) { throw new Exception("Device name can not be empty!"); }

                int Address = int.Parse(StartAddress);
                var WriteAry = Value.Split(_MAX_BIT_RW_POINT);
                var QArray = WriteAry.Select((val, idx) => new { Index = idx, Value = val });//產生index
                foreach (var WAry in QArray)
                {
                    string WriteAddress = (Address + (WAry.Index * _MAX_BIT_RW_POINT)).ToString();
                    string Command = CreateCommandString(MCCommand.BatchWrite, MCCommand.Bit, Name, WriteAddress, WAry.Value.Count(), WAry.Value.ToArray());
                    WriteByte = Encoding.ASCII.GetBytes(Command);
                    _SocketTCP.Send(WriteByte, WriteByte.Length, SocketFlags.None);
                    int RtnByte = _SocketTCP.Receive(RecvDataByte, RecvDataByte.Length, SocketFlags.None);
                    if (RtnByte > 0)
                    {
                        string RecvStr = Encoding.ASCII.GetString(RecvDataByte, 0, RtnByte);
                        string CompleteCode = Utility.GetCompleteCode(RecvStr);
                        if (CompleteCode != "0000") { return Convert.ToInt32(CompleteCode, 16); }
                    }
                    else
                        return -1;
                }
                return 0;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public override Task<int> WriteBitValAsync(string StartAddress, int Size, short[] Value, string Name = "")
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 寫入Word
        /// </summary>
        /// <param name="StartAddress">起始位址</param>
        /// <param name="Size">寫入大小</param>
        /// <param name="Value">寫入的值</param>
        /// <param name="Name">記憶體名稱</param>
        /// <returns>Return code</returns>
        public override int WriteWordVal(string StartAddress, int Size, short[] Value, string Name = "")
        {
            byte[] WriteByte;
            byte[] RecvDataByte = new byte[4999];
            try
            {
                if (string.IsNullOrEmpty(Name)) { throw new Exception("Device name can not be empty!"); }

                int Address = int.Parse(StartAddress);
                var WriteAry = Value.Split(_MAX_WORD_RW_POINT);
                var QArray = WriteAry.Select((val, idx) => new { Index = idx, Value = val });//產生index
                foreach (var WAry in QArray)
                {
                    string WriteAddress = (Address + (WAry.Index * _MAX_WORD_RW_POINT)).ToString();
                    string Command = CreateCommandString(MCCommand.BatchWrite, MCCommand.Word, Name, WriteAddress, WAry.Value.Count(), WAry.Value.ToArray());
                    WriteByte = Encoding.ASCII.GetBytes(Command);
                    _SocketTCP.Send(WriteByte, WriteByte.Length, SocketFlags.None);
                    int RtnByte = _SocketTCP.Receive(RecvDataByte, RecvDataByte.Length, SocketFlags.None);
                    if (RtnByte > 0)
                    {
                        string RecvStr = Encoding.ASCII.GetString(RecvDataByte, 0, RtnByte);
                        string CompleteCode = Utility.GetCompleteCode(RecvStr);
                        if (CompleteCode != "0000") { return Convert.ToInt32(CompleteCode, 16); }
                    }
                    else
                        return -1;
                }
                return 0;

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public override Task<int> WriteWordValAsync(string StartAddress, int Size, short[] Value, string Name = "")
        {
            throw new NotImplementedException();
        }

        private string CreateCommandString( string cmooand, string subcommand, string DeviceName, string StartAddress, int Size, short[] WriteVal = null)
        {
            string Address;
            string Name = DeviceName.PadRight(2, '*');

            if (Name == "X*" || Name == "Y*" || Name == "B*" || Name == "W*" || Name == "SB" || Name == "SW" || Name == "DX" || Name == "DY" || Name == "ZR")
                Address = int.Parse(StartAddress).ToString("X").PadLeft(6, '0');
            else
                Address = int.Parse(StartAddress).ToString().PadLeft(6, '0');

            string Tmp = _CPU_TimerStr +
                                    cmooand +
                                    subcommand +
                                    Name +
                                    Address +
                                    Size.ToString("X").PadLeft(4, '0');

            if (WriteVal != null) //Write Command
            {
                if(subcommand == MCCommand.Bit)
                {
                    var TmpAry = WriteVal.Select(x => x == 48 ? (short)0 : (short)1).ToArray(); //轉換 48=0, 49=1
                    Tmp += string.Join("", TmpAry);
                }
                else //Word
                {
                    byte[] Writebyte = WriteVal.SelectMany(BitConverter.GetBytes).ToArray();
                    for (int i = 0; i < Writebyte.Length; i += 2) // big/little endian轉換
                        Writebyte.SwapBytes(i, i + 1);
                    Tmp += Utility.ByteToMCString(Writebyte);
                }
            }

            string SendCMD = _BasicFormat +
                                            _NetNo +
                                            _PCNo +
                                            _SubBasicFormat +
                                            Tmp.Length.ToString("X").PadLeft(4, '0') +
                                            Tmp;
            return SendCMD;
        }

        public override bool CheckStatus()
        {
            return _SocketTCP.Connected;
        }
    }
}
