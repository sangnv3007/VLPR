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
using System.Drawing.Drawing2D;
using TensorFlow;

namespace Vietnamese_License_Plate_Recognition
{
    public partial class Form1 : Form
    {
        Net Model = null;
        string PathConfig = "yolov4-tiny-custom.cfg";
        string PathWeights = "yolov4-tiny-custom_final.weights";
        OCRModelConfig config = null;
        OCRParameter oCRParameter = new OCRParameter();
        OCRResult ocrResult = new OCRResult();
        PaddleOCREngine engine = null;
        //Net mobile_net = DnnInvoke.ReadNetFromTensorflow("saved_model.pb", "label_map.pbtxt");
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
        public void ProcessImage(string path)
        {
                string textPlates = "";
                float confThreshold = 0.8f;
                int imgDefaultSizeH = 0;//Kich thuoc default Height dau vao
                int imgDefaultSizeW = 0;//Kich thuoc default Weight dau vao
                //Thay đổi kich thước ảnh đầu vào           
                var img = new Image<Bgr, byte>(path);             
                //img = ResizeImage(img, 1280,0);
                if (img.Width % 32 != 0 || img.Height % 32 != 0)
                {
                    imgDefaultSizeW = img.Width / 32 * 32;
                    imgDefaultSizeH = img.Height / 32 * 32;
                    img = img.Resize(imgDefaultSizeW, imgDefaultSizeH, Inter.Cubic);
                }
                label6.Text = path;
                //Dự đoán vùng chứa biển số
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
                //Kiểm tra kết quả các ảnh đã detect được
                if (PlateImagesList.Count > 0)
                {
                    string temp = String.Empty;                    
                    for (int i = 0; i < PlateImagesList.Count; i++)
                    {
                    //Bitmap rotate_image = rotateImage(PlateImagesList[i].ToBitmap(), 8.5);
                    //PlateImagesList[i] = PlateImagesList[i].Rotate(7.25, new Bgr(43, 40, 33));
                    //CvInvoke.Imshow("gray", PlateImagesList[i].Mat);
                    //CvInvoke.WaitKey();
                    //var imgGray = rotate_image.ToImage<Gray, byte>().Convert<Gray, byte>().Clone();
                    //Image<Gray, byte> output_image = imgGray.SmoothGaussian(5);
                    //CvInvoke.Imshow("gray", output_image.Mat);
                    //CvInvoke.WaitKey();
                        //findLineImage(PlateImagesList[i]);
                        ocrResult = engine.DetectText(PlateImagesList[i].ToBitmap());
                        List<string> arrayresult = new List<string>();
                        // Do dai toi da cua bien co the chua la 12 ky tu(bao gom ca cac ky tu "-" hoặc ".")
                        if (ocrResult.Text.Length > temp.Length && ocrResult.Text != String.Empty && ocrResult.Text.Length <= 12)
                        {
                            temp = ocrResult.Text;
                            double accuracy = 1;
                            for (int j = 0; j < ocrResult.TextBlocks.Count; j++)
                            {
                                string TextBlocksPlate = ocrResult.TextBlocks[j].Text;
                                TextBlocksPlate = Regex.Replace(TextBlocksPlate, @"[^0-9A-Z\-]", "");
                                if (isValidPlatesNumberForm(TextBlocksPlate))
                                {
                                    if(ocrResult.TextBlocks[j].Score < accuracy)
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
                                pictureBox3.Image = PlateImagesList[i].ToBitmap();
                                textBox1.Text = textPlates;
                                LPReturnForm obj = new LPReturnForm();
                                ResultLPForm resultobj = obj.Result(textPlates, true, accuracy);
                                label2.Text = "Biển số: " + resultobj.textPlate + ", status: " + resultobj.statusPlate + ", acc: "+ resultobj.accPlate;
                            }
                            else
                            {
                                pictureBox3.Image = PlateImagesList[i].ToBitmap();
                                LPReturnForm obj = new LPReturnForm();
                                ResultLPForm resultobj = obj.Result("Null", false,0);
                                label2.Text = "Biển số: " + resultobj.textPlate + ", status: " + resultobj.statusPlate + ", acc: " + resultobj.accPlate;
                            }
                        }
                    }
                }
                else
                {
                    LPReturnForm obj = new LPReturnForm();
                    ResultLPForm resultobj = obj.Result("No license plate found", false,0);
                    label2.Text = "Biển số: " + resultobj.textPlate + ", status: " + resultobj.statusPlate + ", acc: " + resultobj.accPlate;
                }
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
        public static bool isValidPlatesNumberForm(string inputPlatesNumber)
        {
            string strRegex = @"(^[0-9]{2}-?[A-Z0-9]{1,3}$)|(^[0-9]{2,5}$)|(^[0-9]{2,3}-[0,9]{2}$)|(^[A-Z0-9]{2,3}-?[0-9]{4,5}$)|(^[A-Z]{2}-?[0-9]{0,4}$)|(^[0-9]{2}-?[A-Z0-9]{2,3}-?[A-Z0-9]{2,3}-?[0-9]{2}$)|(^[A-Z]{2}-?[0-9]{2}-?[0-9]{2}$)|(^[0-9]{3}-?[A-Z0-9]{2}$)";
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
                //Model.SetPreferableBackend(Emgu.CV.Dnn.Backend.OpenCV);
                //Model.SetPreferableTarget(Target.Cpu);
                engine = new PaddleOCREngine(config, oCRParameter);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Application.Exit();
            }

        }
        private void pictureBox6_Click(object sender, EventArgs e)
        {

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
        public static void findLineImage(Image<Bgr, byte> img)
        {
            Image<Gray, byte> gray_image = img.Convert<Gray, byte>();
            Image<Gray, byte> img_canny = new Image<Gray, byte>(img.Width, img.Height);
            CvInvoke.WaitKey();
            CvInvoke.Canny(gray_image, img_canny, 30, 100, apertureSize: 3);
            //CvInvoke.MedianBlur(gray_image, gray_image, 3);
            CvInvoke.Imshow("img", img_canny.Mat);
            CvInvoke.WaitKey();
            LineSegment2D[] array = CvInvoke.HoughLinesP(img_canny, 1, Math.PI / 180, 100, minLineLength: img.Width/4, maxGap: img.Height/4);
            Console.WriteLine(array[0]);
            //double angle = 0;
            //int nline = lines.Length;
      
        }

    }
    public class ResultLPForm
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

    public class LPReturnForm
    {
        // Create a class result for ResultLP.
        public ResultLPForm Result(string textPlate, bool statusPlate, double accPlate)
        {
            ResultLPForm result = new ResultLPForm();
            result.textPlate = textPlate;
            if (statusPlate) result.statusPlate = 0;
            else result.statusPlate = 1;
            result.accPlate = accPlate;
            return result;
        }
    }
}
