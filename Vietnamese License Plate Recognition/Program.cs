using Emgu.CV;
using Emgu.CV.Structure;
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
            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new Form1());

            var extracter = new NumberPlateExtracter();
            string path = @"D:\Download Chorme\ImageTest\xe-ngoai-giao-1_bezk.jpg";
            //var img = new Image<Bgr, byte>(path);
            ResultLP resultobj = extracter.ProcessImage(path);
            string textplate = resultobj.textplate();
            string status = resultobj.status();
            Console.WriteLine("TextPlate: {0}",textplate);
            Console.WriteLine("Status: {0}", status);
        }
    }
}
