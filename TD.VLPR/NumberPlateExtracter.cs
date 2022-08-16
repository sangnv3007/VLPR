using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Dnn;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using PaddleOCRSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        OCRModelConfig config = null;
        OCRParameter oCRParameter = new OCRParameter();
        OCRResult ocrResult = new OCRResult();
        PaddleOCREngine engine = null;
        public NumberPlateExtracter(
            string pathConfig = "yolov3.cfg",
            string pathWeights = "yolov3_Final.weights")
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

        string textPlates = string.Empty;
        ResultLP result = new ResultLP();
        //Hàm trích xuất thông tin biển số xe từ đường dẫn ảnh trả về obj
        public ResultLP ProcessImage(Mat imgInput)
        {            
            try
            {
                float confThreshold = 0.8f;
                int imgDefaultSizeH = 576;
                int imgDefaultSizeW = 1024;
                //Detect biển số xe

                Image<Bgr, Byte> src = imgInput.ToImage<Bgr, Byte>();
                var img = src.Resize(imgDefaultSizeW, imgDefaultSizeH, Inter.Cubic);
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
                        }
                    }

                }
                if (imageCrop.Height != imgDefaultSizeH && imageCrop.Width != imgDefaultSizeW)
                {
                    CvInvoke.Imwrite("imgcrop.jpg", imageCrop);
                    ocrResult = engine.DetectText(imageCrop.ToBitmap());
                    List<string> arrayresult = new List<string>();
                    if (ocrResult != null)
                    {
                        for (int i = 0; i < ocrResult.TextBlocks.Count; i++)
                        {
                            string TextBlocksPlate = ocrResult.TextBlocks[i].Text;
                            TextBlocksPlate = Regex.Replace(TextBlocksPlate, @"[^0-9A-Z\-]", "");
                            if (isValidPlatesNumber(TextBlocksPlate))
                            {
                                arrayresult.Add(TextBlocksPlate);
                            }
                        }
                        textPlates = string.Join("-", arrayresult);
                        LPReturn obj = new LPReturn();
                        result = obj.Result(textPlates, true);
                    }
                }
                else
                {
                    LPReturn obj = new LPReturn();
                    result = obj.Result("No license plate found", false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return result;
        }
        public ResultLP ProcessImage(string path)
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
                        }
                    }

                }
                if (imageCrop.Height != imgDefaultSize && imageCrop.Width != imgDefaultSize)
                {
                    CvInvoke.Imwrite("imgcrop.jpg", imageCrop);
                    ocrResult = engine.DetectText(imageCrop.ToBitmap());
                    List<string> arrayresult = new List<string>();
                    if (ocrResult != null)
                    {
                        for (int i = 0; i < ocrResult.TextBlocks.Count; i++)
                        {
                            string TextBlocksPlate = ocrResult.TextBlocks[i].Text;
                            TextBlocksPlate = Regex.Replace(TextBlocksPlate, @"[^0-9A-Z\-]", "");
                            if (isValidPlatesNumber(TextBlocksPlate))
                            {
                                arrayresult.Add(TextBlocksPlate);
                            }
                        }
                        textPlates = string.Join("-", arrayresult);
                        LPReturn obj = new LPReturn();
                        result = obj.Result(textPlates, true);
                    }
                }
                else
                {
                    LPReturn obj = new LPReturn();
                    result = obj.Result("No license plate found", false);
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
            Model.SetPreferableBackend(Emgu.CV.Dnn.Backend.OpenCV);
            Model.SetPreferableTarget(Target.Cpu);
            engine = new PaddleOCREngine(config, oCRParameter);
        }
        public static bool isValidPlatesNumber(string inputPlatesNumber)
        {
            string strRegex = @"(^[A-Z0-9]{2}-?[A-Z0-9]{0,3}$)|(^[0-9]{4,5}$)|(^[0-9]{2}[A-Z]{1,2}-?[0-9]{4,5}$)|(^[A-Z]{2}-?[0-9]{2}-?[0-9]{2}$)|(^[A-Z0-9]{2}-?[A-Z0-9]{2,3}-?[A-Z0-9]{2,3}-?[0-9]{2}$)";
            Regex re = new Regex(strRegex);
            if (re.IsMatch(inputPlatesNumber))
                return (true);
            else
                return (false);
        }
    }

    public class ResultLP
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

    public class LPReturn
    {
        // Create a class result for ResultLP.
        public ResultLP Result(string LP, bool statusLP)
        {
            ResultLP result = new ResultLP();
            result.LP = LP;
            if (statusLP) result.statusLP = 0;
            else result.statusLP = 1;
            return result;
        }
    }
}
