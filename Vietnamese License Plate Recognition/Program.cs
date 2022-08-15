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
            string path = @"D:\Download Chorme\GreenParking\GreenParking\taxi.jpg";
            var img = new Image<Bgr, byte>(path);
            ResultLP resultobj = extracter.ProcessImage(img.Mat);
            Console.WriteLine("TextPlate: {0}", resultobj.LP);
            Console.WriteLine("Status: {0}", resultobj.statusLP);
        }
    }
}
