using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SYN.BC.Driver.PLC.MCProtocolDriver;
using SYN.BC.Core.Common;
using SYN.BC.Driver.PLC.Mitsubishi.MappingAnalysis;

namespace MCProtocolDriver
{
    public partial class Test : Form
    {
        private MCProtocol _MCP;

        public Test()
        {
            InitializeComponent();
        }

        private void Test_Load(object sender, EventArgs e)
        {
            try
            {
                var Parameter = new List<ConstructorParameter>();
                Parameter.Add(new ConstructorParameter() { Name = "IP", Value = "192.168.2.102" });
                Parameter.Add(new ConstructorParameter() { Name = "Port", Value = "2005" });

                _MCP = new MCProtocol(Parameter);
                _MCP.Ini();
                bool connect = _MCP.Connect();
                short[] read = new short[1];
                //for (int i = 0; i < 10; i++)
                //{
                //    //_MCP.ReadWordVal("0", 8000, ref read, "ZR");
                //    _MCP.ReadBitVal("0", 5000, ref read, "B");
                //}


                //short[] wriveval = MappingAnalysisUtility.HEXStringToShortArray("010203040506");
                //_MCP.WriteWordVal("0", wriveval.Length, wriveval, "W");

                short[] wriveval = MappingAnalysisUtility.BinStringToShortArray("1011010");
                _MCP.WriteBitVal("0", wriveval.Length,wriveval,"M");
                _MCP.DisConnect();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
