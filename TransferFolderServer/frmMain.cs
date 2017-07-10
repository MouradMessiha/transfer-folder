using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace TransferFolderServer
{
    public partial class frmMain : Form
    {
        // variables for receiving the files from other computer
        int receivedBytesLen = 0;
        int intBufferLength;
        int fileNameLen;
        int fileDataLen;
        string fileName;
        string fileCreationTime;
        bool blnProcessed;
        bool mblnDoneOnce = false;
        int mintNumberOfFiles = -1;   // flag that we didn't receive the number of files yet
        int mintFilesReceived = 0;
        byte[] clientData = new byte[1024 * 1024 * 5];
        byte[] bufferData = new byte[1024 * 1024];
        string receivedPath = Path.GetDirectoryName(Application.ExecutablePath) + "\\images\\";
        private bool mblnExiting = false;
        string mstrStatus = "";

        // variables for displaying the images after receiving them
        private Bitmap mobjFormBitmap;
        private Graphics mobjBitmapGraphics;
        private int mintFormWidth;
        private int mintFormHeight;
        private string mstrFileName = "";
        private string[] marrFileNames;
        private Int32 mintFileOrder;

        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Activated(object sender, EventArgs e)
        {
            IPHostEntry IPHost;
            IPAddress MyIP;

            try
            {
                if (!mblnDoneOnce)
                {
                    mblnDoneOnce = true;

                    // get my ipaddress (same as the server ip address)
                    IPHost = Dns.GetHostEntry(Dns.GetHostName());
                    MyIP = IPHost.AddressList[0];
                    lblIP.Text = MyIP.ToString();

                    Socket sock = new Socket(MyIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    sock.Bind(new IPEndPoint(MyIP, 5656));
                    sock.Listen(100);
                    sock.BeginAccept(Accept, sock);
                    while (!mblnExiting & mintFilesReceived != mintNumberOfFiles)
                    {
                        for (int intSleepCounter = 0; intSleepCounter < 600 & !mblnExiting & mintFilesReceived != mintNumberOfFiles; intSleepCounter++)
                        {
                            Thread.Sleep(100);
                            lblStatus.Text = mstrStatus;
                            Application.DoEvents();
                        }
                    }

                    if (mintFilesReceived == mintNumberOfFiles)   // finished receiving all files
                    {
                        lblStatus.Visible = false;
                        this.ContextMenuStrip = mnuPopup;
                        this.Left = (Screen.PrimaryScreen.Bounds.Width * 5) / 100;
                        this.Top = (Screen.PrimaryScreen.Bounds.Height * 5) / 100;
                        this.Width = (Screen.PrimaryScreen.Bounds.Width * 90) / 100;
                        this.Height = (Screen.PrimaryScreen.Bounds.Height * 90) / 100;
                        this.WindowState = FormWindowState.Minimized;
                        this.WindowState = FormWindowState.Normal;
                        Application.DoEvents();
                        mintFormWidth = this.Width;
                        mintFormHeight = this.Height;
                        mobjFormBitmap = new Bitmap(mintFormWidth, mintFormHeight, this.CreateGraphics());
                        mobjBitmapGraphics = Graphics.FromImage(mobjFormBitmap);

                        marrFileNames = Directory.GetFiles(Path.GetDirectoryName(Application.ExecutablePath) + "\\images", "*.jpg", SearchOption.TopDirectoryOnly);
                        if (marrFileNames.Length > 0)
                        {
                            mintFileOrder = 0;
                            mstrFileName = marrFileNames[mintFileOrder];
                        }
                        RefreshDisplay();
                    }
                }
            }
            catch (Exception Ex)
            {
                MessageBox.Show(Ex.Message, "Receive");
            }
        }

        public void Accept(IAsyncResult result)
        {
            Socket clientSock = ((Socket)result.AsyncState).EndAccept(result);
            clientSock.BeginReceive(bufferData, 0, bufferData.Length, SocketFlags.None, Receive, clientSock);
            mstrStatus = "Receiving";
        }

        public void Receive(IAsyncResult result)
        {
            Socket clientSock = (Socket)result.AsyncState;
            intBufferLength = clientSock.EndReceive(result);
            bufferData.CopyTo(clientData, receivedBytesLen);
            receivedBytesLen += intBufferLength;

            if (mintNumberOfFiles == -1)  // we didn't receive the number of files yet
            {
                mintNumberOfFiles = BitConverter.ToInt32(clientData, 0);
                int ShiftLen = 4;
                int newLen = receivedBytesLen - 4;
                for (int intBytCounter = 0; intBytCounter < newLen; intBytCounter++)
                    clientData[intBytCounter] = clientData[intBytCounter + ShiftLen];
                receivedBytesLen = newLen;
            }
            
            // process all full files within the current data received
            blnProcessed = true;
            while (blnProcessed)
            {
                blnProcessed = false;
                if (receivedBytesLen > 30)       // enough bytes to read the file size, creation time and file name size
                {
                    fileNameLen = BitConverter.ToInt32(clientData, 0);
                    fileDataLen = BitConverter.ToInt32(clientData, 4);
                    fileCreationTime = Encoding.ASCII.GetString(clientData, 8, 22);
                    if (receivedBytesLen >= 30 + fileNameLen + fileDataLen)      // enough bytes to read a full file
                    {
                        fileName = Encoding.ASCII.GetString(clientData, 30, fileNameLen);
                        BinaryWriter bWrite = new BinaryWriter(File.Open(receivedPath + fileName, FileMode.Create));
                        bWrite.Write(clientData, 30 + fileNameLen, fileDataLen);
                        bWrite.Flush();
                        bWrite.Close();
                        File.SetCreationTime(receivedPath + fileName, Convert.ToDateTime(fileCreationTime));
                        mintFilesReceived++;

                        int ShiftLen = 30 + fileNameLen + fileDataLen;
                        int newLen = receivedBytesLen - 30 - fileNameLen - fileDataLen;
                        for (int intBytCounter = 0; intBytCounter < newLen; intBytCounter++)
                            clientData[intBytCounter] = clientData[intBytCounter + ShiftLen];

                        receivedBytesLen = newLen;
                        blnProcessed = true;
                    }
                }
            }

            if (mintFilesReceived != mintNumberOfFiles)
                clientSock.BeginReceive(bufferData, 0, bufferData.Length, SocketFlags.None, Receive, clientSock);
            else
                mstrStatus = "Finished receiving " + mintFilesReceived + " files";
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            mblnExiting = true;
        }

        private void frmMain_Resize(object sender, EventArgs e)
        {
            if (this.WindowState != FormWindowState.Minimized)
            {
                mintFormWidth = this.Width;
                mintFormHeight = this.Height;
                mobjFormBitmap = new Bitmap(mintFormWidth, mintFormHeight, this.CreateGraphics());
                mobjBitmapGraphics = Graphics.FromImage(mobjFormBitmap);
                RefreshDisplay();
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            //Do nothing (after initial file receiving phase is finished)
            if (mobjFormBitmap == null)
                base.OnPaintBackground(e);
        }

        private void frmMain_Paint(object sender, PaintEventArgs e)
        {
            if (mobjFormBitmap != null)
                e.Graphics.DrawImage(mobjFormBitmap, 0, 0);
        }

        private void RefreshDisplay()
        {
            Font objFont;
            int intX;
            int intY;
            Image objImage;
            string strTimeStamp;
            FileInfo objFileInfo;

            mobjBitmapGraphics.FillRectangle(Brushes.White, 0, 0, mintFormWidth, mintFormHeight);

            if (mstrFileName != "") // if there are any image files in the folder
            {
                objImage = Image.FromFile(mstrFileName);
                if (mnuRotate.Checked)
                    objImage.RotateFlip(RotateFlipType.Rotate90FlipNone);
                intX = (mintFormWidth - objImage.Width) / 2;
                intY = (mintFormHeight - objImage.Height) / 2;
                mobjBitmapGraphics.DrawImage(objImage, intX, intY);
                objFileInfo = new FileInfo(mstrFileName);
                strTimeStamp = string.Format("{0:ddd MMM dd, yyyy h:mm tt}", objFileInfo.CreationTime);
                objFont = new Font("MS Sans Serif", 14, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
                mobjBitmapGraphics.DrawString(strTimeStamp, objFont, Brushes.Black, (mintFormWidth - 250) / 2, 10);
                mobjBitmapGraphics.DrawString((mintFileOrder + 1).ToString() + " of " + marrFileNames.Length.ToString(), objFont, Brushes.Black, (mintFormWidth - 60) / 2, 30);
            }

            this.Invalidate();
        }

        private void mnuRotate_CheckedChanged(object sender, EventArgs e)
        {
            RefreshDisplay();
        }

        private void frmMain_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Right:
                    if (mintFileOrder < marrFileNames.Length - 1)
                        mintFileOrder++;
                    break;

                case Keys.Left:
                    if (mintFileOrder > 0)
                        mintFileOrder--;
                    break;

                case Keys.Home:
                    mintFileOrder = 0;
                    break;

                case Keys.End:
                    mintFileOrder = marrFileNames.Length - 1;
                    break;
            }

            mstrFileName = marrFileNames[mintFileOrder];
            RefreshDisplay();
        }

     }
}