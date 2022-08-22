# AweSDK for WPF
## Build the environment
Import the following three dynamic libraries into the project
- `AweSDK.dll`
- `AweSDK.Core.dll`
- `AweSDK.DataBridge.dll`

`AweSDK` depends on the third-party dynamic library `Newtonsoft.Json`, which can be installed via NuGet. 

Extract the renderer program `AweRenderer` to a custom directory, for example, `E:\Workspace\Demo\WPF\AweRenderer`. We will start the `AweRenderer.exe` process later with code to render 3D content.

## Environment Setup
1. Create a Setup method. This method creates a context `Context` object for each interface of the SDK, initializes the SDK environment, and fills in the SDK license information. The code is as follows.
- Import the necessary namespaces. For example.

```csharp
using AweSDK;
using AweSDK.Core;
using AweSDK.Core.Values;
using AweSDK.Scene;
using AweSDK.DataBridge;
using AweSDK.DataBridge.Sockets;
using AweSDK.Authorization;
```
- Create a context, start the renderer process, and setup the environment. As follows.
```csharp
private Context Setup(string rendererPath)
{
    // Create a context
    Context context = new Context();
    // Setup environment
    AweSDK.Environment.Setup(context);
    // Scan for available socket ports on the local machine for data exchanging.
    int port = SocketClient.GetAvailablePort(10000);
    // Start the renderer process
    StartRenderer(context, rendererPath, port);
    // Setup a data bridge for communication with renderer processes
    SetupDataBridge(context, port);
    // Setup license
    SetupLicense(context);
    // Setup resource directories
    SetupResources(context);
    return context;
}
```
## Start the renderer process
In order to embed the renderer in the grid of the WPF main window, we first open `MainWindow.xmal`, find the default `Grid` tag, add an attribute `x:Name="MainGrid"`.
Next, we need to declare some Win32 APIs. as follows.
```csharp
[DllImport("user32.dll", SetLastError = true)]
public static extern bool MoveWindow(IntPtr hwnd, int x, int y, int cx, int cy, bool repaint);

[DllImport("user32.dll", SetLastError = true)]
public static extern int SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

[DllImport("user32.dll", SetLastError = true)]
public static extern void SetWindowLongPtr(IntPtr hwnd, int nIndex, long dwNewLong);

public const int GWL_STYLE = -16;
public const long WS_CHILD = 0x40000000L;
public const long WS_VISIBLE = 0x10000000L;
```
Start the renderer process and embed it in the grid of the main window.
```csharp
private void StartRenderer(Context context, string path, int port)
{
    // Start the renderer process
    path = Path.Combine(path, "AweRenderer.exe");
    mRendererProcess = new Process();
    ProcessStartInfo startInfo = new ProcessStartInfo(path);
    startInfo.Arguments = $"--port {port}";
    startInfo.UseShellExecute = false;
    startInfo.CreateNoWindow = true;
    mRendererProcess.StartInfo = startInfo;
    mRendererProcess.Start();

    // Wait for the window handle of the renderer process to become available
    while (mRendererProcess.MainWindowHandle == IntPtr.Zero)
    {
        Thread.Sleep(1);
        mRendererProcess.Refresh();
    }

    // Embed the renderer in the grid of the main window.
    IntPtr parentHandler = ((HwndSource)PresentationSource.FromVisual(MainGrid)).Handle;
    mRendererHandle = mRendererProcess.MainWindowHandle;
    SetWindowLongPtr(mRendererHandle, GWL_STYLE, WS_VISIBLE);
    SetParent(mRendererHandle, parentHandler);

    // Resize the renderer window
    ResizeRenderer();
}
```

