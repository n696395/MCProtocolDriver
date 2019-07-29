using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SYN.BC.Core.Common;
using System.Net.Sockets;
using System.Net;
using SYN.BC.Core.Log;
using System.Net.NetworkInformation;
using static SYN.BC.Driver.PLC.Utility;
using static SYN.BC.Core.Common.Constant;
using System.Threading;

namespace SYN.BC.Driver.PLC.MCProtocolDriver
{
    public class MCProtocol : AbstractPLCDriver
    {
        private Socket _SocketTCP;
        private IPEndPoint _TargetIPPort;
        private Task _task;
        private string _IP;
        private string _Port;
        private List<int> _PortList = new List<int>();
        private Logger _Logger;
        private Logger _DMLog;
        private ushort _ReconnectTime = 1000;

        private string _BasicFormat = "5000"; //ASCII Header(3E)
        private string _SubBasicFormat = "03FF00"; //請求目標模組 I/O 編號[03FF]  + 請求目標模組站號[00]
        private string _CPU_TimerStr = "0010";
        private string _NetNo="00";
        private string _PCNo = "FF";

        private const short _MAX_BIT_RW_POINT_ASCII = 3584; //ASCII模式Bit最大讀寫數量
        private const short _MAX_BIT_RW_POINT_BINARY = 7168; //Binary模式Bit最大讀寫數量
        private const short _MAX_WORD_RW_POINT = 960; //Word最大讀寫數量

        private MCCommand.CommunicationMode _Mode = MCCommand.CommunicationMode.ASCII;

        /// <summary>
        /// 建構MCProtocol
        /// </summary>
        /// <param name="para">參數</param>
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

            _PortList = _Port.Split(',').Select(x => Convert.ToInt32(x)).ToList();
            _Port = _PortList.First().ToString();

            string NetNo = (from p in para
                            where p.Name == "NetNo"
                            select p.Value).FirstOrDefault();
            if (!string.IsNullOrEmpty(NetNo)) { _NetNo = NetNo; }

            string PCNo = (from p in para
                            where p.Name == "PCNo"
                           select p.Value).FirstOrDefault();
            if (!string.IsNullOrEmpty(NetNo)) { _PCNo = PCNo; }
        }

        ~MCProtocol()
        {
            if (_SocketTCP != null && _SocketTCP.Connected) { Close(); }
        }

        /// <summary>
        /// 
        /// </summary>
        public override bool IsConnected
        {
            get
            {
                if (_SocketTCP != null)
                    return _SocketTCP.Connected;
                else
                    return false;
            }
        }

        /// <summary>
        /// Connect
        /// </summary>
        /// <returns>Result</returns>
        public override bool Connect()
        {
            try
            {
                _SocketTCP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _SocketTCP.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, new LingerOption(false, 1));
                _SocketTCP.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 2500);
                _TargetIPPort = new IPEndPoint(IPAddress.Parse(_IP), int.Parse(_Port));
                //_SocketTCP?.Connect(_TargetIPPort);

                IAsyncResult result = _SocketTCP.BeginConnect(_TargetIPPort, null, null);
                result.AsyncWaitHandle.WaitOne(1000, true);//只等1秒

                if (!result.IsCompleted)
                {
                    DisConnect();
                }
                //else if (_SocketTCP.Connected == true)
                //{

                //}

                //Start reconnect function
                if (_task.Status != TaskStatus.Running)
                    _task.Start();

