using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        Bitmap rotatedBmp = null;
        PointF pt = new PointF(0, 0);

        Random rnd = new Random();
        double dAltitude = 0.0;
        DateTime dtTime;

        String sSerialData = "";
        float fHorizonAngleX = 0.0f;
        float fHorizonAngleY = 0.0f;

        // Model Data
        int iRandomNumber = 0;
        Random rndGenerator = new Random();

        double dRadians = 0.0;
        double dLat = 0.0;
        double dLong = 0.0;

        int angle = 0;
        float pitch = 0;

        // Create pen.
        System.Drawing.Pen myPen = new System.Drawing.Pen(System.Drawing.Color.Black);


        // Create points that define line.
        Point point1 = new Point(0, 0);
        Point point2 = new Point(0, 0);

        public float FHorizonAngleY { get => fHorizonAngleY; set => fHorizonAngleY = value; }

        public Form1()
        {
            InitializeComponent();
            pictureBox2.Controls.Add(pictureBox3);
            pictureBox3.Location = new Point(0, 0);
            //pictureBox3.BackColor = Color.Transparent;
            point1.X = pictureBox1.Width / 2;
            point1.Y = pictureBox1.Height / 2;
            point2 = point1;

            // Draw line to screen. 
            point1.X = point1.X - 10;
            point2.X = point2.X + 10;

        }

        private void btnRight_Click(object sender, EventArgs e)
        {
            angle = angle + 5;

            // Update Screen:
            pictureBox2.Image = RotateImage(pictureBox1.Image, angle);
        }

        private void btnLeft_Click(object sender, EventArgs e)
        {
            angle = angle - 5;

            pictureBox2.Image = RotateImage(pictureBox1.Image, angle);

        }

        public Bitmap RotateImage(Image image, PointF offset, float angle, float pitch = 0)
        {
            if (image == null)
                throw new ArgumentNullException("image");

            //create a new empty bitmap to hold rotated image
            if (rotatedBmp == null)
                rotatedBmp = new Bitmap(image.Width, image.Height);

            rotatedBmp.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //make a graphics object from the empty bitmap
            Graphics g = Graphics.FromImage(rotatedBmp);



            //Put the rotation point in the center of the image
            g.TranslateTransform(offset.X, offset.Y);

            //rotate the image
            g.RotateTransform(angle);

            g.TranslateTransform(-offset.X, -offset.Y);
            g.TranslateTransform(0, pitch);



            //draw passed in image onto graphics object
            g.DrawImage(image, pt);




            return rotatedBmp;
        }

        public Bitmap RotateImage(Image image, float angle, float pitch = 0)
        {
            return RotateImage(image, new PointF((float)image.Width / 2, (float)image.Height / 2), angle, pitch);
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            // 1 - Set Port Name
            serialPort.PortName = "COM16";

            // 2 - Open Port For Communication
            serialPort.Open();

        }

        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            sSerialData = serialPort.ReadLine();
            sSerialData = serialPort.ReadLine();

            if (sSerialData.Contains("GyroX="))
            {
                sSerialData = sSerialData.Replace("GyroX=", "");
                float.TryParse(sSerialData, out fHorizonAngleX);
            }
            else if (sSerialData.Contains("GyroY="))
            {
                sSerialData = sSerialData.Replace("GyroY=", "");
                float.TryParse(sSerialData, out fHorizonAngleY);
            }
        }
        private void screenUpdate_Tick(object sender, EventArgs e)
        {
            if (fHorizonAngleX > 0.0 || fHorizonAngleX < 0.0)
                angle = -(int)fHorizonAngleX * 10;

            if (fHorizonAngleY > 0.0 || fHorizonAngleY < 0.0)
                pitch = (int)fHorizonAngleY * 50;
        }

        private void pictureBox3_Paint(object sender, PaintEventArgs e)
        {

            e.Graphics.DrawLine(myPen, point1, point2);
        }
        private void chartUpdate_Tick(object sender, EventArgs e)
        {
            dAltitude = rnd.Next(0, 100);
            dtTime = DateTime.Now;
            chart1.Series[0].Points.AddXY(dtTime, dAltitude);
        }
        private void timerUpdate_Tick(object sender, EventArgs e)
        {
            iRandomNumber = rndGenerator.Next(-180, 180);

            dRadians += 0.01;
            dLat = 180*Math.Sin(dRadians);
            dLong = 180 * Math.Sin(-dRadians);

            tbLatitude.Text = dLat.ToString();
            tbLongitude.Text = dLong.ToString();
            progressBar2.Increment(10);
            progressBar1.Increment(-10);
            LoadUSBPorts();
            textBox1.Text = textBox1.Text + sSerialData + "\n";
        }

        private void button2_MouseEnter(object sender, EventArgs e)
        {
            Btndown.BackColor = Color.Red;
        }

        private void button2_MouseLeave(object sender, EventArgs e)
        {
            Btndown.BackColor = Color.Green;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            LoadUSBPorts();
        }
        private void LoadUSBPorts()
        {
            usbPortList.Items.Clear();

            // Get a list of serial port names.
            string[] ports = System.IO.Ports.SerialPort.GetPortNames();

            foreach (string port in ports)
            {
                usbPortList.Items.Add(port);
            }
        }

       

        private void Btnup_Click(object sender, EventArgs e)
        {
            pitch = pitch - 5;
            pictureBox2.Image = RotateImage(pictureBox1.Image, angle, pitch);
        }

        private void Btndown_Click(object sender, EventArgs e)
        {
            pitch = pitch + 5;
            pictureBox2.Image = RotateImage(pictureBox1.Image, angle, pitch);
        }
    }
}
