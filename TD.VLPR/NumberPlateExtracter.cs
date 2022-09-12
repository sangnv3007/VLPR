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
        OCRModelConfig config = new OCRModelConfig();
        OCRParameter oCRParameter = new OCRParameter();
        OCRResult ocrResult = new OCRResult();
        PaddleOCREngine engine = null;
        public NumberPlateExtracter(
            string pathConfig = "yolov4-tiny-custom.cfg",
            string pathWeights = "yolov4-tiny-custom_final.weights")
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
       
        //Hàm trích xuất thông tin biển số xe từ đường dẫn ảnh trả về obj
        public ResultLP ProcessImage(Mat imgInput)
        {
            ResultLP result = new ResultLP();
            try
            {
                string textPlates = string.Empty;
                float confThreshold = 0.8f;// Ngưỡng tin cậy

                //Thay đổi kích thước ảnh
                Image<Bgr, Byte> img = imgInput.ToImage<Bgr, Byte>();
                img = ResizeImage(img, 1280, 0);
                if (img.Width % 32 != 0 || img.Height % 32 != 0)
                {
                    int imgDefaultSizeW = img.Width / 32 * 32;
                    int imgDefaultSizeH = img.Height / 32 * 32;
                    img = img.Resize(imgDefaultSizeW, imgDefaultSizeH, Inter.Cubic);
                }
                //Set input đầu vào cho mô hình
                var input = DnnInvoke.BlobFromImage(img, 1 / 255.0, swapRB: true);
                Model.SetInput(input);               
                VectorOfMat vectorOfMat = new VectorOfMat();

                //Đưa ra kết quả detect được từ mô hình
                Model.Forward(vectorOfMat, Model.UnconnectedOutLayersNames);
                Image<Bgr, byte> imageCrop = img.Clone();
                List<Image<Bgr, byte>> PlateImagesList = new List<Image<Bgr, byte>>();
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
                        //Lưu toạ độ các đối tượng detect được
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
                            PlateImagesList.Add(imageCrop);
                        }
                    }
                }

                //Đưa ra kết quả các ảnh đã detect được                             
                if (PlateImagesList.Count > 0)
                {
                    string temp = String.Empty;
                    for (int i = 0; i < PlateImagesList.Count; i++)
                    {
                        Image<Bgr, byte> imageResize = ResizeImage(PlateImagesList[i], 900, 0);
                        ocrResult = engine.DetectText(imageResize.ToBitmap());
                        List<string> arrayresult = new List<string>();
                        // Do dai toi da cua bien co the chua la 12 ky tu(bao gom ca cac ky tu "-" hoặc ".")
                        if (ocrResult.Text.Length > temp.Length && ocrResult.Text != String.Empty && ocrResult.Text.Length <= 12)
                        {
                            temp = ocrResult.Text;
                            double accuracy = 1;
                            for (int j = 0; j < ocrResult.TextBlocks.Count; j++)
                            {
                                string TextBlocksPlate = ocrResult.TextBlocks[j].Text;
                                TextBlocksPlate = Regex.Replace(TextBlocksPlate, @"[^A-Z0-9\-]|^-|-$", "");
                                if (isValidPlatesNumber(TextBlocksPlate))
                                {
                                    if (ocrResult.TextBlocks[j].Score < accuracy)
                                    {
                                        accuracy = Math.Round(ocrResult.TextBlocks[j].Score, 2);
                                    }
                                    arrayresult.Add(TextBlocksPlate);
                                }
                            }
                            if (arrayresult.Count != 0)
                            {
                                textPlates = string.Join("-", arrayresult);
                                CvInvoke.Imwrite("imgcropColor.jpg", PlateImagesList[i]);
                                LPReturn obj = new LPReturn();
                                result = obj.Result(textPlates, true, accuracy);
                            }
                            else
                            {
                                LPReturn obj = new LPReturn();
                                result = obj.Result("Null", false, 0);
                            }
                        }
                    }
                    if (ocrResult.Text == String.Empty)
                    {
                        LPReturn obj = new LPReturn();
                        result = obj.Result("Null", false, 0);
                    }      
                }                
                else
                {
                    LPReturn obj = new LPReturn();
                    result = obj.Result("No license plate found", false, 0);
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
            ResultLP result = new ResultLP();
            try
            {
                string textPlates = string.Empty;
                float confThreshold = 0.8f;// Ngưỡng tin cậy

                //Thay đổi kích thước ảnh              
                var img = new Image<Bgr, byte>(path);
                img = ResizeImage(img, 1280, 0);
                if (img.Width % 32 != 0 || img.Height % 32 != 0)
                {
                    int imgDefaultSizeW = img.Width / 32 * 32;
                    int imgDefaultSizeH = img.Height / 32 * 32;
                    img = img.Resize(imgDefaultSizeW, imgDefaultSizeH, Inter.Cubic);
                }

                //Đưa ra kết quả từ mô hình
                var input = DnnInvoke.BlobFromImage(img, 1 / 255.0, swapRB: true);
                Model.SetInput(input);
                VectorOfMat vectorOfMat = new VectorOfMat();
                Model.Forward(vectorOfMat, Model.UnconnectedOutLayersNames);
                Image<Bgr, byte> imageCrop = img.Clone();
                List<Image<Bgr, byte>> PlateImagesList = new List<Image<Bgr, byte>>();
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
                        //Kiem tra nguong tin cay
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
                            PlateImagesList.Add(imageCrop);
                        }
                    }
                }

                //Đưa ra kết quả các ảnh đã detect được

                if (PlateImagesList.Count > 0)
                {
                    OCRResult tempOCRResult = new OCRResult();
                    for (int i = 0; i < PlateImagesList.Count; i++)
                    {
                        Image<Bgr, byte> imageResize = ResizeImage(PlateImagesList[i], 900, 0);
                        ocrResult = engine.DetectText(imageResize.ToBitmap());
                        List<string> arrayresult = new List<string>();
                        // Do dai toi da cua bien co the chua la 12 ky tu(bao gom ca cac ky tu "-" hoặc ".")
                        if (ocrResult.Text.Length > tempOCRResult.Text.Length && ocrResult.Text != String.Empty && ocrResult.Text.Length <= 12)
                        {
                            tempOCRResult = ocrResult;
                            double accuracy = 1;
                            for (int j = 0; j < ocrResult.TextBlocks.Count; j++)
                            {
                                string TextBlocksPlate = ocrResult.TextBlocks[j].Text;
                                TextBlocksPlate = Regex.Replace(TextBlocksPlate, @"[^A-Z0-9\-]|^-|-$", "");
                                if (isValidPlatesNumber(TextBlocksPlate))
                                {
                                    if (ocrResult.TextBlocks[j].Score < accuracy)
                                    {
                                        accuracy = Math.Round(ocrResult.TextBlocks[j].Score, 2);
                                    }
                                    arrayresult.Add(TextBlocksPlate);
                                }
                            }
                            if (arrayresult.Count != 0)
                            {
                                textPlates = string.Join("-", arrayresult);
                                //CvInvoke.Imwrite(@"D:\PaddleOCR\Recognition PaddleOCR\CropLP\imgcropColor" + textPlates + ".jpg", PlateImagesList[i]);
                                CvInvoke.Imwrite("imgcropColor.jpg", PlateImagesList[i]);
                                LPReturn obj = new LPReturn();
                                result = obj.Result(textPlates, true, accuracy);
                            }
                            else
                            {
                                LPReturn obj = new LPReturn();
                                result = obj.Result("Null", false, 0);
                            }
                        }
                    }
                }
                else
                {
                    LPReturn obj = new LPReturn();
                    result = obj.Result("No license plate found", false, 0);
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
            string root = Environment.CurrentDirectory;
            string modelPathroot = root + @"\inference";
            config.det_infer = modelPathroot + @"\ch_ppocr_server_v2.0_det_infer";
            config.cls_infer = modelPathroot + @"\ch_ppocr_mobile_v2.0_cls_infer";
            config.rec_infer = modelPathroot + @"\ch_ppocr_server_v2.0_rec_infer";
            config.keys = modelPathroot + @"\en_dict.txt";
            //Load library paddleOCR
            engine = new PaddleOCREngine(config, oCRParameter);
        }
        public static bool isValidPlatesNumber(string inputPlatesNumber)
        {
            string strRegex = @"(^[0-9]{2}-?[0-9A-Z]{1,3}$)|(^[A-Z0-9]{2,5}$)|(^[0-9]{2,3}-[0,9]{2}$)|(^[A-Z0-9]{2,3}-?[0-9]{4,5}$)|(^[A-Z]{2}-?[0-9]{0,4}$)|(^[0-9]{2}-?[A-Z0-9]{2,3}-?[A-Z0-9]{2,3}-?[0-9]{2}$)|(^[A-Z]{2}-?[0-9]{2}-?[0-9]{2}$)|(^[0-9]{3}-?[A-Z0-9]{2}$)";
            Regex re = new Regex(strRegex);
            if (re.IsMatch(inputPlatesNumber))
                return (true);
            else
                return (false);
        }
        public static Image<Bgr, byte> ResizeImage(Image<Bgr, byte> imageOriginal, int width = 0, int height = 0)
        {
            var dim = new Size(0, 0);
            (int w, int h) = (imageOriginal.Width, imageOriginal.Height);
            if (width == 0 && height == 0)
            {
                return imageOriginal;
            }
            if (width == 0)
            {
                double r = height / (float)h;
                dim.Width = (int)(w * r);
                dim.Height = height;
            }
            else
            {
                //double r = width / (float)w;
                double r = width / (float)w;
                dim.Width = width;
                dim.Height = (int)(h * r);
            }
            Image<Bgr, byte> imageReszie = imageOriginal.Resize(dim.Width, dim.Height, Inter.Cubic);
            return imageReszie;
        }
    }

    public class ResultLP
    {
        /// <summary>
        /// Thông tin biển số xe
        /// </summary>
        public string textPlate { get; set; }
        /// <summary>
        /// 0 : Thành công
        /// 1 : Không nhận diện được hoặc nhận diện sai
        /// </summary>
        public int statusPlate { get; set; }
        /// <summary>
        /// Độ chính xác nhận dạng biển số xe
        /// </summary>
        public double accPlate { get; set; }
    }

    public class LPReturn
    {
        // Create a class result for ResultLP.
        public ResultLP Result(string textPlate, bool statusPlate, double accPlate)
        {
            ResultLP result = new ResultLP();
            result.textPlate = textPlate;
            if (statusPlate) result.statusPlate = 0;
            else result.statusPlate = 1;
            result.accPlate = accPlate;
            return result;
        }
    }
}
