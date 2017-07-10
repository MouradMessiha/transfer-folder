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

namespace TransferFolderClient
{
    public partial class frmMain : Form
    {
        private string[] marrFileNames;
        ManualResetEvent allDone = new ManualResetEvent(false);
        private bool mblnDoneOnce = false;
        private bool mblnConnected = false;
        private bool mblnExiting = false;
        private string mstrStatus = "";

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
                    MyIP = IPHost.AddressList[1];
                    lblIP.Text = MyIP.ToString();
                    
                    while (!mblnExiting)
                    {
                        if (!mblnConnected)
                        {
                            Socket clientSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                            clientSock.BeginConnect(new IPEndPoint(MyIP, 5656), Connect, clientSock);
                        }

                        for (int intSleepCounter = 0; intSleepCounter < 600 & !mblnExiting; intSleepCounter++)
                        {
                            Thread.Sleep(100);
                            lblStatus.Text = mstrStatus;
                            Application.DoEvents();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Begin Connect");
            }
        }    

        public void Connect(IAsyncResult result) 
        {
            byte[] numberofFilesByte;

            try
            {
                Socket clientSock = (Socket)result.AsyncState;
                clientSock.EndConnect(result);
                mblnConnected = true;
                mstrStatus = "Connected";
                string filePath = Path.GetDirectoryName(Application.ExecutablePath) + "\\images\\";
                marrFileNames = Directory.GetFiles(filePath, "*.jpg", SearchOption.TopDirectoryOnly);
                allDone.Reset();

                // send the number of files
                numberofFilesByte = BitConverter.GetBytes(marrFileNames.Length);
                clientSock.BeginSend(numberofFilesByte, 0, numberofFilesByte.Length, SocketFlags.None, Send, clientSock);
                allDone.WaitOne(); //halts this thread 

                // send the files
                foreach (string fullfileName in marrFileNames)
                {
                    string fileName = Path.GetFileName(fullfileName);
                    byte[] fileNameByte = Encoding.ASCII.GetBytes(fileName);
                    string strCreationDate = string.Format("{0:yyyy-MM-dd hh:mm:ss tt}", File.GetCreationTime(fullfileName));
                    byte[] fileCreationDateByte = Encoding.ASCII.GetBytes(strCreationDate);
                    byte[] fileData = File.ReadAllBytes(filePath + fileName);
                    byte[] clientData = new byte[30 + fileNameByte.Length + fileData.Length];
                    byte[] fileNameLen = BitConverter.GetBytes(fileNameByte.Length);
                    byte[] fileDataLen = BitConverter.GetBytes(fileData.Length);

                    fileNameLen.CopyTo(clientData, 0);
                    fileDataLen.CopyTo(clientData, 4);
                    fileCreationDateByte.CopyTo(clientData, 8);
                    fileNameByte.CopyTo(clientData, 30);
                    fileData.CopyTo(clientData, 30 + fileNameByte.Length);
                    clientSock.BeginSend(clientData, 0, clientData.Length, SocketFlags.None, Send, clientSock);
                    allDone.WaitOne(); //halts this thread 
                }
                mblnConnected = false;
                mstrStatus = "Finished transmission of " + marrFileNames.Length.ToString() + 
                    " files at " + string.Format("{0:yyyy-MM-dd hh:mm:ss tt}", DateTime.Now);
            }
            catch 
            {
                // could not find server, ignore error, will try again in 1 min
            }
        }
        public void Send(IAsyncResult result) 
        {
            Socket clientSock = (Socket)result.AsyncState;
            int size = clientSock.EndSend(result); 
            allDone.Set(); 
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            mblnExiting = true;
        }
    }
}
