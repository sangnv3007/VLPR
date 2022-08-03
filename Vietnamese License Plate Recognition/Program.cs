using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

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
            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new Form1());


            var imgHelper = new ImageHelper();
            string path = @"D:\Download Chorme\GreenParking\GreenParking\0110_01653_b.jpg";
            var result = imgHelper.ProcessImage(path);
            Console.WriteLine(result);
        }
    }
}
