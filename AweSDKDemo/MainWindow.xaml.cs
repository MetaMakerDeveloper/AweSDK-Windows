using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

using AweSDK;
using AweSDK.Core;
using AweSDK.Core.Values;
using AweSDK.Scene;
using AweSDK.Authorization;
using AweSDK.DataBridge;
using AweSDK.DataBridge.Sockets;

namespace AweSDKDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hwnd, int x, int y, int cx, int cy, bool repaint);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern void SetWindowLongPtr(IntPtr hwnd, int nIndex, long dwNewLong);

        public const int GWL_STYLE = -16;
        public const long WS_VISIBLE = 0x10000000L;

        private Process? mRendererProcess = null;
        private IntPtr mRendererHandle = IntPtr.Zero;
        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += MainWindow_SourceInitialized;
            SizeChanged += MainWindow_SizeChanged;
            Closed += MainWindow_Closed;
        }
        private void MainWindow_SourceInitialized(object? sender, System.EventArgs e)
        {
            Context context = InitializeAweSDK();
            LoadHuman(context);
        }

        private Context InitializeAweSDK()
        {
            string rendererPath = @"E:\Workspace\Demo\WPF\AweRenderer";
            Context context = Setup(rendererPath);
            return context;
        }

        private Context Setup(string rendererPath)
        {
            Context context = new Context();
            AweSDK.Environment.Setup(context);
            int port = SocketClient.GetAvailablePort(10000);
            StartRenderer(context, rendererPath, port);
            SetupDataBridge(context, port);
            SetupLicense(context);
            SetupResources(context);
            return context;
        }

        private void StartRenderer(Context context, string path, int port)
        {
            path = Path.Combine(path, "AweRenderer.exe");
            mRendererProcess = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo(path);
            startInfo.Arguments = $"--port {port}";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            mRendererProcess.StartInfo = startInfo;
            mRendererProcess.Start();

            while (mRendererProcess.MainWindowHandle == IntPtr.Zero)
            {
                Thread.Sleep(1);
                mRendererProcess.Refresh();
            }

            IntPtr parentHandler = ((HwndSource)PresentationSource.FromVisual(MainGrid)).Handle;
            mRendererHandle = mRendererProcess.MainWindowHandle;
            SetWindowLongPtr(mRendererHandle, GWL_STYLE, WS_VISIBLE);
            SetParent(mRendererHandle, parentHandler);
            
            ResizeRenderer();
        }

        private void ResizeRenderer()
        {
            var actualWidth = MainGrid.ActualWidth;
            var actualHeight = MainGrid.ActualHeight;
            TransformToPixels(this, actualWidth, actualHeight, out int width, out int height);
            MoveWindow(mRendererHandle, 0, 0, width, height, true);
        }

        private void TransformToPixels(
            Visual visual,
            double unitX,
            double unitY,
            out int pixelX,
            out int pixelY)
        {
            Matrix matrix;
            var source = PresentationSource.FromVisual(visual);
            if (source != null)
            {
                matrix = source.CompositionTarget.TransformToDevice;
            }
            else
            {
                using (var src = new HwndSource(new HwndSourceParameters()))
                {
                    matrix = src.CompositionTarget.TransformToDevice;
                }
            }

            pixelX = (int)(matrix.M11 * unitX);
            pixelY = (int)(matrix.M22 * unitY);
        }

        private void SetupDataBridge(Context context, int port)
        {
            var exchanger = DataExchanger.GetInstance(context);
            var client = new SocketClient(context, "127.0.0.1", port);
            client.Connect();
            exchanger.SetDataBridge(client);
        }

        private void SetupLicense(Context context)
        {
            LicenseManager licenseManager = LicenseManager.GetInstance(context);
            licenseManager.AppKey = "bacc2c8f874936e5776ac479096a9157f41fb5fc";
            licenseManager.AppSecret = "9dc9a9851aa3a8a329999236c194211ffa220e5e";
        }

        private void SetupResources(Context context)
        {
            ResourceManager resourceManager = ResourceManager.GetInstance(context);
            resourceManager.SetCacheDirectory("E:/temp/cache/");
            resourceManager.AddResourceDirectory("E:/temp/media/");
        }

        private void LoadHuman(Context context)
        {
            var gender = Human.Gender.Female;
            var faceTarget = "xiaojing/face.target";
            var faceMapping = "xiaojing/face.jpg";
            var baseInfo = new Human.BaseInfo(gender, faceTarget, faceMapping);
            Human human = new Human(context, baseInfo);

            human.SetTarget("20003", 1f);
            human.SetTarget("23002", 0.5f);
            human.SetTarget("20101", 0.4769f);
            human.SetTarget("20102", -0.3075f);
            human.SetTarget("20502", -0.3522f);
            human.SetTarget("23202", 0.4769f);
            human.SetTarget("23503", -0.8489f);

            human.WearHair("cloth/nv_tf_128");
            human.WearOutfits("cloth/nv_up_06", "cloth/nv_tz_117_down");
            human.WearShoes("cloth/nv_shoes_98");

            human.PlayAnimation("anim/HP_Share");

            Scene scene = SceneManager.GetInstance(context).GetCurrentScene();
            scene.AddElement(human);
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (mRendererHandle != IntPtr.Zero)
            {
                ResizeRenderer();
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            if (mRendererProcess != null)
            {
                mRendererProcess.Close();
            }
        }
    }
}
