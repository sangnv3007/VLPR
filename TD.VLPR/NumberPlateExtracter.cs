using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Dnn;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using PaddleOCRSharp;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TD.VLPR
{
    public class NumberPlateExtracter
    {
        private string PathConfig;
        private string PathWeights;
        //dynamic func;
        Net Model = null;

        public NumberPlateExtracter(
            string pathConfig = "yolov3.cfg",
            string pathWeights = "yolov3_6000_LP.weights")
        {
            PathConfig = pathConfig;
            PathWeights = pathWeights;
            LoadModelRecognize();
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

        string result = string.Empty;
        //Hàm trích xuất thông tin biển số xe từ đường dẫn ảnh
        public string ProcessImage(Mat imgInput)
        {            
            try
            {
                float confThreshold = 0.8f;
                int imgDefaultSize = 416;

                //Detect biển số xe

                Image<Bgr, Byte> src = imgInput.ToImage<Bgr, Byte>();
                var img = src.Resize(imgDefaultSize, imgDefaultSize, Inter.Cubic);
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
                            Rectangle plate = new Rectangle(x-5, y-5, width+10, height+10);
                            imageCrop = img.Clone();
                            imageCrop.ROI = plate;
                            imageCrop = imageCrop.Resize(500, 500, Inter.Cubic, preserveScale: true);                           
                        }                     
                    }

                }
                CvInvoke.Imwrite("imgtest.jpg", imageCrop);
                OCRModelConfig config = null;
                OCRParameter oCRParameter = new OCRParameter();
                OCRResult ocrResult = new OCRResult();
                PaddleOCREngine engine = new PaddleOCREngine(config, oCRParameter);
                {
                    ocrResult = engine.DetectText(imageCrop.ToBitmap());
                }
                List<string> arrayresult = new List<string>();
                if (ocrResult != null)
                {
                    for (int i = 0; i < ocrResult.TextBlocks.Count; i++)
                    {
                        arrayresult.Add(ocrResult.TextBlocks[i].Text);
                    }
                    result = string.Join("-", arrayresult).Replace(".", "");
                    //Console.WriteLine(result);
                }               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return result;
        }
        public string ProcessImage(string path)
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
                            Rectangle plate = new Rectangle(x - 5, y - 5, width + 10, height + 10);
                            imageCrop = img.Clone();
                            imageCrop.ROI = plate;
                            imageCrop = imageCrop.Resize(500, 500, Inter.Cubic, preserveScale: true);
                        }
                    }

                }
                CvInvoke.Imwrite("imgtest.jpg", imageCrop);
                OCRModelConfig config = null;
                OCRParameter oCRParameter = new OCRParameter();
                OCRResult ocrResult = new OCRResult();
                PaddleOCREngine engine = new PaddleOCREngine(config, oCRParameter);
                {
                    ocrResult = engine.DetectText(imageCrop.ToBitmap());
                }
                List<string> arrayresult = new List<string>();
                if (ocrResult != null)
                {
                    for (int i = 0; i < ocrResult.TextBlocks.Count; i++)
                    {
                        arrayresult.Add(ocrResult.TextBlocks[i].Text);
                    }
                    result = string.Join("-", arrayresult).Replace(".", "");
                    //Console.WriteLine(result);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return result;
        }
        public string ProcessImage(Bitmap bitmap)
        {
            try
            {
                float confThreshold = 0.8f;
                int imgDefaultSize = 416;
                Image<Bgr, Byte> src = bitmap.ToImage<Bgr, byte>();
                //Detect biển số xe
                var img = src
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
                            Rectangle plate = new Rectangle(x - 5, y - 5, width + 10, height + 10);
                            imageCrop = img.Clone();
                            imageCrop.ROI = plate;
                            imageCrop = imageCrop.Resize(500, 500, Inter.Cubic, preserveScale: true);
                        }
                    }

                }
                CvInvoke.Imwrite("imgtest.jpg", imageCrop);
                OCRModelConfig config = null;
                OCRParameter oCRParameter = new OCRParameter();
                OCRResult ocrResult = new OCRResult();
                PaddleOCREngine engine = new PaddleOCREngine(config, oCRParameter);
                {
                    ocrResult = engine.DetectText(imageCrop.ToBitmap());
                }
                List<string> arrayresult = new List<string>();
                if (ocrResult != null)
                {
                    for (int i = 0; i < ocrResult.TextBlocks.Count; i++)
                    {
                        arrayresult.Add(ocrResult.TextBlocks[i].Text);
                    }
                    result = string.Join("-", arrayresult).Replace(".", "");
                    //Console.WriteLine(result);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return result;
        }
        public void LoadModelRecognize()
        {
            //Load model detect LP
            Model = DnnInvoke.ReadNetFromDarknet(PathConfig, PathWeights);
        }
    }
}
