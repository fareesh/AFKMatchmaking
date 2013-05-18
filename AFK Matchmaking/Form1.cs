using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using SocketIOClient;
using System.Collections.Specialized;
using Microsoft.Win32;      


namespace AFK_Matchmaking
{
    public partial class Form1 : Form
    {

        private String serverURL = "http://afk.fareesh.com:8080";
        private String messageURL = "http://afk.fareesh.com:4567/message/send";
        private Client socket;
        private String connectCode;
        private bool notification_sent;
        private IntPtr dotaWindow;

        /*Coordinates at 1920*/
        private const int BUTTON_X = 750;
        private const int BUTTON_Y = 517;


        private Point getButtonCoordinates()
        {
            Rectangle resolution = Screen.PrimaryScreen.Bounds;
            return new Point(BUTTON_X * resolution.Width/1920, BUTTON_Y * resolution.Height / 1080);
        }
                
        private void getConnectCode()
        {
            RegistryKey key =  Registry.CurrentUser.OpenSubKey("Software\\AFK Matchmaking\\ConnectCode");
            if (key == null)
            {
                key = Registry.CurrentUser.CreateSubKey("Software\\AFK Matchmaking\\ConnectCode");
                connectCode = Guid.NewGuid().ToString("N").Substring(0, 6);
                key.SetValue("ConnectCode",connectCode);
                key.Close();
            }
            else
            {
                connectCode = key.GetValue("ConnectCode").ToString();
            }
            
        }

        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point lpPoint);

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);
        private const int HSHELL_WINDOWCREATED = 1;
        public delegate bool WindowEnumCallback(int hwnd, int lparam);
        private int uMsgNotify;
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumWindows(WindowEnumCallback lpEnumFunc, int lParam);

        [DllImport("user32.dll")]
        public static extern void GetWindowText(IntPtr h, StringBuilder s, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(int h);
        [DllImport("user32.dll")]
        private static extern int RegisterWindowMessage(string lpString);
        [DllImport("user32.dll")]
        private static extern int RegisterShellHookWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern void FlashWindow(IntPtr a, bool b);
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        public Form1()
        {
            InitializeComponent();
            if (hasUpdate())
            {
                MessageBox.Show("An update is available. Please download the latest version of this application.");
            }
            getConnectCode();
            lblCode.Text = "Your Code is: " + connectCode;
            notification_sent = false;
            setupWindowListener();
            setupSocket();
        }

        private void ClickOnPoint(IntPtr wndHandle, Point clientPoint)
        {
            Point oldPoint;
            GetCursorPos(out oldPoint);

            /// get screen coordinates
            ClientToScreen(wndHandle, ref clientPoint);

            /// set cursor on coords, and press mouse
            SetCursorPos(clientPoint.X, clientPoint.Y);
            mouse_event(0x00000002, 0, 0, 0, UIntPtr.Zero); /// left mouse button down
            mouse_event(0x00000004, 0, 0, 0, UIntPtr.Zero); /// left mouse button up

            /// return mouse 
            SetCursorPos(oldPoint.X, oldPoint.Y);
        }

        private void setupSocket()
        {
            socket = new Client(serverURL); // url to nodejs 
            socket.Opened += SocketOpened;
            socket.Message += SocketMessage;
            socket.SocketConnectionClosed += SocketConnectionClosed;
            socket.Error += SocketError;
         
            socket.On("connect", (fn) =>
            {
                Console.WriteLine("Connected to Server");
                socket.Emit("DeviceKey", connectCode);
            });

            socket.On(connectCode, (data) =>
            {
                Console.WriteLine("Firing clickAccept for clicking the button!");
                clickAccept();
            });

            socket.Connect();
        }

        private void clickAccept()
        {
            Console.WriteLine("Setting Dotawindow " + dotaWindow.ToString() + " to foreground");
            SetForegroundWindow(dotaWindow);
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(3000);
                ClickOnPoint(dotaWindow, getButtonCoordinates()); ClickOnPoint(dotaWindow, getButtonCoordinates());
                try
                {
                    if (socket != null)
                    {
                        socket.Close();
                    }
                }
                catch (Exception e)
                {

                }
                notification_sent = false;
            });
        }

        private void SocketOpened(Object sender, EventArgs e)
        {

        
        }

        private void SocketMessage(Object sender, EventArgs e)
        {

        }

        private void SocketConnectionClosed(Object sender, EventArgs e)
        {

        }

        private void SocketError(Object sender, EventArgs e)
        {

        }



        private void sendMessage()
        {
            using (System.Net.WebClient client = new System.Net.WebClient())
            {

                byte[] response = client.UploadValues(messageURL, new NameValueCollection(){
                    {"connectCode", connectCode}
                });
            }

        }

        

        private void matchmakingPop()
        {
            Console.WriteLine("Matchmaking Queue Ready");
            if (!notification_sent)
            {
                notification_sent = true;
                setupSocket();
                sendMessage();
            }
        }





        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            if (m.Msg == uMsgNotify)
            {
                IntPtr handle = m.LParam;
                StringBuilder sb = new StringBuilder(255);
                switch (m.WParam.ToInt32())
                {
                    case HSHELL_WINDOWCREATED:
                        break;

                    default:
                        GetWindowText(handle, sb, sb.Capacity);
                        if (sb.ToString().IndexOf("DOTA 2") >= 0 && m.WParam.ToInt32() == 6)
                        {
                            dotaWindow = handle;
                            Task.Factory.StartNew(() =>
                            {
                                Thread.Sleep(3000);
                                this.Invoke(new Action(() => matchmakingPop()));
                            });
                        }
                        break;
                }
            }
            base.WndProc(ref m);
        }





        private void setupWindowListener()
        {
            uMsgNotify = RegisterWindowMessage("SHELLHOOK");
            RegisterShellHookWindow(this.Handle);
        }




        private bool hasUpdate(){
            return false;
        }
    }
}
