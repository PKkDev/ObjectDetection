using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.PointOfService;

namespace ObjectDetection.WinApp
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async Task Open()
        {
            DevicePicker devicePicker = new();
            devicePicker.Filter.SupportedDeviceClasses.Add(DeviceClass.All);

            //BluetoothLEDevice
            //devicePicker.Filter.SupportedDeviceSelectors.Add();

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(devicePicker, hwnd);

            Windows.Foundation.Rect rect = new Windows.Foundation.Rect(new Windows.Foundation.Point(0, 0), new Windows.Foundation.Point(0, 0));

            DeviceInformation di = await devicePicker.PickSingleDeviceAsync(rect);
            if (di != null)
            {
                try
                {
                }
                catch (Exception ex)
                {

                }
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            await Open();
        }
    }
}
