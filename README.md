# Vietnamese License Plate Recognition(Demo version)
Hiện tại chương trình vẫn chưa hoàn thiện, thuật toán nhận dạng chưa hoàn hảo 100% chính xác với mọi loại biển số xe. Nên thời gian tới mình sẽ phát triển thêm và cập nhật sau. Do chương trình dài nên mình chỉ giới thiệu tính năng các bạn có thể clone về rồi từ từ tham khảo. Các bước thực hiện của chương trình như sau: 

**1. Hình gốc**

![img1.jpg](https://github.com/sangnv3007/VLPR/blob/master/test1.jpg)

**2. Nhận dạng ví trí biển số xe**

![img2.jpg](https://github.com/sangnv3007/VLPR/blob/master/imgtest.jpg)

**3. Nhận dạng chữ/ số trên biển số xe trả về kết quả**

![img3.jpg](https://github.com/sangnv3007/VLPR/blob/master/Screenshot%202022-08-05%20100934.png)
### Cài đặt các nuget package cần thiết trên VS Studio 2019(. NET 4.7.1)
* Emgu.CV 4.5.5.4823
* Emgu.CV.runtime.windows 4.5.5.4823
* Python.Runtime.Windows 3.7.2
* DynamicLanguageRuntime 1.3.1
### Tools and Libraries(Python)
* Python(3.7)
* paddlepaddle
    * pip install paddlepaddle
* paddleocr
    * pip install paddleocr
### Tài nguyên cần thiết(yolov3.cf,yolov3_6000_LP.weights, reconigtion_character.py)
* Dowload 3 file trong thư mục "backup" https://drive.google.com/drive/folders/1AOwlf1i7SldTr-y3ce4W8KRjirV7duY1?usp=sharing
* Lưu tại thư mục ..\Vietnamese-License-Plate-Recognition\Vietnamese License Plate Recognition\bin\x64\Debug

Lưu ý: Hiện tại vì là bản demo nên độ chính xác chưa cao nên chương trình còn một số hạn chế sau:
- Nhận dạng đúng cho những biển số rõ ràng, dể nhìn
- Chưa nhận diện tốt những biển quá mờ, điều kiện ánh sáng quá chói hoặc biển bị che.
