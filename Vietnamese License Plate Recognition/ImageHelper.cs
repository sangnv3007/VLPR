using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Dnn;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vietnamese_License_Plate_Recognition
{
    public class ImageHelper
    {
        Net Model = null;
        string PathConfig = "yolov3.cfg";
        string PathWeights = "yolov3_6000_LP.weights";
        dynamic func;

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

        //Hàm trích xuất thông tin biển số xe từ đường dẫn ảnh
        public string ProcessImage(string path)
        {
            LoadModelRecognize();
            string result="";
            try
            {
                float confThreshold = 0.8f;
                int imgDefaultSize = 416;
                //Detect biển số xe                
                var img = new Image<Bgr, byte>(path).Resize(imgDefaultSize, imgDefaultSize, Inter.Cubic);
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
                            result = IdentifyContours(imageCrop.Resize(500, 500, Inter.Cubic, preserveScale: true));//Hàm tìm contour                                                                                                                  
                        }
                    }

                }  
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return result;
        }

        public string IdentifyContours(Image<Bgr, byte> colorImage)
        {
            List<Rectangle> listRect = new List<Rectangle>();
            Image<Gray, byte> srcGray = colorImage.Convert<Gray, byte>();
            Image<Gray, byte> imageT = new Image<Gray, byte>(srcGray.Width, srcGray.Height);
            CvInvoke.AdaptiveThreshold(srcGray, imageT, 255.0, AdaptiveThresholdType.MeanC, ThresholdType.BinaryInv, 51, 9);
            Image<Gray, byte> imageThresh = imageT;
            //pictureBox3.Image = imageT.ToBitmap();
            imageT = imageT.ThresholdBinary(new Gray(100), new Gray(255.0));//Cần xử lý thêm để cải thiện
            var rate = (double)imageT.Width / (double)imageT.Height;
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
                        if (rect.Width > 15 && rect.Width < 80 && rect.Height > 35 && rect.Height < 100 && rect.X > 10 && rect.Y > 10)
                        {
                            if (max_H < rect.Height) max_H = rect.Height;
                            //Console.WriteLine("X: {0}, Y: {1}, W: {2}, H: {3}", rect.X, rect.Y, rect.Width, rect.Height);
                            CvInvoke.Rectangle(colorImage, rect, new MCvScalar(0, 0, 255), 1);
                            listRect.Add(rect);
                        }
                    }

                    for (int i = 0; i < listRect.Count; i++)
                    {
                        if (listRect[i].Height + 10 < max_H) listRect.RemoveAt(i);
                    }

                    List<Rectangle> up = new List<Rectangle>();
                    List<Rectangle> dow = new List<Rectangle>();

                    ArrangePlates(listRect, false, up, dow);//Hàm sắp xếp thứ tự số trong biển

                    //Lưu ảnh vừa cắt được
                    for (int i = 0; i < listRect.Count; i++)
                    {
                        Image<Gray, byte> imageCrop = imageThresh;
                        imageCrop.ROI = listRect[i];
                        CvInvoke.Imwrite("images/LP(" + i + ").jpg", imageCrop);
                    }
                    textPlates = func();//Xử lý nhận dạng các ký tự vừa lưu
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

                    //Loại bỏ những rectangle thừa
                    for (int i = 0; i < listRect.Count; i++)
                    {
                        if (listRect[i].Height + 25 < max_H) listRect.RemoveAt(i);
                    }

                    List<Rectangle> up = new List<Rectangle>();
                    List<Rectangle> dow = new List<Rectangle>();

                    ArrangePlates(listRect, true, up, dow);//Hàm sắp xếp thứ tự số trong biển

                    //Lưu ảnh dòng trên của biển 2 dòng để xử lý
                    for (int i = 0; i < up.Count; i++)
                    {
                        Image<Gray, byte> imageCrop = imageThresh;
                        imageCrop = imageCrop.Dilate(2);
                        imageCrop.ROI = up[i];
                        CvInvoke.Imwrite("images/LP_Up(" + i + ").jpg", imageCrop);                      
                    }
                    textPlates = func();//Xử lý nhận dạng các ký tự vừa lưu
                    textPlates += " - ";
                    for (int i = 0; i < dow.Count; i++)
                    {
                        Image<Gray, byte> imageCrop = imageThresh;
                        imageCrop = imageCrop.Dilate(2);
                        imageCrop.ROI = dow[i];
                        CvInvoke.Imwrite("images/LP_Dow(" + i + ").jpg", imageCrop);
                    }
                    textPlates += func();//Xử lý nhận dạng các ký tự vừa lưu
                }
            }
            else
            {             
                Console.WriteLine("Không nhận diện được ảnh này. Thử lại ảnh khác!");
            }
            return textPlates;
        }

        // Lưu toạ độ ô vuông các biển số
        public void ArrangePlates(List<Rectangle> listRect, bool isTwoPlates, List<Rectangle> up, List<Rectangle> dow)
        {
            //Sắp xếp trên biển 2 dòng
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
            //Sắp xếp trên biển 1 dòng
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
