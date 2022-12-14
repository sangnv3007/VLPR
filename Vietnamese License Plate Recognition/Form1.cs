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
using TD.VLPR;
using System.Threading;

namespace Vietnamese_License_Plate_Recognition
{
    public partial class Form1 : Form
    {
        Net Model = null;
        string PathConfig = "yolov4-tiny-custom.cfg";
        string PathWeights = "yolov4-tiny-custom_final.weights";
        OCRParameter oCRParameter = new OCRParameter();
        OCRModelConfig config = new OCRModelConfig();
        OCRResult ocrResult = new OCRResult();
        PaddleOCREngine engine = null;
        int number_failed = 0;
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

            //Thay đổi kich thước ảnh đầu vào

            var img = new Image<Bgr, byte>(path);

            //Dự đoán vùng chứa biển số

            double scale = 1/255f;
            //float nms_threshold = 0.4f;
            //Crop ảnh để detect
            var input = DnnInvoke.BlobFromImage(img, scale, new Size(416, 416), new MCvScalar(0, 0, 0), swapRB: true, crop: false);
            Model.SetInput(input);
            VectorOfMat vectorOfMat = new VectorOfMat();
            Model.Forward(vectorOfMat, Model.UnconnectedOutLayersNames);
            Image<Bgr, byte> imageCrop = img.Clone();
            List<Image<Bgr, byte>> PlateImagesList = new List<Image<Bgr, byte>>();
            //List<Rectangle> ListRec = new List<Rectangle>();
            List<float> confidences = new List<float>();

            for (int k = 0; k < vectorOfMat.Size; k++)
            {
                var mat = vectorOfMat[k];
                var data = ArrayTo2DList(mat.GetData());
                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var confidence = row.Skip(5).Max();
                    //Kiem tra nguong tin cay
                    if (confidence > confThreshold)
                    {
                        var center_x = (int)(row[0] * img.Width);
                        var center_y = (int)(row[1] * img.Height);

                        var width = (int)(row[2] * img.Width);
                        var height = (int)(row[3] * img.Height);

                        var x = center_x - (width / 2);
                        var y = center_y - (height / 2);
                        Rectangle plate = new Rectangle(x, y, width, height);
                        imageCrop = img.Clone();
                        imageCrop.ROI = plate;
                        PlateImagesList.Add(imageCrop);
                        confidences.Add(confidence);
                        //CvInvoke.Rectangle(img, new Rectangle(x, y, width, height), new MCvScalar(0, 0, 255), 2); //Ve khung hinh chua bien so
                        CvInvoke.Imshow("Anhrec", imageCrop.Mat);
                        CvInvoke.WaitKey();
                        //ListRec.Add(plate);
                    }
                }
            }

