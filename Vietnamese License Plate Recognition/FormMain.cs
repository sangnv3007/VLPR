﻿using System;
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
using Tesseract;
using FaceRecognitionDotNet;

namespace Vietnamese_License_Plate_Recognition
{
    public partial class FormMain : Form
    {
        //Config YOLO
        Net Model = null;
        string PathConfig = "yolov4-tiny-custom.cfg";
        string PathWeights = "yolov4-tiny-custom_final.weights";
        //string PathClassNames = "classes.names";
        OCRParameter oCRParameter = new OCRParameter();
        OCRModelConfig config = new OCRModelConfig(); 
        OCRResult ocrResult = new OCRResult();
        PaddleOCREngine engine = null;
        FaceRecognition fr;
        int number_failed = 0; //Số lượng nhận dạng thất bại
        //
        System.Timers.Timer timerAutoRec = new System.Timers.Timer(1000); //Chu kỳ nhận diện biển số xe. Mặc định 1000 mili giây
        System.Timers.Timer timerAutoMove = new System.Timers.Timer(1); //Chu kỳ phát hiện chuyển động. Mặc định 1 mili giây
        bool isStop = true; //Cờ bật/tắt video
        VideoCapture capture; //Khởi tạo video capture
        Rectangle rec = new Rectangle();
        System.Drawing.Point startPoint = new System.Drawing.Point();
        System.Drawing.Point endPoint = new System.Drawing.Point();
        bool isDrawing, isMouseDown, isMoveMent; //Các biến xử lý vẽ bbox trên PictureBox
        int MotionThreshold = 16; //varThreshold: Ngưỡng để xác định liệu pixel có được coi là nền hay không
        IBackgroundSubtractor backgroundSubtractor; // Khởi tạo đối tượng IBackgroundSubtractor
        public FormMain()
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
                using (System.Drawing.Image sourceImg = System.Drawing.Image.FromFile(open.FileName))
                {
                    System.Drawing.Image clonedImg = new Bitmap(sourceImg.Width, sourceImg.Height, PixelFormat.Format32bppArgb);
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
                //TesseractOCR(open.FileName);         
                ProcessImage(open.FileName);      
                //Thời gian kết thúc
                swObj.Stop();
                //Console.WriteLine(Math.Round(swObj.Elapsed.TotalSeconds, 2).ToString() + " giây");
                //Tổng thời gian thực hiện               
                label5.Text = Math.Round(swObj.Elapsed.TotalSeconds, 2).ToString() + " giây";
            }
        }      
        public void TesseractOCR(string path)
        {
            // Load the image of the license plate
            Bitmap licensePlate = new Bitmap(path);
            // Tạo đối tượng ảnh xám
            //Image<Gray, byte> grayImage = licensePlate.ToImage<Gray, byte>();
            // Create a Tesseract OCR engine
            //Image<Bgr, byte> imageResize = ResizeImage(licensePlate.ToImage<Bgr, byte>(), width: 250, 0);
            //CvInvoke.Imshow("abc", imageResize.Mat);        
            using (var engine = new TesseractEngine(@"./tessdata", "mymodel_process_0404", EngineMode.Default))
            {
                // Set the page segmentation mode to automatic
                engine.SetVariable("tessedit_pageseg_mode", "auto");

                // Set the whitelist to alphanumeric characters only
                engine.SetVariable("tessedit_char_whitelist", "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ");

                // Set the image to recognize
                using (var page = engine.Process(licensePlate, PageSegMode.SingleWord))
                {
                    // Get the recognized text
                    var text = page.GetText();
                    //MessageBox.Show(text);
                    // Print the recognized text
                    textBox1.Text = text;
                    //Console.WriteLine("License plate: " + text);
                }
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

            string textPlates = ""; // Thông tin biển số
            float confThreshold = 0.7f; // Ngưỡng tin cậy
            VectorOfMat vectorOfMat = new VectorOfMat();
            List<Image<Bgr, byte>> PlateImagesList = new List<Image<Bgr, byte>>();// List ảnh crop biển số
            List<float> confidences = new List<float>();//List độ tin cậy 
            var img = new Image<Bgr, byte>(path);//Đọc ảnh vào từ đường dẫn
            double scale = 1/255f;//Tham số chuẩn hoá ảnh 0=>1
            var input = DnnInvoke.BlobFromImage(img, scale, new Size(416, 416), new MCvScalar(0, 0, 0), swapRB: true, crop: false);
            Model.SetInput(input);    
            Model.Forward(vectorOfMat, Model.UnconnectedOutLayersNames);       
            Image<Bgr, byte> imageCrop = img.CopyBlank();
            img.CopyTo(imageCrop);//Copy ảnh gốc

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
                        //CvInvoke.Imshow("Anhrec", imageCrop.Mat);
                        //CvInvoke.WaitKey();
                        //ListRec.Add(plate);
                    }
                }
            }

            //Đưa ra kết quả các ảnh đã detect được
            //List<int> indices = DnnInvoke.NMSBoxes(ListRec.ToArray(), confidences.ToArray(), confThreshold, nms_threshold).ToList();
            List<string> arrayresult = new List<string>();
            if (confidences.Count > 0)
            {
                //OCRResult tempOCRResult = new OCRResult();
                var max_indices = confidences.IndexOf(confidences.Max());
                Image<Bgr, byte> imageResize = ResizeImage(PlateImagesList[max_indices], width: 200, 0);
                //CvInvoke.Imshow("Anhrec", imageResize.Mat);
                //CvInvoke.WaitKey(0);
                ocrResult = engine.DetectText(imageResize.ToBitmap());
                // Do dai toi da cua bien co the chua la 12 ky tu(bao gom ca cac ky tu "-")
                if (!string.IsNullOrEmpty(ocrResult.Text))
                {
                    double accuracy = 1;
                        
                    for (int j = 0; j < ocrResult.TextBlocks.Count; j++)
                    {
                        string TextBlocksPlate = ocrResult.TextBlocks[j].Text.ToUpper();
                        var threshholdOCR = 0.7;
                        TextBlocksPlate = Regex.Replace(TextBlocksPlate, @"[^A-Z0-9\-]|^-|-$", "");
                        if(TextBlocksPlate.Length <= 12 && checkBlockLP(TextBlocksPlate) && ocrResult.TextBlocks[j].Score > threshholdOCR)
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
                        //CvInvoke.Imwrite("imgcropColor.jpg", imageResize);
                        textBox1.Text = textPlates;
                        LPReturnForm obj = new LPReturnForm();
                        ResultLPForm resultobj = obj.Result(textPlates, true, accuracy);
                        label2.Text = "Biển số: " + resultobj.textPlate + ", status: " + resultobj.statusPlate + ", acc: " + resultobj.accPlate;
                    }
                    else
                    {
                        pictureBox3.Image = PlateImagesList[max_indices].ToBitmap();
                        //CvInvoke.Imwrite("imgcropColor.jpg", imageResize);
                        LPReturnForm obj = new LPReturnForm();
                        ResultLPForm resultobj = obj.Result("Null", false, 0);
                        label2.Text = "Biển số: " + resultobj.textPlate + ", status: " + resultobj.statusPlate + ", acc: " + resultobj.accPlate;
                    }
                }
                else
                {
                    //CvInvoke.Imwrite("imgcropColor.jpg", imageResize);
                    LPReturnForm obj = new LPReturnForm();
                    ResultLPForm resultobj = obj.Result("Null", false, 0);
                    label2.Text = "Biển số: " + resultobj.textPlate + ", status: " + resultobj.statusPlate + ", acc: " + resultobj.accPlate;
                }
            }
            else
            {
                Image<Bgr, byte> imgResize = ResizeImage(img, width: 250, 0);
                ocrResult = engine.DetectText(imgResize.ToBitmap());
                if (ocrResult.Text.Length != 0)
                {

                    for (int i = 0; i < ocrResult.TextBlocks.Count; i++)
                    {
                        string TextBlocksPlate = Regex.Replace(ocrResult.TextBlocks[i].Text, @"[^A-Z0-9\-]|^-|-$", "");
                        if (isVNLicensePlate(TextBlocksPlate) && TextBlocksPlate.Length <= 12)
                        {
                            textBox1.Text = TextBlocksPlate;
                            var boxPoints = ocrResult.TextBlocks[i];

                            PointF[] source_points = new PointF[4];
                            source_points[0] = new PointF(boxPoints.BoxPoints[0].X, boxPoints.BoxPoints[0].Y);
                            source_points[1] = new PointF(boxPoints.BoxPoints[3].X, boxPoints.BoxPoints[3].Y);
                            source_points[2] = new PointF(boxPoints.BoxPoints[2].X, boxPoints.BoxPoints[2].Y);
                            source_points[3] = new PointF(boxPoints.BoxPoints[1].X, boxPoints.BoxPoints[1].Y);
                            Image<Bgr, byte> crop = PerspectiveTransform(imgResize, source_points);
                            textPlates = TextBlocksPlate;
                            LPReturnForm obj = new LPReturnForm();
                            ResultLPForm resultobj = obj.Result(textPlates, true, ocrResult.TextBlocks[i].Score);
                            label2.Text = "Biển số: " + resultobj.textPlate + ", status: " + resultobj.statusPlate + ", acc: " + resultobj.accPlate;
                            break;
                        }
                    }
                    if (textPlates == String.Empty)
                    {
                        LPReturnForm obj = new LPReturnForm();
                        ResultLPForm resultobj = obj.Result("No license plate found", false, 0);
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
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            LoadModelRecognize();
        }

        /// <summary>
        /// Hàm kéo dãn ảnh
        /// </summary>
        /// <param name="inputPlatesNumber"></param>
        /// <returns></returns>
        /// 


        // method containing the regex
        public static bool checkBlockLP(string inputPlatesNumber)
        {
            string strRegex = @"(^[A-Z0-9]{2}-?[A-Z0-9]{0,3}-?[A-Z0-9]{1,2}$)|(^[A-Z0-9]{2,5}$)|(^[0-9]{2,3}-[0,9]{2}$)|(^[A-Z0-9]{2,3}-?[0-9]{4,5}$)|(^[A-Z]{2}-?[0-9]{0,4}$)|(^[0-9]{2}-?[A-Z0-9]{2,3}-?[A-Z0-9]{2,3}-?[0-9]{2}$)|(^[A-Z]{2}-?[0-9]{2}-?[0-9]{2}$)|(^[0-9]{3}-?[A-Z0-9]{2}$)";
            Regex re = new Regex(strRegex);
            if (re.IsMatch(inputPlatesNumber))
                return (true);
            else
                return (false);
        }

        public static bool isVNLicensePlate(string inputPlatesNumber)
        {
            string strRegex = @"(^[0-9]{2}[A-Z]{1}-?[0-9]{4,5}$)|(^[A-Z]{2}-?[0-9]{4,5}$)|(^[0-9]{2}-?[A-Z]{2}-?[0-9]{3}-?[0-9]{2}$)|(^[A-Z]{2}-?[0-9]{0,4}$)|(^[A-Z]{2}-?[0-9]{2}-?[0-9]{2}$)|(^[0-9]{2}-?[A-Z0-9]{2,3}-?[A-Z0-9]{2,3}-?[0-9]{2}$)|(^[0-9]{2}-?[0-9]{3}-?[A-Z]{2}$)";
            Regex re = new Regex(strRegex);
            if (re.IsMatch(inputPlatesNumber))
                return (true);
            else
                return (false);
        }
        public Image<Bgr, byte> PerspectiveTransform(Image<Bgr, byte> imageCropDraw, PointF[] points)
        {
            // Use L2 norm           
            double width_AD = Math.Sqrt(Math.Pow((points[0].X - points[3].X), 2) + Math.Pow((points[0].Y - points[3].Y), 2));
            double width_BC = Math.Sqrt(Math.Pow((points[1].X - points[2].X), 2) + Math.Pow((points[1].Y - points[2].Y), 2));
            double maxWidth = Math.Max((int)width_AD, (int)width_BC);
            double height_AB = Math.Sqrt(Math.Pow((points[0].X - points[1].X), 2) + Math.Pow((points[0].Y - points[1].Y), 2));
            double height_CD = Math.Sqrt(Math.Pow((points[2].X - points[3].X), 2) + Math.Pow((points[2].Y - points[3].Y), 2));
            double maxHeight = Math.Max((int)height_AB, (int)height_CD);
            PointF[] output_pts = new PointF[4];
            output_pts[0] = new PointF(0, 0);
            output_pts[1] = new PointF(0, (float)maxHeight - 1);
            output_pts[2] = new PointF((float)maxWidth - 1, (float)maxHeight - 1);
            output_pts[3] = new PointF((float)maxWidth - 1, 0);
            Mat M = CvInvoke.GetPerspectiveTransform(points, output_pts);
            CvInvoke.WarpPerspective(imageCropDraw, imageCropDraw, M, new Size((int)maxWidth, (int)maxHeight), interpolationType: Inter.Linear);
            return imageCropDraw;
        }
        public void LoadModelRecognize()
        {
            try
            {
                string root = Environment.CurrentDirectory;
                //var rootPaddleOCR = PaddleOCRSharp.EngineBase.GetRootDirectory();
                /*Version 1 */
                //string modelPathroot = root + @"\en";
                //string modelRec = root + "\\models";
                //config.det_infer = modelPathroot + @"\ch_PP-OCRv3_det_infer";
                //config.cls_infer = modelPathroot + @"\ch_ppocr_mobile_v2.0_cls_infer";
                //config.rec_infer = modelPathroot + @"\ch_ppocr_server_v2.0_rec_infer";

                /*Version 2 */
                string modelPathRoot = root + @"\ml";
                //string modelRec = root + "\\models";
                config.det_infer = modelPathRoot + @"\inference\ch_PP-OCRv3_det_infer";
                config.cls_infer = modelPathRoot + @"\inference\ch_ppocr_mobile_v2.0_cls_infer";
                config.rec_infer = modelPathRoot + @"\inference\ch_ppocr_server_v2.0_rec_infer";
                //oCRParameter.use_angle_cls = false;
                //oCRParameter.cls = false;

                config.keys = modelPathRoot + @"\inference\en_dict.txt";           
                engine = new PaddleOCREngine(config, oCRParameter);
                Model = DnnInvoke.ReadNetFromDarknet(Path.Combine(modelPathRoot,PathConfig), Path.Combine(modelPathRoot,PathWeights));//Load model detect LP
                Model.SetPreferableBackend(Emgu.CV.Dnn.Backend.OpenCV);
                Model.SetPreferableTarget(Target.Cpu);
                //fr = FaceRecognition.Create(modelRec);
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

            Image<Bgr, byte> imageReszie = imageOriginal.Resize(dim.Width,
                dim.Height > dim.Width ? dim.Width : dim.Height,
                Inter.Cubic);
            return imageReszie;
        }
        public static Image<Bgr, byte> rotateImage(Image<Bgr, byte> img)
        {
            var SE = Mat.Ones(1, 1, DepthType.Cv8U, 1);//adjust
            var binary = img.Convert<Gray, byte>()
                .SmoothGaussian(3)
                .ThresholdBinary(new Gray(100), new Gray(255))
                .MorphologyEx(MorphOp.Dilate, SE, new System.Drawing.Point(-1, -1), 1, BorderType.Default, new MCvScalar(0))
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
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(curItem);
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
                CvInvoke.Imwrite(@"D:\PaddleOCR_Train\Recognition PaddleOCR\data_train\LpCrop_" + nameWithoutExtension + ".jpg", resultobj.imagePlate);
                pictureBox2.Image = resultobj.imagePlate.ToBitmap();
            }
            else
            {
                number_failed++;
                label13.Text = number_failed.ToString();
            }
            
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
        //Button chuyển về ảnh phía trước
        private void button3_Click(object sender, EventArgs e)
        {
            if (ListBoxFIle.SelectedIndex > 0)
            {
                ListBoxFIle.SelectedIndex--;
            }
        }
        //Button chuyển về ảnh tiếp theo
        private void button4_Click(object sender, EventArgs e)
        {
            if (ListBoxFIle.SelectedIndex < ListBoxFIle.Items.Count - 1)
            {
                ListBoxFIle.SelectedIndex ++;
            }
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int tabText = tabControl1.SelectedIndex;
            if(tabText == 2)
            {
                AutoRecLPVideo();
            }    
            if(tabText==3)
            {
                AutoMovDetectionVideo();            
            }
            if (tabText == 4)
            {
                FaceRecognitionAuto();
            }
        }

        private void FaceRecognitionAuto()
        {
            capture = new VideoCapture(0, VideoCapture.API.DShow);
            Mat frame = new Mat();
            VectorOfMat output = new VectorOfMat();
            this.KeyPreview = true;
            this.KeyPress += (sender, eg) =>
            {
                if (eg.KeyChar == 'q' || eg.KeyChar == (char)Keys.Escape)
                {
                    isStop = false;
                    Application.Exit();
                }
            };
            while (isStop)
            {
                capture.Read(frame);
                var bitmap = frame.ToBitmap();
                PicFace.Image = bitmap;
                var image = FaceRecognition.LoadImage(bitmap);
                var faces = fr.FaceLocations(image);
                var imageCrop = FaceRecognition.CropFaces(image, faces).FirstOrDefault();
                if (imageCrop!= null) PicCropFace.Image = imageCrop.ToBitmap();
                //// Vẽ hình chữ nhật xung quanh khuôn mặt
                foreach (var face in faces)
                {
                    var rect = new Rectangle(face.Left, face.Top, face.Right - face.Left, face.Bottom - face.Top);
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        CvInvoke.Rectangle(frame, rect, new MCvScalar(0, 255, 0), 2);
                    }
                }
                PicFace.Image = frame.ToBitmap();
                Application.DoEvents();
            }
            capture.Dispose();
            CvInvoke.DestroyAllWindows();
        }

        private void AutoMovDetectionVideo()
        {
            capture = new VideoCapture(0, VideoCapture.API.DShow);
            backgroundSubtractor = new BackgroundSubtractorMOG2(varThreshold: MotionThreshold);
            //Ẩn các nút xác nhận và xoá vùng định nghĩa
            btn_clearRec2.Enabled = false;
            btn_confirm2.Enabled = false;
            Mat frame = new Mat();    
            // Lắng nghe sự kiện bàn phím trực tiếp từ ứng dụng Windows Forms
            this.KeyPreview = true;
            this.KeyPress += (sender, e) =>
            {
                if (e.KeyChar == 'q' || e.KeyChar == (char)Keys.Escape)
                {
                    isStop = false;
                    Application.Exit();
                }
            };
            timerAutoMove.Elapsed += (sender, e) => {  
                
                Mat frame_temp = capture.QueryFrame();
                Mat smoothFrame = new Mat();
                CvInvoke.GaussianBlur(frame_temp, smoothFrame, new Size(3, 3), 1);
                // Thực hiện phép trừ nền
                Mat foregroundMask = new Mat();
                backgroundSubtractor.Apply(smoothFrame, foregroundMask);
                // Điều chỉnh ngưỡng mặt nạ nền
                CvInvoke.Threshold(foregroundMask, foregroundMask, 200, 240, ThresholdType.Binary);       
                Mat kernel = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(3, 3), new System.Drawing.Point(-1, -1));
                //Bỏ các điểm nhiễu và cải thiện chất lượng của mặt nạ.
                CvInvoke.Erode(foregroundMask, foregroundMask, kernel, new System.Drawing.Point(-1, -1), 1, BorderType.Default, new MCvScalar(1));
                CvInvoke.Dilate(foregroundMask, foregroundMask, kernel, new System.Drawing.Point(-1, -1), 2, BorderType.Default, new MCvScalar(1));
                // Hiệu chỉnh hình ảnh nhị phân
                Mat kernel_optimize = Mat.Ones(7, 3, DepthType.Cv8U, 1);
                CvInvoke.MorphologyEx(foregroundMask, foregroundMask, MorphOp.Close, kernel_optimize, new System.Drawing.Point(-1, -1), 1, BorderType.Reflect, new MCvScalar(0));
                // Phát hiện contours và vẽ các bbox
                VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
                CvInvoke.FindContours(foregroundMask, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);
                int minArea = 900;//Kích thước vùng nhỏ nhất
                //Lấy ra danh sách các contours

                for (int i = 0; i < contours.Size; i++)
                {
                    var bbox = CvInvoke.BoundingRectangle(contours[i]);
                    var area = bbox.Width * bbox.Height;
                    bool isBboxInRec = bbox.X >= rec.X && bbox.Y >= rec.Y && (bbox.X + bbox.Width) <= (rec.X + rec.Width) && (bbox.Y + bbox.Height) <= (rec.Y + rec.Height);// Kiểm tra bbox có trong vùng định ngĩa không?
                    if (area > minArea)
                    {
                        if (isBboxInRec && bbox != null)
                        {
                            isMoveMent = true;                          
                            //CvInvoke.Rectangle(frame_temp, bbox, new MCvScalar(0, 255, 0), 2);                          
                        }
                        else
                        {
                            isMoveMent = false;
                        }
                    }

                }
                ptb_movementDet.Image = frame_temp.ToBitmap();
            };
            timerAutoMove.Start();         
            while (isStop)
            {
                capture.Read(frame);
                ptb_movementDet.Image = frame.ToBitmap();
                Application.DoEvents();
            }
            timerAutoMove.Stop();
            capture.Dispose();
            CvInvoke.DestroyAllWindows();
        }
        public ResultLP DetectorVideo(Mat frame, float confThreshold)
        {
            //Định nghĩa các biến và đối tượng trả về
            ResultLP result = new ResultLP();
            string textPlates = string.Empty;
            VectorOfMat output = new VectorOfMat();
            double scale = 1 / 255f;
            List<float> scores = new List<float>();
            List<Image<Bgr, byte>> PlateImagesList = new List<Image<Bgr, byte>>();
            Image<Bgr, byte> image = frame.ToImage<Bgr, byte>();
            //Lấy kết quả trả về output 
            image.ROI = rec;
            Image<Bgr, byte> temp = image.CopyBlank();
            image.CopyTo(temp);
            //image.ROI = Rectangle.Empty;
            //Console.WriteLine("Image width: {0}, Image height: {1}", temp.Width, temp.Height);
            var input = DnnInvoke.BlobFromImage(temp, scale, new Size(416, 416), new MCvScalar(0, 0, 0), swapRB: true, crop: false) ;
            Model.SetInput(input);
            Model.Forward(output, Model.UnconnectedOutLayersNames);

            //Lấy ra các biển số thoả mãn confThreshold
            for (int i = 0; i < output.Size; i++)
            {
                var mat = output[i];
                var data = ArrayTo2DList(mat.GetData());
                for (int j = 0; j < data.Count; j++)
                {
                    var row = data[j];
                    var rowsscores = row.Skip(5).ToArray();
                    var classId = rowsscores.ToList().IndexOf(rowsscores.Max());
                    var confidence = rowsscores[classId];
                    //Kiểm tra ngưỡng
                    if (confidence > confThreshold)
                    {
                        var centerX = (int)(row[0] * temp.Width);
                        var centerY = (int)(row[1] * temp.Height);
                        var boxWidth = (int)(row[2] * temp.Width);
                        var boxHeight = (int)(row[3] * temp.Height);

                        var x = (int)(centerX - (boxWidth / 2));
                        var y = (int)(centerY - (boxHeight / 2));
                        if(x > 0 && y > 0)
                        {
                            Rectangle plate = new Rectangle(x, y, boxWidth, boxHeight);
                            Image<Bgr, byte> roiImage = temp.Clone();
                            roiImage.ROI = plate;
                            PlateImagesList.Add(roiImage);
                            scores.Add(confidence);
                        }                        
                    }

                }
            }
            //Đưa ra kết quả nhận diện biển số
            if (scores.Count > 0)
            {
                var max_indices = scores.IndexOf(scores.Max());
                //OCR bien so co confidence cao nhat
                Image<Bgr, byte> imageResize = ResizeImage(PlateImagesList[max_indices], 250, 0);
                ocrResult = engine.DetectText(imageResize.ToBitmap());
                List<string> arrayresult = new List<string>();
                if (ocrResult.Text != String.Empty && ocrResult.Text.Length <= 12)
                {
                    double accuracy = 1;
                    for (int j = 0; j < ocrResult.TextBlocks.Count; j++)
                    {
                        string TextBlocksPlate = ocrResult.TextBlocks[j].Text;
                        TextBlocksPlate = Regex.Replace(TextBlocksPlate, @"[^A-Z0-9\-]|^-|-$", "");
                        if (checkBlockLP(TextBlocksPlate))
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
                        LPReturn obj = new LPReturn();
                        result = obj.Result(textPlates, true, accuracy, PlateImagesList[0]);
                    }
                    else
                    {
                        LPReturn obj = new LPReturn();
                        result = obj.Result("Null", false, 0, PlateImagesList[0]);
                    }
                }
            }
            else
            {
                LPReturn obj = new LPReturn();
                result = obj.Result("No license plate found", false, 0, image);
            }
            return result;
        }
        public void AutoRecLPVideo()
        {
            //Config Yolov4
            Model = DnnInvoke.ReadNetFromDarknet(PathConfig, PathWeights);
            Model.SetPreferableBackend(Emgu.CV.Dnn.Backend.OpenCV);
            Model.SetPreferableTarget(Target.Cpu);
            capture = new VideoCapture(0, VideoCapture.API.DShow);
            Mat frame = new Mat();
            //var classLabels = File.ReadAllLines(PathClassNames); //Lấy ra các classes YOLO
            //var vc = new VideoCapture(0, VideoCapture.API.DShow);          
            VectorOfMat output = new VectorOfMat();           
            List<float> scores = new List<float>();
            //Ẩn các nút khi chưa vẽ Rec
            btn_clearRec1.Enabled = false;
            btn_confirm1.Enabled = false;
            //string pathSave = Environment.CurrentDirectory + @"\captures";        
            float confThreshold = 0.8f;// Ngưỡng tin cậy
            // Lắng nghe sự kiện bàn phím trực tiếp từ ứng dụng Windows Forms
            this.KeyPreview = true;
            this.KeyPress += (sender, e) =>
            {
                if (e.KeyChar == 'q' || e.KeyChar == (char)Keys.Escape)
                {
                    isStop = false;               
                    Application.Exit();
                }
            };
            timerAutoRec.Elapsed += (sender, e) =>
            {
                if (capture != null) { 
                    capture.Read(frame);            
                    //Đo thời gian chạy nhận diện biển số
                    Stopwatch swObj = new Stopwatch();
                    swObj.Start();
                    ResultLP resultobj = DetectorVideo(frame, confThreshold);
                    //Thời gian kết thúc
                    swObj.Stop();        
                    if (resultobj.accPlate != 0)
                    {
                        ptb_LPImage.Image = resultobj.imagePlate.ToBitmap();
                        tb_LPText.Invoke((MethodInvoker)(() => tb_LPText.Text = resultobj.textPlate));
                        lb_LPTimeRec.Invoke((MethodInvoker)(() => lb_LPTimeRec.Text = Math.Round(swObj.Elapsed.TotalSeconds, 2).ToString() + " giây"));                
                    }  
                } 
            };
            timerAutoRec.Start();
            while (isStop)
            {
                capture.Read(frame);
                ptb_Video.Image = frame.ToBitmap();
                Application.DoEvents();
            }
            timerAutoRec.Stop();
            capture.Dispose();
            CvInvoke.DestroyAllWindows();
        }

        private void chb_autotime_CheckedChanged(object sender, EventArgs e)
        {
            if (chb_autotime.Checked)
            {
                DialogResult res = MessageBox.Show("Bạn chắc chắn muốn bật chức năng này", "Xác nhận thông tin", MessageBoxButtons.OKCancel);
                if (res == DialogResult.Cancel)
                {
                    chb_autotime.Checked = false;
                }
                else
                {
                    timerAutoRec.Interval = (double)numeric_time.Value;
                }
            }
            
        }

        private void bt_clearRec_Click(object sender, EventArgs e)
        {
            btn_drawRegion.Enabled = true;
            btn_clearRec1.Enabled = false;
            startPoint = new System.Drawing.Point(0, 0);
            endPoint = new System.Drawing.Point(0, 0);
        }

        private void btn_drawRegion_Click(object sender, EventArgs e)
        {
            isDrawing = true;
            btn_confirm1.Enabled = true;
            btn_drawRegion.Enabled = false;
        }
        private void ptb_Video_MouseDown(object sender, MouseEventArgs e)
        {
            if(isDrawing)
            {
                isMouseDown = true;
                startPoint = e.Location;
            }    
        }

        private void ptb_Video_MouseUp(object sender, MouseEventArgs e)
        {
            if(isDrawing)
            {
                if (isMouseDown)
                {
                    endPoint = e.Location;
                    isMouseDown = false;
                }
            }    
            
        }

        private void ptb_Video_Paint(object sender, PaintEventArgs e)
        {
            rec.X = Math.Min(startPoint.X, endPoint.X);
            rec.Y = Math.Min(startPoint.Y, endPoint.Y);
            rec.Width = Math.Abs(startPoint.X - endPoint.X);
            rec.Height = Math.Abs(startPoint.Y - endPoint.Y);
            if (rec != null)
            {
                e.Graphics.DrawRectangle(new Pen(Color.Green, 2), rec);
                e.Graphics.DrawString(rec.Width.ToString() + "x" + rec.Height.ToString(), new Font("Arial", 12), Brushes.Red, new PointF(rec.X, rec.Y + 10));
            }                   
        }

        private void ptb_Video_MouseMove(object sender, MouseEventArgs e)
        {
            if(isDrawing)
            {
                if (isMouseDown)
                {
                    endPoint = e.Location;
                }
            }    
        }

        private void btn_confirm_Click(object sender, EventArgs e)
        {
            DialogResult res = MessageBox.Show("Xác nhận vùng định nghĩa là " + rec.Width.ToString() + "x" + rec.Height.ToString(), "Xác nhận thông tin", MessageBoxButtons.OKCancel);
            if (res == DialogResult.OK)
            {
                isDrawing = false;
                btn_confirm1.Enabled = false;
                btn_clearRec1.Enabled = true;
                MessageBox.Show("Lưu ý: Nên vẽ chiều rộng lơn hơn 320 và chiều cao lớn hơn 240 thì mới có tác dụng cho chức năng này !", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }    
        }

        private void btn_defineReg_Click(object sender, EventArgs e)
        {
            isDrawing = true;
            btn_confirm2.Enabled = true;
            btn_defineReg.Enabled = false;
        }

        private void ptb_movementDet_MouseDown(object sender, MouseEventArgs e)
        {
            if (isDrawing)
            {
                isMouseDown = true;
                startPoint = e.Location;
            }
        }

        private void ptb_movementDet_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDrawing)
            {
                if (isMouseDown)
                {
                    endPoint = e.Location;
                    isMouseDown = false;
                }
            }
        }

        private void ptb_movementDet_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrawing)
            {
                if (isMouseDown)
                {
                    endPoint = e.Location;
                }
            }
        }

        private void ptb_movementDet_Paint(object sender, PaintEventArgs e)
        {
            //Console.WriteLine(isMoveMent.ToString());
            rec.X = Math.Min(startPoint.X, endPoint.X);
            rec.Y = Math.Min(startPoint.Y, endPoint.Y);
            rec.Width = Math.Abs(startPoint.X - endPoint.X);
            rec.Height = Math.Abs(startPoint.Y - endPoint.Y);
            if(isMoveMent)
            {
                e.Graphics.DrawRectangle(new Pen(Color.Red, 2), rec);
            }
            else
            {
                e.Graphics.DrawRectangle(new Pen(Color.Green, 2), rec);
                e.Graphics.DrawString(rec.Width.ToString() + "x" + rec.Height.ToString(), new Font("Arial", 12), Brushes.Red, new PointF(rec.X, rec.Y + 10));
            }
   
        }

        private void chb_MotionDetThreshold_CheckedChanged(object sender, EventArgs e)
        {
            if (chb_MotionDetThreshold.Checked)
            {
                DialogResult res = MessageBox.Show("Bạn chắc chắn muốn bật chức năng này", "Xác nhận thông tin", MessageBoxButtons.OKCancel);
                if (res == DialogResult.Cancel)
                {
                    chb_MotionDetThreshold.Checked = false;
                }
                else
                {
                    MotionThreshold = (int)numeric_threshold.Value;
                }
            }
        }

        private void chb_MotionDetCycle_CheckedChanged(object sender, EventArgs e)
        {
            if (chb_MotionDetCycle.Checked)
            {
                DialogResult res = MessageBox.Show("Bạn chắc chắn muốn bật chức năng này", "Xác nhận thông tin", MessageBoxButtons.OKCancel);
                if (res == DialogResult.Cancel)
                {
                    chb_MotionDetCycle.Checked = false;
                }
                else
                {
                    timerAutoMove.Interval = (double)numeric_movement.Value;
                }
            }
        }

        private void btn_confirm2_Click(object sender, EventArgs e)
        {
            DialogResult res = MessageBox.Show("Xác nhận vùng định nghĩa là " + rec.Width.ToString() + "x" + rec.Height.ToString(), "Xác nhận thông tin", MessageBoxButtons.OKCancel);
            if (res == DialogResult.OK)
            {
                isDrawing = false;
                btn_confirm2.Enabled = false;
                btn_clearRec2.Enabled = true; 
            }
        }

        private void btn_clearRec2_Click(object sender, EventArgs e)
        {
            btn_defineReg.Enabled = true;
            btn_clearRec2.Enabled = false;
            startPoint = new System.Drawing.Point(0, 0);
            endPoint = new System.Drawing.Point(0, 0);
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