                return _SocketTCP.Connected;
            }
            catch (Exception ex)
            {
                _Logger.Error(ex);
                return false;
            }
        }

        /// <summary>
        /// DisConnect
        /// </summary>
        /// <returns>Result</returns>
        public override bool DisConnect()
        {
            try
            {
                //_SocketTCP?.Shutdown(SocketShutdown.Both);
                _SocketTCP?.Close();

                //_TcpClient?.Close();
                //_TcpClient = new TcpClient(AddressFamily.InterNetwork);
                return true;
            }
            catch(ObjectDisposedException)
            {
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
                _Logger = new Logger(nameof(MCProtocol));
                _DMLog = new Logger("DMLoger");

                //Reconnect Function
                _task = new Task(() =>
                {
                    while (true)
                    {
                        try
                        {
                            if (_SocketTCP != null && (!_SocketTCP.Connected || !PingIP(_IP)))
                            {
                                _Port = Utility.getPortNo(_Port, _PortList).ToString();
                                _Logger.Warn(new LogMessage("", nameof(MCProtocol)) { Message = $"Reconnecting..., Port:{_Port}" });
                                DisConnect();
                                Connect();
                            }
                        }
                        catch (Exception ex)
                        {
                            _Logger.Error(ex);
                        }
                        System.Threading.SpinWait.SpinUntil(() => false, _ReconnectTime);
                    }
                });

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Not Implemented
        /// </summary>
        /// <param name="Msg"></param>
        /// <returns></returns>
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
                if (_SocketTCP == null)
                    return -1;

                int Address = int.Parse(StartAddress);
                int ReadAddress = Address;
                string ReadString = "";
                string Command = "";
                int ByteRead = -1;
                do
                {
                    Command = CreateCommandString(MCCommand.BatchRead, MCCommand.Bit, Name, ReadAddress.ToString(), _MAX_BIT_RW_POINT_ASCII);
                    ReadAddress += _MAX_BIT_RW_POINT_ASCII;
                    SendDataByte = Encoding.ASCII.GetBytes(Command);
                    _SocketTCP.Send(SendDataByte, SendDataByte.Length, SocketFlags.None);
                    ByteRead = _SocketTCP.Receive(RecvDataByte, RecvDataByte.Length, SocketFlags.None);
                    if (ByteRead > 0)
                    {
                        string RecvStr = Encoding.ASCII.GetString(RecvDataByte, 0, ByteRead);
                        string CompleteCode = Utility.GetCompleteCode(RecvStr);
                        if (CompleteCode != "0000") { return Convert.ToInt32(CompleteCode, 16); }
                        ReadString += RecvStr.Substring(22);
                    }
                    else
                    {
                        _Logger.Error(new LogMessage("", nameof(MCProtocol)) { Message = $"Read Bit fail, name={Name}, Address={StartAddress}, Size={Size}" });
                        return -1;
                    }
                } while (ReadAddress < (Address + Size));
                Value = Enumerable.Range(0, Size).Select(x => short.Parse(ReadString[x].ToString())).ToArray();
                return 0;
            }
            catch (Exception ex)
            {
                _Logger.Error(ex);
                throw ex;
            }
        }

        /// <summary>
        /// Not Implemented
        /// </summary>
        /// <param name="StartAddress"></param>
        /// <param name="Size"></param>
        /// <param name="Value"></param>
        /// <param name="Name"></param>
        /// <returns></returns>
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
                string Command = "";
                int ByteRead = -1;
                //do
                //{
                Command = CreateCommandString(MCCommand.BatchRead, MCCommand.Word, Name, ReadAddress.ToString(), Size);
                SendDataByte = Encoding.ASCII.GetBytes(Command);
                _SocketTCP?.Send(SendDataByte, SendDataByte.Length, SocketFlags.None);
                Thread.Sleep(1);
                ByteRead = _SocketTCP.Receive(RecvDataByte, RecvDataByte.Length, SocketFlags.None);
                if (ByteRead>0)
                {
                    string RecvStr = Encoding.ASCII.GetString(RecvDataByte, 0, ByteRead);
                    string CompleteCode = Utility.GetCompleteCode(RecvStr);
                    if (CompleteCode != "0000") { return Convert.ToInt32(CompleteCode, 16); }
                    ReadString += RecvStr.Substring(22);
                }
                else
                {
                    _Logger.Error(new LogMessage("", nameof(MCProtocol)) { Message = $"Read Word fail, name={Name}, Address={StartAddress}, Size={Size}" });
                    return -1;
                }
                //ReadAddress += _MAX_WORD_RW_POINT;
                //} while (ReadAddress < (Address + Size));

                Value = HEXStringToShortArray(ReadString).Take(Size).ToArray();
                return 0;
            }
            catch(SocketException sex)
            {
                _Logger.Info(new LogMessage("", nameof(MCProtocol)) { Direct = emDirect.H2E, Message = sex.ToString() });
                return -1;
            }
            catch (Exception ex)
            {
                _Logger.Error(ex);
                throw ex;
            }                
        }

        /// <summary>
        /// Not Implemented
        /// </summary>
        /// <param name="StartAddress"></param>
        /// <param name="Size"></param>
        /// <param name="Value"></param>
        /// <param name="Name"></param>
        /// <returns></returns>
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
                if (_SocketTCP == null)
                    return -1;

                int Address = int.Parse(StartAddress);
                var WriteAry = Value.Split(_MAX_BIT_RW_POINT_ASCII);
                var QArray = WriteAry.Select((val, idx) => new { Index = idx, Value = val });//產生index
                foreach (var WAry in QArray)
                {
                    string WriteAddress = (Address + (WAry.Index * _MAX_BIT_RW_POINT_ASCII)).ToString();
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
                    {
                        _Logger.Error(new LogMessage("", nameof(MCProtocol)) { Message = $"Write Bit fail, name={Name}, Address={StartAddress}, Size={Size}" });
                        return -1;
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                _Logger.Error(ex);
                throw ex;
            }
        }

        /// <summary>
        /// Not Implemented
        /// </summary>
        /// <param name="StartAddress"></param>
        /// <param name="Size"></param>
        /// <param name="Value"></param>
        /// <param name="Name"></param>
        /// <returns></returns>
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

                int Address = (int)new System.ComponentModel.Int32Converter().ConvertFromString(StartAddress);
                var WriteAry = Value.Split(_MAX_WORD_RW_POINT);
                var QArray = WriteAry.Select((val, idx) => new { Index = idx, Value = val });//產生index
                foreach (var WAry in QArray)
                {
                    string WriteAddress = (Address + (WAry.Index * _MAX_WORD_RW_POINT)).ToString();
                    string Command = CreateCommandString(MCCommand.BatchWrite, MCCommand.Word, Name, WriteAddress, WAry.Value.Count(), WAry.Value.ToArray());
                    WriteByte = Encoding.ASCII.GetBytes(Command);
                    _SocketTCP?.Send(WriteByte, WriteByte.Length, SocketFlags.None);
                    Thread.Sleep(1);
                    int RtnByte = _SocketTCP.Receive(RecvDataByte, RecvDataByte.Length, SocketFlags.None);
                    if (RtnByte > 0)
                    {
                        string RecvStr = Encoding.ASCII.GetString(RecvDataByte, 0, RtnByte);
                        string CompleteCode = Utility.GetCompleteCode(RecvStr);
                        if (CompleteCode != "0000") { return Convert.ToInt32(CompleteCode, 16); }
                    }
                    else
                    {
                        _Logger.Error(new LogMessage("", nameof(MCProtocol)) { Message = $"Write Word fail, name={Name}, Address={StartAddress}, Size={Size}" });
                        return -1;
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                _Logger.Error(ex);
                throw ex;
            }
        }

        /// <summary>
        /// Not Implemented
        /// </summary>
        /// <param name="StartAddress"></param>
        /// <param name="Size"></param>
        /// <param name="Value"></param>
        /// <param name="Name"></param>
        /// <returns></returns>
        public override Task<int> WriteWordValAsync(string StartAddress, int Size, short[] Value, string Name = "")
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// ASCII Mode
        /// </summary>
        /// <param name="command"></param>
        /// <param name="subcommand"></param>
        /// <param name="DeviceName"></param>
        /// <param name="StartAddress"></param>
        /// <param name="Size"></param>
        /// <param name="WriteVal"></param>
        /// <returns>Command String</returns>
        private string CreateCommandString( string command, string subcommand, string DeviceName, string StartAddress, int Size, short[] WriteVal = null)
        {
            string Address;
            string Name = DeviceName.PadRight(2, '*');

            if (Name == "X*" || Name == "Y*" || Name == "B*" || Name == "W*" || Name == "SB" || Name == "SW" || Name == "DX" || Name == "DY" || Name == "ZR")
                Address = int.Parse(StartAddress).ToString("X").PadLeft(6, '0');
            else
                Address = int.Parse(StartAddress).ToString().PadLeft(6, '0');

            string Tmp = _CPU_TimerStr +
                        command +
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

        /// <summary>
        /// Connection status
        /// </summary>
        /// <returns></returns>
        public override bool CheckStatus()
        {
            if (_SocketTCP != null)
                return _SocketTCP.Connected;
            else
                return false;
        }

        /// <summary>
        /// Close connection
        /// </summary>
        public override void Close()
        {
            DisConnect();
        }

        /// <summary>
        /// Use Ping to check connection
        /// </summary>
        /// <param name="IP">IP Address</param>
        /// <returns></returns>
        private bool PingIP(string IP)
        {
            IPAddress tIP = IPAddress.Parse(IP);
            Ping tPingControl = new Ping();
            PingReply tReply = tPingControl.Send(tIP, 100);
            tPingControl.Dispose();
            
            if (tReply.Status != IPStatus.Success)
            {
                _Logger.Info(new LogMessage("", nameof(MCProtocol)) { Message = $"Ping {IP} {tReply.Status.ToString()}" });
                return false;
            }
            else
                return true;
        }

        /// <summary>
        /// Write Data To PLC
        /// </summary>
        /// <param name="Data"></param>
        /// <param name="dataList"></param>
        /// <returns>Error Code</returns>
        public override int WriteData(TransferMsg Data, List<TagMessagePLC> dataList)
        {
            int returncode = -1;
            foreach (TagMessagePLC taginfo in dataList)
            {
                if (taginfo.IsBit)
                    returncode = WriteBitVal(taginfo.Offset, taginfo.Size, taginfo.RawData, taginfo.DeviceName);
                else
                    returncode = WriteWordVal(taginfo.Offset, taginfo.Size, taginfo.RawData, taginfo.DeviceName);

                if (returncode == 0)
                {
                    DMLogMessage _DM = new DMLogMessage(new List<TagMessagePLC> { taginfo }.ToArray(), Data, nameof(MCProtocol)) { Direct = emDirect.H2E };
                    _DMLog.Info(_DM);
                }
                else
                {
                    string[] arValueHex = Enumerable.Range(0, taginfo.RawData.Length).Select(x => taginfo.RawData[x].ToString("X4")).ToArray();
                    LogMessage LM = new LogMessage(Data, nameof(MCProtocol)) { EqpID = Data.EqpID, Direct = emDirect.H2E };
                    LM.Message = $"Write Data Error! code:0x{returncode.ToString("X")} Device={taginfo.DeviceName },Size={taginfo.Size},DeviceValue={string.Join(" ", arValueHex)}";
                    _Logger.Error(LM);
                }
            }

            return returncode;
        }
    }
}