            //Đưa ra kết quả các ảnh đã detect được
            //List<int> indices = DnnInvoke.NMSBoxes(ListRec.ToArray(), confidences.ToArray(), confThreshold, nms_threshold).ToList();
            if (confidences.Count > 0)
            {
                //OCRResult tempOCRResult = new OCRResult();
                var max_indices = confidences.IndexOf(confidences.Max());
                Image<Bgr, byte> imageResize = ResizeImage(PlateImagesList[max_indices], width: 250, 0);
                //CvInvoke.Imshow("abc", imageResize.Mat);
                ocrResult = engine.DetectText(imageResize.ToBitmap());
                List<string> arrayresult = new List<string>();
                // Do dai toi da cua bien co the chua la 12 ky tu(bao gom ca cac ky tu "-")
                if (String.IsNullOrEmpty(ocrResult.Text) || ocrResult.Text.Length <= 12)
                {
                    double accuracy = 1;
                    for (int j = 0; j < ocrResult.TextBlocks.Count; j++)
                    {
                        string TextBlocksPlate = ocrResult.TextBlocks[j].Text;
                        TextBlocksPlate = Regex.Replace(TextBlocksPlate, @"[^A-Z0-9\-]|^-|-$", "");
                        if (isValidPlatesNumberForm(TextBlocksPlate))
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
                        pictureBox3.Image = PlateImagesList[max_indices].ToBitmap();
                        CvInvoke.Imwrite("imgcropColor.jpg", imageResize);
                        textBox1.Text = textPlates;
                        LPReturnForm obj = new LPReturnForm();
                        ResultLPForm resultobj = obj.Result(textPlates, true, accuracy);
                        label2.Text = "Biển số: " + resultobj.textPlate + ", status: " + resultobj.statusPlate + ", acc: " + resultobj.accPlate;
                    }
                    else
                    {
                        pictureBox3.Image = PlateImagesList[max_indices].ToBitmap();
                        CvInvoke.Imwrite("imgcropColor.jpg", imageResize);
                        LPReturnForm obj = new LPReturnForm();
                        ResultLPForm resultobj = obj.Result("Null", false, 0);
                        label2.Text = "Biển số: " + resultobj.textPlate + ", status: " + resultobj.statusPlate + ", acc: " + resultobj.accPlate;
                    }
                }
                else
                {
                    CvInvoke.Imwrite("imgcropColor.jpg", imageResize);
                    LPReturnForm obj = new LPReturnForm();
                    ResultLPForm resultobj = obj.Result("Null", false, 0);
                    label2.Text = "Biển số: " + resultobj.textPlate + ", status: " + resultobj.statusPlate + ", acc: " + resultobj.accPlate;
                }
            }
            else
            {
                LPReturnForm obj = new LPReturnForm();
                ResultLPForm resultobj = obj.Result("No license plate found", false, 0);
                label2.Text = "Biển số: " + resultobj.textPlate + ", status: " + resultobj.statusPlate + ", acc: " + resultobj.accPlate;
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            LoadModelRecognize();
        }
        // method containing the regex
        public static bool isValidPlatesNumberForm(string inputPlatesNumber)
        {
            string strRegex = @"(^[A-Z0-9]{2}-?[A-Z0-9]{0,3}-?[A-Z0-9]{1,2}$)|(^[A-Z0-9]{2,5}$)|(^[0-9]{2,3}-[0,9]{2}$)|(^[A-Z0-9]{2,3}-?[0-9]{4,5}$)|(^[A-Z]{2}-?[0-9]{0,4}$)|(^[0-9]{2}-?[A-Z0-9]{2,3}-?[A-Z0-9]{2,3}-?[0-9]{2}$)|(^[A-Z]{2}-?[0-9]{2}-?[0-9]{2}$)|(^[0-9]{3}-?[A-Z0-9]{2}$)";
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
                string root = Environment.CurrentDirectory;
                string modelPathroot = root + @"\en";
                config.det_infer = modelPathroot + @"\ch_PP-OCRv3_det_infer";
                config.cls_infer = modelPathroot + @"\ch_ppocr_mobile_v2.0_cls_infer";
                config.rec_infer = modelPathroot + @"\ch_ppocr_server_v2.0_rec_infer";
                config.keys = modelPathroot + @"\en_dict.txt";
                engine = new PaddleOCREngine(config, oCRParameter);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Application.Exit();
            }

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
            Image<Bgr, byte> imageReszie = imageOriginal.Resize(dim.Width, dim.Height, Inter.Area);
            return imageReszie;
        }
        public static Image<Bgr, byte> rotateImage(Image<Bgr, byte> img)
        {
            var SE = Mat.Ones(1, 1, DepthType.Cv8U, 1);//adjust
            var binary = img.Convert<Gray, byte>()
                .SmoothGaussian(3)
                .ThresholdBinary(new Gray(100), new Gray(255))
                .MorphologyEx(MorphOp.Dilate, SE, new Point(-1, -1), 1, BorderType.Default, new MCvScalar(0))
                .Erode(1);
            var points = new VectorOfPoint();
            var rotatedImage = img.Clone();
            CvInvoke.FindNonZero(binary, points);
            if (points.Length > 0)
            {
                double temp = 0.0;
                var minAreaRect = CvInvoke.MinAreaRect(points);
                //Console.WriteLine(minAreaRect.Angle);
                var rotationMatrix = new Mat(2, 3, DepthType.Cv32F, 1);
                if (minAreaRect.Angle > temp)
                {
                    temp = minAreaRect.Angle;
                    double angle = temp < 45 ? temp : temp - 90;
                    CvInvoke.GetRotationMatrix2D(minAreaRect.Center, angle, 1.0, rotationMatrix);
                    CvInvoke.WarpAffine(img, rotatedImage, rotationMatrix, img.Size, borderMode: BorderType.Replicate);
                }
            }
            return rotatedImage;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog FBD = new FolderBrowserDialog();
            if( FBD.ShowDialog()==DialogResult.OK)
            {
                ListBoxFIle.Items.Clear();
                string supportedExtensions = "*.jpg,*.png,*.bmp,*.jpeg";
                foreach (string imageFile in Directory.GetFiles(FBD.SelectedPath, "*.*", SearchOption.AllDirectories)
                    .Where(s => supportedExtensions.Contains(Path.GetExtension(s).ToLower())))
                {
                    ListBoxFIle.Items.Add(imageFile);
                }
                string[] dirs = Directory.GetDirectories(FBD.SelectedPath);
                foreach(string dir in dirs)
                {
                    ListBoxFIle.Items.Add(Path.GetFileName(dir));
                }    
            }    
        }
        //Button click vào items
        private void ListBoxFIle_Click(object sender, EventArgs e)
        {
            /*
            textBox2.Text = "";
            label13.Text = "*";

            string pathImage = ListBoxFIle.SelectedItem.ToString();
            using (Bitmap tmpBitmap = new Bitmap(pathImage))
            {
                var img = new Image<Bgr, byte>(pathImage);
                pictureBox1.Image = new Bitmap(tmpBitmap);
                var extracter = new NumberPlateExtracter();
                Stopwatch swObj = new Stopwatch();
                //Thời gian bắt đầu
                swObj.Start();
                ResultLP resultobj = extracter.ProcessImage(ListBoxFIle.SelectedItem.ToString());            
                //Thời gian kết thúc
                swObj.Stop();
                if(resultobj.accPlate != 0)
                {                
                    textBox2.Text = resultobj.textPlate;                                    
                }
                else
                {
                    label13.Text = resultobj.textPlate; 
                }
                pictureBox2.Image = resultobj.imagePlate.ToBitmap();
                label7.Text = img.Width + "x" + img.Height;
                label9.Text = Math.Round(swObj.Elapsed.TotalSeconds, 2).ToString() + " giây";

            }
            */
        }
        //Button tự động nhận diện trong folder
        private void button5_Click(object sender, EventArgs e)
        {
            label13.Text = "0";
            for (int i = 0; i < ListBoxFIle.Items.Count; i++)
            {
                ListBoxFIle.SetSelected(i, true);
                Thread.Sleep(500);
            }
        }
        private void ListBoxFIle_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshForm();
            string curItem = ListBoxFIle.SelectedItem.ToString();
            var img = new Image<Bgr, byte>(curItem);
            pictureBox1.Image = new Bitmap(curItem);
            var extracter = new NumberPlateExtracter();
            Stopwatch swObj = new Stopwatch();
            //Thời gian bắt đầu
            swObj.Start();
            ResultLP resultobj = extracter.ProcessImage(curItem);
            //Thời gian kết thúc
            swObj.Stop();
            if (resultobj.accPlate != 0)
            {             
                textBox2.Text = resultobj.textPlate;
            }
            else
            {
                number_failed++;
                label13.Text = number_failed.ToString();
            }
            pictureBox2.Image = resultobj.imagePlate.ToBitmap();
            label7.Text = img.Width + "x" + img.Height;
            label9.Text = Math.Round(swObj.Elapsed.TotalSeconds, 2).ToString() + " giây";         

        }
        public void RefreshForm()
        {
            pictureBox1.Refresh();
            textBox2.Refresh();
            label13.Refresh();
            pictureBox2.Refresh();
            label7.Refresh();
            label9.Refresh();
        }
        //Button previous image
        private void button3_Click(object sender, EventArgs e)
        {
            if (ListBoxFIle.SelectedIndex > 0)
            {
                ListBoxFIle.SelectedIndex = ListBoxFIle.SelectedIndex - 1;
            }
        }
        //Button next image
        private void button4_Click(object sender, EventArgs e)
        {
            if (ListBoxFIle.SelectedIndex < ListBoxFIle.Items.Count - 1)
            {
                ListBoxFIle.SelectedIndex = ListBoxFIle.SelectedIndex + 1;
            }
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
