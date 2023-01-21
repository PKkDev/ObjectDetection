using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ObjectDetection.WinApp.MVVM.View;
using ObjectDetection.WinApp.MVVM.ViewModel;
using ObjectDetection.WinApp.Services;
using System;

namespace ObjectDetection.WinApp
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {

        public static Window MainWindow { get; set; }
        public IHost Host { get; }

        private UIElement? _shell { get; set; }

        public static T GetService<T>() where T : class
        {
            if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
                throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");

            return service;
        }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();

            Host = Microsoft.Extensions.Hosting.Host.
            CreateDefaultBuilder().
            UseContentRoot(AppContext.BaseDirectory).
            ConfigureServices((context, services) =>
            {
                // Views and ViewModels
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<SettingsPage>();
                services.AddTransient<ShellViewModel>();
                services.AddTransient<ShellPage>();
                services.AddTransient<PictureDetectViewModel>();
                services.AddTransient<PictureDetectPage>();
                services.AddTransient<CameraDetectViewModel>();
                services.AddTransient<CameraDetectPage>();

                services.AddTransient<NavigationHelperService>();

                services.AddSingleton<Yolo4Service>();
                services.AddSingleton<Yolo3Service>();
            })
            .Build();

            UnhandledException += App_UnhandledException;

            //OnShareTargetActivated
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            throw new NotImplementedException();
        }



        //protected override void OnShareTargetActivated(ShareTargetActivatedEventArgs args)
        //{
        //    var rootFrame = new Frame();
        //    rootFrame.Navigate(typeof(MainPage), args.ShareOperation);
        //    Window.Current.Content = rootFrame;
        //    Window.Current.Activate();
        //}


        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();

            _shell = App.GetService<ShellPage>();
            MainWindow.Content = _shell ?? new Frame();

            MainWindow.Activate();
        }
    }

}
