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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

            ///<summary>
            ///Run Console Command
            ///</summary>
            ///
            //int i = 1;
            //var extracter = new NumberPlateExtracter();
            //foreach (string file in Directory.EnumerateFiles(@"D:\DownloadChorme\crawlLP\Cars", "*.jpg"))
            //{
            //    ResultLP resultobj = extracter.ProcessImage(file);
            //    CvInvoke.Imwrite(@"D:\Thang_8\Data\imgCrop\crawlLP" + i.ToString() + ".jpg", resultobj.imagePlate);
            //    Console.WriteLine("Done file " + i.ToString() + "_" + file);
            //    if (resultobj.textPlate != null)
            //    {
            //        CvInvoke.Imwrite(@"D:\DownloadChorme\crawlLP\Cars\imgCrop\crawlLP" + i.ToString() + ".jpg", resultobj.imagePlate);
            //        i++;
            //        string results = file + "\t" + resultobj.textPlate + "\t" + resultobj.accPlate;
            //        File.AppendAllText("ResultLP.txt", results + "\n");
            //        File.Copy(file, @"D:\Thang_8\Data\AnhNhanDang\TestDLL\" + Path.GetFileName(file));
            //        File.Delete(file);
            //    }
            //}
            ///
            //var extracter = new NumberPlateExtracter();
            //string root = Environment.CurrentDirectory;
            //string path = @"D:\Download Chorme\CarTGMT\CarTGMT\DataLP (707).jpg";
            //var img = new Image<Bgr, byte>(path);
            //Thời gian bắt đầu
            //Stopwatch swObj = new Stopwatch();
            //swObj.Start();
            //ResultLP resultobj = extracter.ProcessImage(img.Mat);
            ////Thời gian kết thúc
            //swObj.Stop();
            //Stopwatch swObj1 = new Stopwatch();
            //swObj1.Start();
            //ResultLP resultobj1 = extracter.ProcessImage(path);
            //Thời gian kết thúc
            //swObj1.Stop();
            //Tổng thời gian thực hiện               
            //Console.WriteLine(Math.Round(swObj.Elapsed.TotalSeconds, 2).ToString() + " giây");
            //Console.WriteLine(Math.Round(swObj1.Elapsed.TotalSeconds, 2).ToString() + " giây");
            //Console.WriteLine("TextPlate    : {0}", resultobj.textPlate);
            //Console.WriteLine("TextPlate    : {0}", resultobj1.textPlate);
            //File.AppendAllText("Result.txt", Math.Round(swObj.Elapsed.TotalSeconds, 2).ToString() + " giây" + "\n" +
            //Math.Round(swObj1.Elapsed.TotalSeconds, 2).ToString() + " giây");
        }
    }
}
