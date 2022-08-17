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
        public void ProcessImage(string path)
        {
            try
            {
                string textPlates = "";
                float confThreshold = 0.8f;
                int imgDefaultSizeH = 0;//Kich thuoc default Height dau vao
                int imgDefaultSizeW = 0;//Kich thuoc default Weight dau vao
                //Detect biển số xe               
                var img = new Image<Bgr, byte>(path);
                if (img.Width % 32 != 0 || img.Height % 32 != 0)
                {
                    imgDefaultSizeW = img.Width / 32 * 32;
                    imgDefaultSizeH = img.Height / 32 * 32;
                }
                else
                {
                    imgDefaultSizeH = img.Height;
                    imgDefaultSizeW = img.Width;
                }
                //img = img.Resize(imgDefaultSizeW, imgDefaultSizeH, Inter.Cubic);
                img = ResizeImage(img, 1024,0);
                //Image imgPhoto = (Image)img.ToBitmap();
                //imgPhoto = FixedSize(imgPhoto, 1024, 1024);
                //CvInvoke.WaitKey();
                label6.Text = path;
                var input = DnnInvoke.BlobFromImage(img, 1 / 255.0, swapRB: false);
                Model.SetInput(input);
                VectorOfMat vectorOfMat = new VectorOfMat();
                Model.Forward(vectorOfMat, Model.UnconnectedOutLayersNames);
                VectorOfRect bboxes = new VectorOfRect();
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
                            Rectangle plate = new Rectangle(x - 5, y - 5, width + 10, height + 10);
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
                        ocrResult = engine.DetectText(PlateImagesList[i].ToBitmap());
                        List<string> arrayresult = new List<string>();
                        if (ocrResult.Text.Length > temp.Length && ocrResult.Text != String.Empty)
                        {
                            temp = ocrResult.Text;
                            for (int j = 0; j < ocrResult.TextBlocks.Count; j++)
                            {
                                string TextBlocksPlate = ocrResult.TextBlocks[j].Text;
                                TextBlocksPlate = Regex.Replace(TextBlocksPlate, @"[^0-9A-Z]", "");
                                if (isValidPlatesNumberForm(TextBlocksPlate))
                                {
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
                                ResultLPForm resultobj = obj.Result(textPlates, true);
                                label2.Text = "Biển số: " + resultobj.LP + ", status: " + resultobj.statusLP;
                            }
                            else
                            {
                                pictureBox3.Image = PlateImagesList[i].ToBitmap();
                                LPReturnForm obj = new LPReturnForm();
                                ResultLPForm resultobj = obj.Result("Null", false);
                                label2.Text = "Biển số: " + resultobj.LP + ", status: " + resultobj.statusLP;
                            }
                        }
                    }
                }
                else
                {
                    LPReturnForm obj = new LPReturnForm();
                    ResultLPForm resultobj = obj.Result("No license plate found", false);
                    label2.Text = "Biển số: " + resultobj.LP + ", status: " + resultobj.statusLP;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
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
            string strRegex = @"(^[A-Z0-9]{2,3}[A-Z0-9]{0,4}$)|(^[0-9]{4,5}$)|(^[0-9]{2}[A-Z]{1,2}[0-9]{4,5}$)|(^[A-Z0-9]{2}[A-Z0-9]{2,3}[A-Z0-9]{2,3}[0-9]{2}$)";
            Regex re = new Regex(strRegex);
            if (re.IsMatch(inputPlatesNumber))
                return (true);
            else
                return (false);
        }
        public static bool isValidPlatesNumber(string inputPlatesNumber)
        {
            string strRegex = @"(^[A-Z0-9]*$)";
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
                Model.SetPreferableBackend(Emgu.CV.Dnn.Backend.OpenCV);
                Model.SetPreferableTarget(Target.Cpu);
                engine = new PaddleOCREngine(config, oCRParameter);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Application.Exit();
            }
        }
        static Image FixedSize(Image imgPhoto, int Width, int Height)
        {
            int sourceWidth = imgPhoto.Width;
            int sourceHeight = imgPhoto.Height;
            int sourceX = 0;
            int sourceY = 0;
            int destX = 0;
            int destY = 0;

            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;

            nPercentW = ((float)Width / (float)sourceWidth);
            nPercentH = ((float)Height / (float)sourceHeight);
            if (nPercentH < nPercentW)
            {
                nPercent = nPercentH;
                destX = System.Convert.ToInt16((Width -
                              (sourceWidth * nPercent)) / 2);
            }
            else
            {
                nPercent = nPercentW;
                destY = System.Convert.ToInt16((Height -
                              (sourceHeight * nPercent)) / 2);
            }

            int destWidth = (int)(sourceWidth * nPercent);
            int destHeight = (int)(sourceHeight * nPercent);

            Bitmap bmPhoto = new Bitmap(Width, Height,
                              PixelFormat.Format24bppRgb);
            bmPhoto.SetResolution(imgPhoto.HorizontalResolution,
                             imgPhoto.VerticalResolution);

            Graphics grPhoto = Graphics.FromImage(bmPhoto);
            grPhoto.Clear(Color.Red);
            grPhoto.InterpolationMode =
                    InterpolationMode.HighQualityBicubic;

            grPhoto.DrawImage(imgPhoto,
                new Rectangle(destX, destY, destWidth, destHeight),
                new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                GraphicsUnit.Pixel);

            grPhoto.Dispose();
            return bmPhoto;
        }       
        private void pictureBox6_Click(object sender, EventArgs e)
        {

        }
        public static Image<Bgr, byte> ResizeImage(Image<Bgr, byte> imageOriginal, int width = 0, int height = 0)
        {
            var dim = new Size(0, 0);
            (int h, int w) = (imageOriginal.Width, imageOriginal.Height);
            if (width == 0 && height == 0)
            {
                return imageOriginal;
            }
            if (width != 0)
            {
                double r = height / (float)h;
                dim.Width = (int)(w * r);
                dim.Height = imageOriginal.Height;
            }
            else
            {
                double r = width / (float)w;
                dim.Width = (int)(w * r);
                dim.Height = imageOriginal.Width;
            }
            Image<Bgr, byte> imageReszie = imageOriginal.Resize(dim.Width, dim.Height, Inter.Cubic);
            return imageReszie;
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
