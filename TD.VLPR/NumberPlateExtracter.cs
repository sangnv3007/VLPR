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
                double scale = 0.00392;
                float nms_threshold = 0.4f;
                Image<Bgr, Byte> img = imgInput.ToImage<Bgr, Byte>();
                //Set input đầu vào cho mô hình
                var input = DnnInvoke.BlobFromImage(img, scale, new Size(416, 416), new MCvScalar(0, 0, 0), swapRB: true, crop: false);
                Model.SetInput(input);               
                VectorOfMat vectorOfMat = new VectorOfMat();
                //Đưa ra kết quả detect được từ mô hình
                Model.Forward(vectorOfMat, Model.UnconnectedOutLayersNames);
                Image<Bgr, byte> imageCrop = img.Clone();
                List<Image<Bgr, byte>> PlateImagesList = new List<Image<Bgr, byte>>();
                List<Rectangle> ListRec = new List<Rectangle>();
                List<float> confidences = new List<float>();
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
                            confidences.Add(confidence);                           
                            ListRec.Add(plate);
                        }
                    }
                }
                //Loại bỏ các boxes dư thừa bằng NMS
                List<int> indices = new List<int>();
                indices = DnnInvoke.NMSBoxes(ListRec.ToArray(), confidences.ToArray(), confThreshold, nms_threshold).ToList();
                //Đưa ra kết quả các ảnh đã detect được
                if (indices.Count > 0)
                {
                    OCRResult tempOCRResult = new OCRResult();
                    foreach (var indice in indices)
                    {
                        Image<Bgr, byte> imageResize = ResizeImage(PlateImagesList[indice], 250, 0);
                        ocrResult = engine.DetectText(imageResize.ToBitmap());
                        List<string> arrayresult = new List<string>();
                        // Do dai toi da cua bien co the chua la 12 ky tu(bao gom ca cac ky tu "-")
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
                                //CvInvoke.Imwrite("imgcropColor.jpg", PlateImagesList[indice]);
                                LPReturn obj = new LPReturn();
                                result = obj.Result(textPlates, true, accuracy, PlateImagesList[indice]);
                            }
                            else
                            {
                                LPReturn obj = new LPReturn();
                                result = obj.Result("Null", false, 0, PlateImagesList[indice]);                              
                            }
                        }
                    }
                }
                else
                {
                    LPReturn obj = new LPReturn();
                    result = obj.Result("No license plate found", false, 0, img);
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
                double scale = 0.00392;
                float nms_threshold = 0.4f;
                //Đọc ảnh từ đường dẫn
                var img = new Image<Bgr, byte>(path);
                //Set input đầu vào cho mô hình
                var input = DnnInvoke.BlobFromImage(img, scale, new Size(416, 416), new MCvScalar(0, 0, 0), swapRB: true, crop: false);
                Model.SetInput(input);
                VectorOfMat vectorOfMat = new VectorOfMat();
                //Đưa ra kết quả detect được từ mô hình
                Model.Forward(vectorOfMat, Model.UnconnectedOutLayersNames);
                Image<Bgr, byte> imageCrop = img.Clone();
                List<Image<Bgr, byte>> PlateImagesList = new List<Image<Bgr, byte>>();
                List<Rectangle> ListRec = new List<Rectangle>();
                List<float> confidences = new List<float>();
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
                            confidences.Add(confidence);
                            ListRec.Add(plate);
                        }
                    }
                }
                //Loại bỏ các boxes dư thừa bằng NMS
                List<int> indices = new List<int>();
                indices = DnnInvoke.NMSBoxes(ListRec.ToArray(), confidences.ToArray(), confThreshold, nms_threshold).ToList();
                //Đưa ra kết quả các ảnh đã detect được
                if (indices.Count > 0)
                {
                    OCRResult tempOCRResult = new OCRResult();
                    foreach (var indice in indices)
                    {
                        Image<Bgr, byte> imageResize = ResizeImage(PlateImagesList[indice], 250, 0);
                        ocrResult = engine.DetectText(imageResize.ToBitmap());
                        List<string> arrayresult = new List<string>();
                        // Do dai toi da cua bien co the chua la 12 ky tu(bao gom ca cac ky tu "-")
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
                                //CvInvoke.Imwrite("imgcropColor.jpg", PlateImagesList[indice]);
                                LPReturn obj = new LPReturn();
                                result = obj.Result(textPlates, true, accuracy, PlateImagesList[indice]);
                            }
                            else
                            {
                                LPReturn obj = new LPReturn();
                                result = obj.Result("Null", false, 0, PlateImagesList[indice]);
                            }
                        }
                    }
                }
                else
                {
                    LPReturn obj = new LPReturn();
                    result = obj.Result("No license plate found", false, 0, img);
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
            try
            {
                //Load model detect LP
                Model = DnnInvoke.ReadNetFromDarknet(PathConfig, PathWeights);
                Model.SetPreferableBackend(Emgu.CV.Dnn.Backend.OpenCV);
                Model.SetPreferableTarget(Target.Cpu);
                string root = Environment.CurrentDirectory;
                string modelPathroot = root + @"\en";
                config.det_infer = modelPathroot + @"\ch_PP-OCRv3_det_infer";
                config.cls_infer = modelPathroot + @"\ch_ppocr_mobile_v2.0_cls_infer";
                config.rec_infer = modelPathroot + @"\ch_ppocr_server_v2.0_rec_infer";
                config.keys = modelPathroot + @"\en_dict.txt";
                //Load library paddleOCR
                engine = new PaddleOCREngine(config, oCRParameter);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);                
            }
        }
        public static bool isValidPlatesNumber(string inputPlatesNumber)
        {
            string strRegex = @"(^[A-Z0-9]{2}-?[A-Z0-9]{1,3}-?[A-Z0-9]{1,2}$)|(^[A-Z0-9]{2,5}$)|(^[0-9]{2,3}-[0,9]{2}$)|(^[A-Z0-9]{2,3}-?[0-9]{4,5}$)|(^[A-Z]{2}-?[0-9]{0,4}$)|(^[0-9]{2}-?[A-Z0-9]{2,3}-?[A-Z0-9]{2,3}-?[0-9]{2}$)|(^[A-Z]{2}-?[0-9]{2}-?[0-9]{2}$)|(^[0-9]{3}-?[A-Z0-9]{2}$)";
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
        /// <summary>
        /// Ảnh cắt biển số
        /// </summary>
        public Image<Bgr,byte> imagePlate { get; set; }
    }

    public class LPReturn
    {
        // Create a class result for ResultLP.
        public ResultLP Result(string textPlate, bool statusPlate, double accPlate, Image<Bgr, byte> imageCrop)
        {
            ResultLP result = new ResultLP();
            result.textPlate = textPlate;
            if (statusPlate) result.statusPlate = 0;
            else result.statusPlate = 1;
            result.accPlate = accPlate;
            result.imagePlate = imageCrop;
            return result;
        }
    }
}
