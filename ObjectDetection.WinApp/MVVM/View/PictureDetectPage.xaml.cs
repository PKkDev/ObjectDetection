using Microsoft.UI.Xaml.Controls;
using ObjectDetection.WinApp.MVVM.ViewModel;

namespace ObjectDetection.WinApp.MVVM.View
{
    public sealed partial class PictureDetectPage : Page
    {
        public PictureDetectViewModel ViewModel { get; set; }

        public PictureDetectPage()
        {
            InitializeComponent();
            ViewModel = App.GetService<PictureDetectViewModel>();
        }
    }
}
