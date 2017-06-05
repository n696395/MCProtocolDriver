using System;
using System.Windows.Forms;

namespace MCProtocolDriver
{
    class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Test());
        }
    }
}
