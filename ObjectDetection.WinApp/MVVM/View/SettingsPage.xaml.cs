using Microsoft.UI.Xaml.Controls;
using ObjectDetection.WinApp.MVVM.ViewModel;

namespace ObjectDetection.WinApp.MVVM.View
{

    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; set; }

        public SettingsPage()
        {
            InitializeComponent();
            DataContext = ViewModel = App.GetService<SettingsViewModel>();
        }
    }
}
