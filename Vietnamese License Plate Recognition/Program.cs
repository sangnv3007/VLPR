using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TD.VLPR;

namespace Vietnamese_License_Plate_Recognition
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

            //var extracter = new NumberPlateExtracter();
            //string path = @"D:\Download Chorme\CarTGMT\CarTGMT\test10.jpg";
            //string result = extracter.ProcessImage(path);
            //Console.WriteLine(result);
        }
    }
}
