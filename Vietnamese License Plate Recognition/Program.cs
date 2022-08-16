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
            //string path = @"D:\Download Chorme\10 ngay\2022_07_01\LanVao_1\20220701_072027.637922568287320112.1.AnhBienSo.jpg";
            //var img = new Image<Bgr, byte>(path);
            ////Thời gian bắt đầu
            //Stopwatch swObj = new Stopwatch();
            //swObj.Start();
            //ResultLP resultobj = extracter.ProcessImage(img.Mat);
            ////Thời gian kết thúc
            //swObj.Stop();
            ////Tổng thời gian thực hiện               
            //Console.WriteLine(Math.Round(swObj.Elapsed.TotalSeconds, 2).ToString()+ " giây");
            //Console.WriteLine("TextPlate: {0}", resultobj.LP);
            //Console.WriteLine("Status: {0}", resultobj.statusLP);
        }
    }
}
