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
using Tesseract;
using System.Drawing.Imaging;
using System.IO;
using Python.Runtime;

namespace Vietnamese_License_Plate_Recognition
{
    public partial class Form1 : Form
    {
        Net Model = null;
        string PathConfig = "yolov3.cfg";
        string PathWeights = "yolov3_6000_LP.weights";
        dynamic func;
        public Form1()
        {
            InitializeComponent();
        }
        private void button1_Click(object sender, EventArgs e)
        {          
            // open file dialog   
            OpenFileDialog open = new OpenFileDialog();
            // image filters  
            open.Filter = "Image Files(*.jpg; *.jpeg; *.gif; *.bmp)|*.jpg; *.jpeg; *.gif; *.bmp";
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
                ProcessImage(open.FileName, pictureBox6);
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
        public void ProcessImage(string path, PictureBox pictureBox6)
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
                            Image<Bgr, byte> imageCrop = img.Clone();
                            imageCrop.ROI = plate;
                            pictureBox2.Image = imageCrop.ToBitmap();                       
                            IdentifyContours(imageCrop.Resize(500, 500, Inter.Cubic, preserveScale: true)); 
                        }
                    }

                }
                //Vẽ boxes detect được
                //var idx = DnnInvoke.NMSBoxes(bboxes,scores, confThreshold, nmsThreshold, indices);
                //var imgOutput = img.Clone();
                //for (int i = 0; i < idx.Length; i++)
                //{
                //    int index = idx[i];
                //    var bbox = bboxes[index];
                //    imgOutput.Draw(bbox, new Bgr(0, 255, 0), 2);
                //    CvInvoke.PutText(imgOutput, ClassLabels[indices[index]], new Point(bbox.X, bbox.Y + 20),
                //        FontFace.HersheySimplex, 1.0, new MCvScalar(0, 0, 255), 2);
                //}
                //var input = DnnInvoke.BlobFromImage(img, 1 / 255.0, swapRB: true);
                Image<Bgr, byte> image2 = img;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }      
        public void IdentifyContours(Image<Bgr,byte> colorImage)
        {
            List<Rectangle> listRect = new List<Rectangle>();
            Image<Gray, byte> srcGray = colorImage.Convert<Gray, byte>();
            Image<Gray, byte> imageT = new Image<Gray, byte>(srcGray.Width, srcGray.Height);
            //srcGray = srcGray.Dilate(2);
            CvInvoke.AdaptiveThreshold(srcGray, imageT, 255.0, AdaptiveThresholdType.MeanC, ThresholdType.BinaryInv, 51, 9);           
            Image<Gray, byte> imageThresh = imageT;
            pictureBox3.Image = imageT.ToBitmap();
            imageT = imageT.ThresholdBinary(new Gray(100), new Gray(255.0));//Cần xử lý thêm
            var rate = (double)imageT.Width/(double)imageT.Height;
            Console.WriteLine(rate);
            VectorOfVectorOfPoint contour = new VectorOfVectorOfPoint();
            Mat hier = new Mat();
            CvInvoke.FindContours(imageT, contour, hier, RetrType.List, ChainApproxMethod.ChainApproxSimple);
            string textPlates = "";
            if (contour.Size > 0)
            {
                var max_H = 0;
                //Biển 1 dòng
                if (rate > 3)//Tỉ lệ dài / rộng
                {
                    Console.WriteLine("Day la bien 1 dong");                                 
                    for (int c = 0; c < contour.Size; c++)
                    {
                        Rectangle rect = CvInvoke.BoundingRectangle(contour[c]);                    
                        //double area = CvInvoke.ContourArea(contour[c]);
                        //double rate = (double)rect.Width / (double)rect.Height;
                        if (rect.Width > 15 && rect.Width < 80 && rect.Height > 35 && rect.Height < 100 && rect.X > 10 && rect.Y > 10)
                        {
                            if (max_H < rect.Height) max_H = rect.Height;
                            //Console.WriteLine("X: {0}, Y: {1}, W: {2}, H: {3}", rect.X, rect.Y, rect.Width, rect.Height);
                            CvInvoke.Rectangle(colorImage, rect, new MCvScalar(0, 0, 255), 1);
                            listRect.Add(rect);
                        }                       
                    }
                    for(int i = 0;i<listRect.Count;i++)
                    {
                        if(listRect[i].Height + 10 < max_H) listRect.RemoveAt(i);
                    }    
                    List<Rectangle> up = new List<Rectangle>();
                    List<Rectangle> dow = new List<Rectangle>();
                    ArrangePlates(listRect, false, up, dow);                 
                    for (int i = 0; i < listRect.Count; i++)
                    {
                        Image<Gray, byte> imageCrop = imageThresh;
                        imageCrop = imageCrop.Dilate(2);
                        imageCrop.ROI = listRect[i];
                        //CvInvoke.Imshow("Numbers Plates Up", imageCrop);
                        CvInvoke.Imwrite("images/LP("+i+").jpg", imageCrop);
                        //CvInvoke.WaitKey(0);
                    }
                    textPlates = func();
                    textBox1.Text = textPlates;
                }
                //Biển 2 dòng
                else
                {
                    Console.WriteLine("Day la bien 2 dong");
                    for (int c = 0; c < contour.Size; c++)
                    {
                        Rectangle rect = CvInvoke.BoundingRectangle(contour[c]);                     
                        if (rect.Width > 20 && rect.Width < 150 && rect.Height > 70 && rect.Height < 170 && rect.X > 10 && rect.Y > 10)
                        {
                            if (max_H < rect.Height) max_H = rect.Height;
                            //Console.WriteLine("X: {0}, Y: {1}, W: {2}, H: {3}", rect.X, rect.Y, rect.Width, rect.Height);
                            CvInvoke.Rectangle(colorImage, rect, new MCvScalar(0, 0, 255), 1);
                            listRect.Add(rect);
                        }
                    }
                    for (int i = 0; i < listRect.Count; i++)
                    {
                        if (listRect[i].Height + 20< max_H) listRect.RemoveAt(i);
                    }
                    List<Rectangle> up = new List<Rectangle>();
                    List<Rectangle> dow = new List<Rectangle>();
                    ArrangePlates(listRect, true, up, dow);
                    //Console.WriteLine("Up count: {0}, Dow count: {1}", up.Count, dow.Count);
                    for (int i = 0; i < up.Count; i++)
                    {
                        Image<Gray, byte> imageCrop = imageThresh;
                        imageCrop = imageCrop.Dilate(2);
                        imageCrop.ROI = up[i];
                        //CvInvoke.Imshow("Numbers Plates Up", imageCrop);
                        CvInvoke.Imwrite("images/LP_Up(" + i + ").jpg", imageCrop);
                        //CvInvoke.WaitKey(0);                       
                    }
                    textPlates = func();
                    textPlates += " - ";
                    for (int i = 0; i < dow.Count; i++)
                    {
                        Image<Gray, byte> imageCrop = imageThresh;
                        imageCrop = imageCrop.Dilate(2);
                        imageCrop.ROI = dow[i];                     
                        //CvInvoke.Imshow("Numbers Plates(imgD)", imageCrop);
                        CvInvoke.Imwrite("images/LP_Dow(" + i + ").jpg", imageCrop);
                        //CvInvoke.WaitKey(0);
                    }
                    textPlates += func();
                }              
            }
            else
            {
                MessageBox.Show("Không nhận diện được ảnh này. Thử lại ảnh khác!", "TD SJC");
            }
            //Console.WriteLine(listRect.Count);
            textBox1.Text = textPlates;
            pictureBox2.Image = colorImage.ToBitmap();
        }

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

        public static Bitmap rotateImage(Bitmap b, double angle)
        {
            //create a new empty bitmap to hold rotated image
            Bitmap returnBitmap = new Bitmap(b.Width, b.Height);
            //make a graphics object from the empty bitmap
            Graphics g = Graphics.FromImage(returnBitmap);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            //move rotation point to center of image
            g.TranslateTransform((float)b.Width / 2, (float)b.Height / 2);
            //rotate
            g.RotateTransform(Convert.ToSingle(angle));
            //move image back
            g.TranslateTransform(-(float)b.Width / 2, -(float)b.Height / 2);
            //draw passed in image onto graphics object
            g.DrawImage(b, new Point(0, 0));
            return returnBitmap;
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
            Bien.X = xmin-10;
            Bien.Y = Y / listRect.Count-10;
            Bien.Width = xmax - xmin +20;
            Bien.Height = Height / listRect.Count +20;
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

        public void LoadModelRecognize()
        {
            Model = DnnInvoke.ReadNetFromDarknet(PathConfig, PathWeights);//Load model detect LP
            //Executing Python Code Script
            using (Py.GIL())
            {
                using (PyScope scope = Py.CreateScope())
                {
                    string code = File.ReadAllText("reconigtion_character.py");

                    var scriptCompiled = PythonEngine.Compile(code);

                    scope.Execute(scriptCompiled);
                    func = scope.Get("output");
                }
            }
        }
    }
}
