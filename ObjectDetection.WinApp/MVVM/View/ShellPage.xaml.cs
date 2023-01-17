using Microsoft.UI.Xaml.Controls;
using ObjectDetection.WinApp.MVVM.ViewModel;

namespace ObjectDetection.WinApp.MVVM.View
{
    public sealed partial class ShellPage : Page
    {
        public ShellViewModel ViewModel { get; set; }

        public ShellPage()
        {
            InitializeComponent();
            DataContext = ViewModel = App.GetService<ShellViewModel>();

            ViewModel.NavigationHelperService.Initialize(NavView, ContentFrame);
            ViewModel.NavigationHelperService.Navigate("PictureDetect");
        }
    }
}
