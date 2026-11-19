using System;
using System.Threading;
using System.Windows.Forms;

namespace Eslatmachi
{
    internal static class Program
    {
        private static Mutex singleInstance;

        [STAThread]
        private static void Main()
        {
            bool isFirstInstance;
            singleInstance = new Mutex(true, "Local\\Eslatmachi.Desktop.SingleInstance", out isFirstInstance);
            if (!isFirstInstance)
            {
                MessageBox.Show(
                    "Eslatmachi allaqachon ishlab turibdi. Xabarnomalar panelidagi ikonkasini ikki marta bosing.",
                    "Eslatmachi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            GC.KeepAlive(singleInstance);
        }
    }
}
