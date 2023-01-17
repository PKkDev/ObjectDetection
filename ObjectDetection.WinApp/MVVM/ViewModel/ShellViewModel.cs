using CommunityToolkit.Mvvm.ComponentModel;
using ObjectDetection.WinApp.Services;

namespace ObjectDetection.WinApp.MVVM.ViewModel
{
    public class ShellViewModel : ObservableRecipient
    {
        public NavigationHelperService NavigationHelperService { get; private set; }

        public ShellViewModel(NavigationHelperService navigationHelperService)
        {
            NavigationHelperService = navigationHelperService;
        }
    }
}
