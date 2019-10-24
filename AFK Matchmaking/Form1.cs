using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using WebSocketSharp;
using Microsoft.Win32;      


namespace AFK_Matchmaking
{
    public partial class Form1 : Form
    {

        private String serverURL = "ws://afkaccept.fareesh.com:7474";
        private WebSocket socket;
        private String connectCode;
        private bool notification_sent;
        private IntPtr dotaWindow;

        /*Coordinates at 1920*/
        private const int BUTTON_X = 951;
        private const int BUTTON_Y = 527;


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
            System.Diagnostics.Debug.WriteLine("Listener Setup Complete");
            setupSocket();
        }

        private void setActiveLabel(Object sender, EventArgs e)
        {
            lblNotifications.Text = "NOTIFICATIONS ENABLED: AFTER QUEUEING, ALT-TAB BACK TO THIS WINDOW AND KEEP IT IN FOCUS TO ENABLE NOTIFICATIONS";
        }

        private void setInactiveLabel(Object sender, EventArgs e)
        {
            lblNotifications.Text = "NOTIFICATIONS DISABLED: ALT-TAB BACK TO THIS WINDOW TO RECEIVE NOTIFICATIONS";
        }

        private void ClickOnPoint(IntPtr wndHandle, Point clientPoint)
        {
            Point oldPoint;
            GetCursorPos(out oldPoint);
            ClientToScreen(wndHandle, ref clientPoint);
            SetCursorPos(clientPoint.X, clientPoint.Y);
            mouse_event(0x00000002, 0, 0, 0, UIntPtr.Zero); /// left mouse button down
            mouse_event(0x00000004, 0, 0, 0, UIntPtr.Zero); /// left mouse button up
            SetCursorPos(oldPoint.X, oldPoint.Y);
        }

        private void setupSocket()
        {
            System.Diagnostics.Debug.WriteLine("Connecting..");
            socket = new WebSocket(serverURL);
            socket.OnMessage += (sender, e) =>
            {
                System.Diagnostics.Debug.WriteLine("Message Received: " + e.Data);
                phoneLabel.Text = "Accepting Match...";
                if(e.Data == "ACCEPT")
                {
                    clickAccept();
                }
            };
            
            socket.OnOpen += (sender, e) =>
            {
                statusLabel.Text = "Status: Connected";
                System.Diagnostics.Debug.WriteLine("Socket Opened");
                socket.Send("{\"deviceKey\": \"" + connectCode + "\", \"messageType\": \"CONNECT\"}");
            };

            socket.OnClose += (sender, e) =>
            {
                statusLabel.Text = "Status: Not Connected";
                setupSocket();
            };

            socket.Connect();
            System.Diagnostics.Debug.WriteLine("Connect fired");
        }

        private void clickAccept()
        {
            System.Diagnostics.Debug.WriteLine("Setting Dotawindow " + dotaWindow.ToString() + " to foreground");
            SetForegroundWindow(dotaWindow);
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(3000);
                ClickOnPoint(dotaWindow, getButtonCoordinates()); ClickOnPoint(dotaWindow, getButtonCoordinates());
                phoneLabel.Text = "If you entered the code correctly, your phone will receive a notification when the match is ready";
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
            statusLabel.Text = "Status: Connection Closed";
        }

        private void SocketError(Object sender, EventArgs e)
        {
            statusLabel.Text = "Status: Connection Error";
        }



        private void sendMessage()
        {
            socket.Send("{\"deviceKey\": \"" + connectCode + "\", \"messageType\": \"QUEUE\"}");
        }

        

        private void matchmakingPop()
        {
            System.Diagnostics.Debug.WriteLine("Matchmaking Queue Ready");
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
                        if (sb.ToString().ToUpper().IndexOf("DOTA 2") >= 0)
                            //System.Diagnostics.Debug.WriteLine("DOTA Window reports: " + m.WParam.ToInt32());
                            if (m.WParam.ToInt32() == 32774)
                            {
                                {
                                    dotaWindow = handle;
                                    Task.Factory.StartNew(() =>
                                    {
                                        Thread.Sleep(3000);
                                        this.Invoke(new Action(() => matchmakingPop()));
                                    });
                                }
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