The last step above, `ResizeRenderer()`, is to resize the renderer. 
As a simple example, here the renderer is spread over the entire main window by default. 
Since one unit value of WPF size is `1/96 inch` and one unit value of renderer is `pixel`, some conversion is needed to control it. 
Here we provide a convenient unit conversion helper method as follows.
```csharp
private void ResizeRenderer()
{
    // Get the grid's width and height 
    var actualWidth = MainGrid.ActualWidth;
    var actualHeight = MainGrid.ActualHeight;
    // Convert the unit.
    TransformToPixels(this, actualWidth, actualHeight, out int width, out int height);
    // Control the position and size of the renderer window so that it spreads over the entire main window.
    MoveWindow(mRendererHandle, 0, 0, width, height, true);
}

// A unit conversion helper method.
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
```
In general, when the window size changes, the renderer has to scale the window along with it. 
So, as an example, we can listen to the `SizeChanged` event of `Window` to control the change of the renderer window. For example.
```csharp
// Callback that listens to the SizeChanged event.
private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
{
    if (mRendererHandle != IntPtr.Zero)
    {
        ResizeRenderer();
    }
}
```
## Setup the data bridge
The SDK uses sockets to communicate data with the renderer process, so we need to setup a socket data bridge.
```csharp
private void SetupDataBridge(Context context, int port)
{
    var exchanger = DataExchanger.GetInstance(context);
    var client = new SocketClient(context, "127.0.0.1", port);
    client.Connect();
    exchanger.SetDataBridge(client);
}
```

## Setup License
Developers need to apply for `AppKey` and `AppSecret` in the open platform and set them to the SDK before they can use the functions of the SDK.
```csharp
private void SetupLicense(Context context)
{
    LicenseManager licenseManager = LicenseManager.GetInstance(context);
    // Please replace the AppKey and AppSecret.
    licenseManager.AppKey = "{YourAppKey}";
    licenseManager.AppSecret = "{YourAppSecret}";
}
```
**Note: Developers need to replace the `[YourAppKey]` and `[YourAppSecret]` in the sample code with the `AppKey` and `AppSecret` values that have been applied. **

## Setup resource paths
The SDK relies on resources such as animations, dresses, etc., so we need to setup cache paths and resource paths so that the SDK can load resources and control caching.
```csharp
private void SetupResources(Context context)
{
    ResourceManager resourceManager = ResourceManager.GetInstance(context);
    // Set the cache directory.
    resourceManager.SetCacheDirectory("E:\Workspace\Demo\WPF\AweCache");
    // Add a resource directory, you can add more than one.
    resourceManager.AddResourceDirectory("E:\Workspace\Demo\WPF\AweResources");
}
```
Developers need to extract the downloaded resource package to the resource path provided above by themselves.


## Run environment
After the environment setup method is defined, we need to run the environment setup method in a suitable place. 
Since starting the renderer sub-process and embedding it in the main window requires getting the handle of the main window, 
`SourceInitialized` is the earliest event in the lifecycle events of the WPF window where we can get the handle of the main window. 
Therefore, we can consider running the environment in the callback method of the `SourceInitialized` event. For example:

```csharp
// Callback that listens to the SourceInitialized event.
private void MainWindow_SourceInitialized(object? sender, System.EventArgs e)
{
    Context context = InitializeAweSDK();
    // Once the environment is setup, it's time to load the human.
    LoadHuman(context);
}

// Run the environment setup.
private Context InitializeAweSDK()
{
    string rendererPath = @"E:\Workspace\Demo\WPF\AweRenderer";
    Context context = Setup(rendererPath);
    return context;
}
```

## Load Human
Once the above environment is running, we call the interface `LoadHuman(context)` for loading a human in the last step. 
```csharp
private void LoadHuman(Context context)
{
    // Quickly build a human using information such as gender, face mapping and face target。
    var gender = Human.Gender.Female;
    var faceTarget = "xiaojing/face.target";
    var faceMapping = "xiaojing/face.jpg";
    var baseInfo = new Human.BaseInfo(gender, faceTarget, faceMapping);
    Human human = new Human(context, baseInfo);

    // Set target parameters for the human.
    human.SetTarget("20003", 1f);
    human.SetTarget("23002", 0.5f);
    human.SetTarget("20101", 0.4769f);
    human.SetTarget("20102", -0.3075f);
    human.SetTarget("20502", -0.3522f);
    human.SetTarget("23202", 0.4769f);
    human.SetTarget("23503", -0.8489f);

    // Wear hair, outfits, shoes, etc.
    human.WearHair("cloth/nv_tf_128");
    human.WearOutfits(
        "cloth/nv_up_06", 
        "cloth/nv_tz_117_down");
    human.WearShoes("cloth/nv_shoes_98");

    // Play an animation.
    human.PlayAnimation("anim/HP_Share");

    // Add the human to the scene.
    Scene scene = SceneManager.GetInstance(context).GetCurrentScene();
    scene.AddElement(human);
}
```

**Noted that resources such as face mapping, face target, hairs, dresses, animation, etc. should be placed under the resource path in advance. **

Run the program and you will see a digital human in the window.


