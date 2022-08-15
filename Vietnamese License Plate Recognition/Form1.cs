using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Dnn;
using Emgu.CV.Util;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.Drawing.Imaging;
using System.IO;
using System.Diagnostics;
using PaddleOCRSharp;
using System.Text.RegularExpressions;

namespace Vietnamese_License_Plate_Recognition
{
    public partial class Form1 : Form
    {
        Net Model = null;
        string PathConfig = "yolov3.cfg";
        string PathWeights = "yolov3_Final.weights";
        OCRModelConfig config = null;
        OCRParameter oCRParameter = new OCRParameter();
        OCRResult ocrResult = new OCRResult();
        PaddleOCREngine engine = null;
        public Form1()
        {
            InitializeComponent();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            // open file dialog   
            OpenFileDialog open = new OpenFileDialog();
            // image filters  
            open.Filter = "Image Files(*.jpg; *.jpeg; *.gif; *.bmp; *.png; *.webp)|*.jpg; *.jpeg; *.gif; *.bmp; *.png; *.webp)";
            if (open.ShowDialog() == DialogResult.OK)
            {
                using (Image sourceImg = Image.FromFile(open.FileName))
                {
                    Image clonedImg = new Bitmap(sourceImg.Width, sourceImg.Height, PixelFormat.Format32bppArgb);
                    using (var copy = Graphics.FromImage(clonedImg))
                    {
                        copy.DrawImage(sourceImg, 0, 0);
                    }
                    pictureBox6.InitialImage = null;
                    pictureBox6.Image = clonedImg;
                }
                //  Khai báo đối tượng Stopwatch
                Stopwatch swObj = new Stopwatch();

                //Thời gian bắt đầu
                swObj.Start();

                ProcessImage(open.FileName);

                //Thời gian kết thúc
                swObj.Stop();
                //Tổng thời gian thực hiện               
                label5.Text = Math.Round(swObj.Elapsed.TotalSeconds, 2).ToString() + " giây";
            }
        }
        public static List<float[]> ArrayTo2DList(Array array)
        {
            //System.Collections.IEnumerator enumerator = array.GetEnumerator();
            int rows = array.GetLength(0);
            int cols = array.GetLength(1);
            List<float[]> list = new List<float[]>();
            List<float> temp = new List<float>();

            for (int i = 0; i < rows; i++)
            {
                temp.Clear();
                for (int j = 0; j < cols; j++)
                {
                    temp.Add(float.Parse(array.GetValue(i, j).ToString()));
                }
                list.Add(temp.ToArray());
            }

            return list;
        }
        //Hàm xử lý biển số xe 
        string textPlates = "";
        public void ProcessImage(string path)
        {
            try
            {
                float confThreshold = 0.8f;
                int imgDefaultSize = 416;
                //Detect biển số xe                
                var img = new Image<Bgr, byte>(path)
                      .Resize(imgDefaultSize, imgDefaultSize, Inter.Cubic);
                var input = DnnInvoke.BlobFromImage(img, 1 / 255.0, swapRB: true);
                Model.SetInput(input);
                Model.SetPreferableBackend(Emgu.CV.Dnn.Backend.OpenCV);
                Model.SetPreferableTarget(Target.Cpu);
                VectorOfMat vectorOfMat = new VectorOfMat();
                Model.Forward(vectorOfMat, Model.UnconnectedOutLayersNames);
                VectorOfRect bboxes = new VectorOfRect();
                Image<Bgr, byte> imageCrop = img.Clone();
                for (int k = 0; k < vectorOfMat.Size; k++)
                {
                    var mat = vectorOfMat[k];
                    var data = ArrayTo2DList(mat.GetData());
                    for (int i = 0; i < data.Count; i++)
                    {
                        var row = data[i];
                        var rowsscores = row.Skip(5).ToArray();
                        var classId = rowsscores.ToList().IndexOf(rowsscores.Max());
                        var confidence = rowsscores[classId];

                        if (confidence > confThreshold)
                        {
                            var center_x = (int)(row[0] * img.Width);
                            var center_y = (int)(row[1] * img.Height);

                            var width = (int)(row[2] * img.Width);
                            var height = (int)(row[3] * img.Height);

                            var x = (int)(center_x - (width / 2));
                            var y = (int)(center_y - (height / 2));
                            Rectangle plate = new Rectangle(x, y, width, height);
                            imageCrop = img.Clone();
                            imageCrop.ROI = plate;
                            imageCrop = imageCrop.Resize(500, 500, Inter.Cubic, preserveScale: true);
                        }
                    }
                }
                CvInvoke.Imwrite("imgcrop.jpg", imageCrop);
                pictureBox3.Image = imageCrop.ToBitmap();
                ocrResult = engine.DetectText(imageCrop.ToBitmap());
                List<string> arrayresult = new List<string>();
                if (ocrResult != null)
                {
                    for (int i = 0; i < ocrResult.TextBlocks.Count; i++)
                    {
                        arrayresult.Add(ocrResult.TextBlocks[i].Text);
                    }
                    textPlates = string.Join("-", arrayresult);                   
                    textPlates = Regex.Replace(textPlates, @"[^0-9A-Z\-]", "");
                    textBox1.Text = textPlates;
                    LPReturnForm obj = new LPReturnForm();
                    ResultLPForm resultobj = obj.Result(textPlates, isValidPlatesNumber(textPlates));                   
                    label2.Text = "Biển số: "+ resultobj.LP +", status: "+ resultobj.statusLP;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //public void IdentifyContours(Image<Bgr,byte> colorImage)
        //{
        //    List<Rectangle> listRect = new List<Rectangle>();
        //    Image<Gray, byte> srcGray = colorImage.Convert<Gray, byte>();
        //    Image<Gray, byte> imageT = new Image<Gray, byte>(srcGray.Width, srcGray.Height);
        //    //srcGray = srcGray.Dilate(2);
        //    CvInvoke.AdaptiveThreshold(srcGray, imageT, 255.0, AdaptiveThresholdType.MeanC, ThresholdType.BinaryInv, 51, 9);           
        //    Image<Gray, byte> imageThresh = imageT;
        //    //CvInvoke.Imwrite(@"D:\PaddleOCR\imgtest2.jpg", imageT);
        //    pictureBox3.Image = imageT.ToBitmap();
        //    imageT = imageT.ThresholdBinary(new Gray(100), new Gray(255.0));//Cần xử lý thêm
        //    var rate = (double)imageT.Width/(double)imageT.Height;
        //    Console.WriteLine(rate);
        //    VectorOfVectorOfPoint contour = new VectorOfVectorOfPoint();
        //    Mat hier = new Mat();
        //    CvInvoke.FindContours(imageT, contour, hier, RetrType.List, ChainApproxMethod.ChainApproxSimple);
        //    string textPlates = "";
        //    if (contour.Size > 0)
        //    {
        //        var max_H = 0;
        //        //Biển 1 dòng
        //        if (rate > 3)//Tỉ lệ dài / rộng
        //        {
        //            Console.WriteLine("Day la bien 1 dong");                                 
        //            for (int c = 0; c < contour.Size; c++)
        //            {
        //                Rectangle rect = CvInvoke.BoundingRectangle(contour[c]);                    
        //                //double area = CvInvoke.ContourArea(contour[c]);
        //                //double rate = (double)rect.Width / (double)rect.Height;
        //                if (rect.Width > 15 && rect.Width < 80 && rect.Height > 35 && rect.Height < 100 && rect.X > 10 && rect.Y > 10)
        //                {
        //                    if (max_H < rect.Height) max_H = rect.Height;
        //                    //Console.WriteLine("X: {0}, Y: {1}, W: {2}, H: {3}", rect.X, rect.Y, rect.Width, rect.Height);
        //                    CvInvoke.Rectangle(colorImage, rect, new MCvScalar(0, 0, 255), 1);
        //                    listRect.Add(rect);
        //                }                       
        //            }
        //            for(int i = 0;i<listRect.Count;i++)
        //            {
        //                if(listRect[i].Height + 10 < max_H) listRect.RemoveAt(i);
        //            }    
        //            List<Rectangle> up = new List<Rectangle>();
        //            List<Rectangle> dow = new List<Rectangle>();
        //            ArrangePlates(listRect, false, up, dow);                 
        //            for (int i = 0; i < listRect.Count; i++)
        //            {
        //                Image<Gray, byte> imageCrop = imageThresh;
        //                imageCrop = imageCrop.Dilate(2);
        //                imageCrop.ROI = listRect[i];
        //                //CvInvoke.Imshow("Numbers Plates Up", imageCrop);
        //                CvInvoke.Imwrite("images/LP("+i+").jpg", imageCrop);
        //                //CvInvoke.WaitKey(0);
        //            }
        //            textPlates = func();
        //            textBox1.Text = textPlates;
        //        }
        //        //Biển 2 dòng
        //        else
        //        {
        //            Console.WriteLine("Day la bien 2 dong");
        //            for (int c = 0; c < contour.Size; c++)
        //            {
        //                Rectangle rect = CvInvoke.BoundingRectangle(contour[c]);                     
        //                if (rect.Width > 20 && rect.Width < 150 && rect.Height > 70 && rect.Height < 170 && rect.X > 10 && rect.Y > 10)
        //                {
        //                    if (max_H < rect.Height) max_H = rect.Height;
        //                    //Console.WriteLine("X: {0}, Y: {1}, W: {2}, H: {3}", rect.X, rect.Y, rect.Width, rect.Height);
        //                    CvInvoke.Rectangle(colorImage, rect, new MCvScalar(0, 0, 255), 1);
        //                    listRect.Add(rect);
        //                }
        //            }
        //            for (int i = 0; i < listRect.Count; i++)
        //            {
        //                if (listRect[i].Height + 20< max_H) listRect.RemoveAt(i);
        //            }
        //            List<Rectangle> up = new List<Rectangle>();
        //            List<Rectangle> dow = new List<Rectangle>();
        //            ArrangePlates(listRect, true, up, dow);
        //            //Console.WriteLine("Up count: {0}, Dow count: {1}", up.Count, dow.Count);
        //            for (int i = 0; i < up.Count; i++)
        //            {
        //                Image<Gray, byte> imageCrop = imageThresh;
        //                imageCrop = imageCrop.Dilate(2);
        //                imageCrop.ROI = up[i];
        //                //CvInvoke.Imshow("Numbers Plates Up", imageCrop);
        //                CvInvoke.Imwrite("images/LP_Up(" + i + ").jpg", imageCrop);
        //                //CvInvoke.WaitKey(0);                       
        //            }
        //            textPlates = func();
        //            textPlates += " - ";
        //            for (int i = 0; i < dow.Count; i++)
        //            {
        //                Image<Gray, byte> imageCrop = imageThresh;
        //                imageCrop = imageCrop.Dilate(2);
        //                imageCrop.ROI = dow[i];                     
        //                //CvInvoke.Imshow("Numbers Plates(imgD)", imageCrop);
        //                CvInvoke.Imwrite("images/LP_Dow(" + i + ").jpg", imageCrop);
        //                //CvInvoke.WaitKey(0);
        //            }
        //            textPlates += func();
        //        }              
        //    }
        //    else
        //    {
        //        MessageBox.Show("Không nhận diện được ảnh này. Thử lại ảnh khác!", "TD SJC");
        //    }
        //    //Console.WriteLine(listRect.Count);
        //    textBox1.Text = textPlates;
        //    //pictureBox2.Image = colorImage.ToBitmap();
        //}

        public void ArrangePlates(List<Rectangle> listRect, bool isTwoPlates, List<Rectangle> up, List<Rectangle> dow)
        {
            if (isTwoPlates)
            {
                int up_y = 0, dow_y = 0;
                bool flag_up = false;
                if (listRect == null) return;
                for (int i = 0; i < listRect.Count; i++)
                {
                    for (int j = i; j < listRect.Count; j++)
                    {
                        if (listRect[i].Y > listRect[j].Y + 100)
                        {
                            flag_up = true;
                            up_y = listRect[j].Y;
                            dow_y = listRect[i].Y;
                            break;
                        }
                        else if (listRect[j].Y > listRect[i].Y + 100)
                        {
                            flag_up = true;
                            up_y = listRect[i].Y;
                            dow_y = listRect[j].Y;
                            break;
                        }
                        if (flag_up == true) break;
                    }
                }
                for (int i = 0; i < listRect.Count; i++)
                {
                    //Up Plate
                    if (listRect[i].Y < up_y + 50 && listRect[i].Y > up_y - 50)
                    {
                        up.Add(listRect[i]);
                    }
                    //Dow Plate
                    else if (listRect[i].Y < dow_y + 50 && listRect[i].Y > dow_y - 50)
                    {
                        dow.Add(listRect[i]);
                    }
                }
                if (flag_up == false) dow = listRect;
                //Sắp xếp các số trong Up PLate
                for (int i = 0; i < up.Count; i++)
                {
                    for (int j = i; j < up.Count; j++)
                    {
                        if (up[i].X > up[j].X)
                        {
                            Rectangle w = up[i];
                            up[i] = up[j];
                            up[j] = w;
                        }
                    }
                }
                //Sắp xếp các số trong Dow PLate
                for (int i = 0; i < dow.Count; i++)
                {
                    for (int j = i; j < dow.Count; j++)
                    {
                        if (dow[i].X > dow[j].X)
                        {
                            Rectangle w = dow[i];
                            dow[i] = dow[j];
                            dow[j] = w;
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < listRect.Count; i++)
                {
                    for (int j = i; j < listRect.Count; j++)
                    {
                        if (listRect[i].X > listRect[j].X)
                        {
                            Rectangle w = listRect[i];
                            listRect[i] = listRect[j];
                            listRect[j] = w;
                        }
                    }
                }
            }
        }
        public Rectangle cutPlates(List<Rectangle> listRect, out double chenhlech)
        {
            Rectangle Bien = new Rectangle();
            int xmin = listRect[0].X, xmax = listRect[0].X + listRect[0].Width;
            int ymin = listRect[0].Y, ymax = listRect[0].Y;
            int Y = 0;
            int Height = 0;
            for (int i = 0; i < listRect.Count; i++)
            {
                if (xmin > listRect[i].X)
                {
                    xmin = listRect[i].X;
                    ymin = listRect[i].Y;
                }
                if (xmax < listRect[i].X + listRect[i].Width)
                {
                    xmax = listRect[i].X + listRect[i].Width;
                    ymax = listRect[i].Y;
                }
                Y += listRect[i].Y;
                Height += listRect[i].Height;
            }
            Bien.X = xmin - 10;
            Bien.Y = Y / listRect.Count - 10;
            Bien.Width = xmax - xmin + 20;
            Bien.Height = Height / listRect.Count + 20;
            chenhlech = ymax - ymin;
            return Bien;
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void groupBox2_Enter(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            LoadModelRecognize();
        }
        // method containing the regex
        public static bool isValidPlatesNumber(string inputPlatesNumber)
        {
            string strRegex = @"(^[0-9]{2}-[A-Z0-9]{2,3}-[0-9]{4,5}$)|(^[A-Z]{0,4}-[0-9]{2}-[0-9]{2}$)|(^[A-Z0-9]{2}-[A-Z0-9]{2,3}-[A-Z0-9]{2,3}-[0-9]{2}$)|(^[0-9]{2}[0-9A-Z]{1,2}-[0-9]{4,5}$)|(^[A-Z0-9]{7,9}$)";
            Regex re = new Regex(strRegex);
            if (re.IsMatch(inputPlatesNumber))
                return (true);
            else
                return (false);
        }
        public void LoadModelRecognize()
        {
            try
            {
                Model = DnnInvoke.ReadNetFromDarknet(PathConfig, PathWeights);//Load model detect LP
                engine = new PaddleOCREngine(config, oCRParameter);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void pictureBox6_Click(object sender, EventArgs e)
        {

        }
    }
    public class ResultLPForm
    {
        /// <summary>
        /// Thông tin biển số xe
        /// </summary>
        public string LP { get; set; }
        /// <summary>
        /// 0 : Thành công
        /// 1 : Không nhận diện được hoặc nhận diện sai
        /// </summary>
        public int statusLP { get; set; }
    }

    public class LPReturnForm
    {
        // Create a class result for ResultLP.
        public ResultLPForm Result(string LP, bool statusLP)
        {
            ResultLPForm result = new ResultLPForm();
            result.LP = LP;
            if (statusLP) result.statusLP = 0;
            else result.statusLP = 1;
            return result;
        }
    }
}
