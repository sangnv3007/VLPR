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
            ///<summary>
            ///Run Apllication Form
            ///</summary>
            ///
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

            ///<summary>
            ///Run Console Command
            ///</summary>

            //var extracter = new NumberPlateExtracter();
            //string path = @"D:\Download Chorme\ImageTest\photo_2022-08-17_10-35-10.jpg";
            //string path2 = @"D:\Download Chorme\ImageTest\photo_2022-08-17_10-35-10.jpg";
            //var img = new Image<Bgr, byte>(path);
            //var img2 = new Image<Bgr, byte>(path2);
            ////Thời gian bắt đầu
            //Stopwatch swObj = new Stopwatch();
            //swObj.Start();
            //ResultLP resultobj = extracter.ProcessImage(img.Mat);
            ////Thời gian kết thúc
            //swObj.Stop();
            //// Thời gian bắt đầu
            //Stopwatch swObj2 = new Stopwatch();
            //swObj2.Start();
            //ResultLP resultobj2 = extracter.ProcessImage(img2.Mat);
            ////Thời gian kết thúc
            //swObj2.Stop();
            ////Tổng thời gian thực hiện               
            //Console.WriteLine(Math.Round(swObj.Elapsed.TotalSeconds, 2).ToString() + " giây");
            //Console.WriteLine("TextPlate: {0}", resultobj.LP);
            //Console.WriteLine("Status: {0}", resultobj.statusLP);
            ////
            //// Tổng thời gian thực hiện
            //Console.WriteLine(Math.Round(swObj2.Elapsed.TotalSeconds, 2).ToString() + " giây");
            //Console.WriteLine("TextPlate: {0}", resultobj2.LP);
            //Console.WriteLine("Status: {0}", resultobj2.statusLP);
        }
    }
}
