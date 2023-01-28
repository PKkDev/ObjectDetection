using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using ObjectDetection.WinApp.MVVM.View;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ObjectDetection.WinApp.Services
{
    public class NavigationHelperService
    {
        private NavigationView NavigationView { get; set; }
        private Frame ContentFrame { get; set; }

        private readonly Dictionary<string, Type> _pages = new()
        {
            { "Settings", typeof(SettingsPage) },
            { "PictureDetect", typeof(PictureDetectPage) },
            { "CameraDetect", typeof(CameraDetectPage) },
            { "VideoDetect", typeof(VideoDetectPage) },
            { "ScreenCapture", typeof(ScreenCapturePage) }
        };

        public NavigationHelperService() { }

        public void Initialize(NavigationView navigationView, Frame contentFrame)
        {
            NavigationView = navigationView;
            ContentFrame = contentFrame;

            NavigationView.BackRequested += OnBackRequested;
            NavigationView.ItemInvoked += OnItemInvoked;
            ContentFrame.NavigationFailed += OnNavigationFailed;
        }

        private void OnNavigationFailed(object sender, Microsoft.UI.Xaml.Navigation.NavigationFailedEventArgs e)
        {

        }

        private void OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            // _contentFrame.GoBack();
        }

        private void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked == true)
            {
                Navigate("Settings");
            }
            else if (args.InvokedItemContainer != null)
            {
                var navItemTag = args.InvokedItemContainer.Tag.ToString();
                Navigate(navItemTag);
            }
        }

        public void Navigate(string navItemTag)
        {
            Type _page = null;
            if (navItemTag == "Settings")
            {
                _page = typeof(SettingsPage);
            }
            else
            {
                var item = _pages.FirstOrDefault(p => p.Key.Equals(navItemTag));
                _page = item.Value;
            }
            // Get the page type before navigation so you can prevent duplicate
            // entries in the backstack.
            var preNavPageType = ContentFrame.CurrentSourcePageType;

            // Only navigate if the selected page isn't currently loaded.
            if (!(_page is null) && !Type.Equals(preNavPageType, _page))
            {
                // ContentFrame.Navigate(_page, null);
                ContentFrame.Navigate(_page, null, new DrillInNavigationTransitionInfo());
            }
        }
    }
}
