using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new Form1());

            ///<summary>
            ///Run Console Command
            ///</summary>
            //var extracter = new NumberPlateExtracter();
            //foreach (string file in Directory.EnumerateFiles(@"D:\Download Chorme\Thang8\Thang_8\DataTrain02", "*.jpg"))
            //{
            //    ResultLP resultobj = extracter.ProcessImage(file);
            //    string results = file + "\t" + resultobj.textPlate + "\t" + resultobj.accPlate;
            //    File.AppendAllText("Result.txt", results + "\n");
            //}

            var extracter = new NumberPlateExtracter();
            string path = @"D:\Download Chorme\Thang8\Thang_8\2022_08_24\LanVao_1\20220824_064503.637969203052202361.1.AnhBienSo.jpg";
            var img = new Image<Bgr, byte>(path);
            //Thời gian bắt đầu
            Stopwatch swObj = new Stopwatch();
            swObj.Start();
            ResultLP resultobj = extracter.ProcessImage(img.Mat);
            //Thời gian kết thúc
            swObj.Stop();
            //Tổng thời gian thực hiện               
            Console.WriteLine(Math.Round(swObj.Elapsed.TotalSeconds, 2).ToString() + " giây");
            Console.WriteLine("TextPlate: {0}", resultobj.textPlate);
            Console.WriteLine("Status: {0}", resultobj.statusPlate);
            Console.WriteLine("Accuracy: {0}", resultobj.accPlate);
        }
    }
}
