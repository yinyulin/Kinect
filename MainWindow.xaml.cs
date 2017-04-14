using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Kinect;

namespace zuobiaoduiying
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution640x480Fps30;
        private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;
        private KinectSensor sensor;
        private DepthImagePixel[] depthPixels;
        private byte[] colorPixels;
        private int[] playerPixelData;
        private ColorImagePoint[] colorCoordinates;
        private int colorToDepthDivisor;
        private int depthWidth;
        private int depthHeight;
        private int opaquePixelValue = -1;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                this.sensor.DepthStream.Enable(DepthFormat);
                this.depthWidth = this.sensor.DepthStream.FrameWidth;
                this.depthHeight = this.sensor.DepthStream.FrameHeight;
                this.sensor.ColorStream.Enable(ColorFormat);
                int colorWidth = this.sensor.ColorStream.FrameWidth;
                int colorHeight = this.sensor.ColorStream.FrameHeight;
                this.colorToDepthDivisor = colorWidth / this.depthWidth;
                this.sensor.SkeletonStream.Enable();
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];
                this.playerPixelData = new int[this.sensor.DepthStream.FramePixelDataLength];
                this.colorCoordinates = new ColorImagePoint[this.sensor.DepthStream.FramePixelDataLength];
                this.sensor.AllFramesReady += this.SensorAllFramesReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
                this.sensor = null;
            }
        }
        short[] depthPixelData = new short[640 * 480];
        private void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            // in the middle of shutting down, so nothing to do
            if (null == this.sensor)
            {
                return;
            }

            bool depthReceived = false;
            bool colorReceived = false;

            

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (null != depthFrame)
                {
                    // Copy the pixel data from the image to a temporary array

                    depthFrame.CopyPixelDataTo(depthPixelData);

                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);

                    depthReceived = true;
                }
            }

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (null != colorFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    colorReceived = true;
                }
            }

            if (true == depthReceived)
            {
                this.sensor.CoordinateMapper.MapDepthFrameToColorFrame(
                    DepthFormat,
                    this.depthPixels,
                    //depthPixelData,
                    ColorFormat,
                    this.colorCoordinates); 
                   

                Array.Clear(this.playerPixelData, 0, this.playerPixelData.Length);

                // loop over each row and column of the depth
                for (int y = 0; y < this.depthHeight; ++y)
                {
                    for (int x = 0; x < this.depthWidth; ++x)
                    {
                        // calculate index into depth array
                        int depthIndex = x + (y * this.depthWidth);

                        DepthImagePixel depthPixel = this.depthPixels[depthIndex];
                        //获取人物标记
                        int player = depthPixel.PlayerIndex;

                        // if we're tracking a player for the current pixel, sets it opacity to full跟踪一个像素设置为不透明的
                        if (player > 0)
                        {
                            // retrieve the depth to color mapping for the current depth pixel
                            ColorImagePoint colorImagePoint = this.colorCoordinates[depthIndex];

                            // scale color coordinates to depth resolution
                            int colorInDepthX = colorImagePoint.X / this.colorToDepthDivisor;
                            int colorInDepthY = colorImagePoint.Y / this.colorToDepthDivisor;

                            if (colorInDepthX > 0 && colorInDepthX < this.depthWidth && colorInDepthY >= 0 && colorInDepthY < this.depthHeight)
                            {
                                // calculate index into the player mask pixel array
                                int playerPixelIndex = colorInDepthX + (colorInDepthY * this.depthWidth);
                                // set opaque
                                this.playerPixelData[playerPixelIndex] = opaquePixelValue;
                                // compensate for depth/color not corresponding exactly by setting the pixel 
                                // to the left to opaque as well
                                this.playerPixelData[playerPixelIndex - 1] = opaquePixelValue;
                            }
                        }
                    }
                }
                //深度图得到
                float center = depthPixelData[76800];  //定义一个0点，就是图像的中心点
                for (int y = 0; y < this.depthHeight; ++y)
                {
                    for (int x = 0; x < this.depthWidth; ++x)
                    {
                        int depthIndex = x + (y * this.depthWidth);
                        if (this.playerPixelData[depthIndex] == opaquePixelValue)
                        {
                            float pixel = depthPixelData[depthIndex] >> DepthImageFrame.PlayerIndexBitmaskWidth;  //右移动3
                            float xiangshu = (3.1395f * pixel) / 1500;  //计算每个像素的实际距离

                            float x1 = ((x - 320) * xiangshu) / 1000;
                            float y1 = ((y - 240) * xiangshu) / 1000;
                            float z1 = (pixel - center) / 1000;
                            FileStream fs = new FileStream("F:\\zuobiao.ply", FileMode.Append, FileAccess.Write);
                            StreamWriter sr = new StreamWriter(fs);
                            sr.WriteLine(x1 + " " + y1 + " " + z1);
                            sr.Close();
                            fs.Close();
                        }
                    }
                }

            }
        }
    }
}
