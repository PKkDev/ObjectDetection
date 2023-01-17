using Microsoft.UI.Xaml.Controls;
using ObjectDetection.WinApp.MVVM.ViewModel;

namespace ObjectDetection.WinApp.MVVM.View
{
    public sealed partial class CameraDetectPage : Page
    {
        public CameraDetectViewModel ViewModel { get; set; }

        public CameraDetectPage()
        {
            InitializeComponent();
            ViewModel = App.GetService<CameraDetectViewModel>();
        }

        private void Page_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.Unload();
        }
    }
}
