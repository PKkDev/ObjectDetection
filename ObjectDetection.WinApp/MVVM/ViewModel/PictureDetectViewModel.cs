﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using ObjectDetection.WinApp.FrameSourceHelper;
using ObjectDetection.WinApp.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using YOLO4.Shared.Parser;
using static System.Net.WebRequestMethods;

namespace ObjectDetection.WinApp.MVVM.ViewModel
{
    public class PictureDetectViewModel : ObservableObject
    {
        public ICommand SelectImage { get; set; }
        public ICommand DetectOnImage { get; set; }
        public ICommand OpenLocalCacheFolder { get; set; }
        public ICommand SaveImage { get; set; }

        private bool _detectInProgress;
        public bool DetectInProgress { get => _detectInProgress; set => SetProperty(ref _detectInProgress, value); }

        private ImageSource _imagePrew;
        public ImageSource ImagePrew { get => _imagePrew; set => SetProperty(ref _imagePrew, value); }

        private StorageFile SavedImage;

        private readonly YoloOutputParser _yoloOutputParser;
        private readonly Yolo4Service _yolo4Service;

        private IFrameSource frameSource { get; set; }

        public PictureDetectViewModel(Yolo4Service yolo4Service)
        {
            _yolo4Service = yolo4Service;
            _yoloOutputParser = new();

            //ImagePrew = new BitmapImage(new Uri("https://warlu.com/wp-content/uploads/08-Warlukurlangu-Dog-program-1-CROPPED-1.jpg"));

            SelectImage = new RelayCommand(async () =>
            {
                FileOpenPicker picker = new();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");

                // Get the current window's HWND by passing in the Window object
                // Associate the HWND with the file picker
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                StorageFile file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    SavedImage = file;
                    //SavedImage = await file.CopyAsync(ApplicationData.Current.LocalCacheFolder);

                    using IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read);
                    BitmapImage bitmapImage = new();
                    bitmapImage.SetSource(fileStream);

                    ImagePrew = bitmapImage;

                }
            });

            OpenLocalCacheFolder = new RelayCommand(async () => await Launcher.LaunchFolderAsync(ApplicationData.Current.LocalCacheFolder));

            DetectOnImage = new RelayCommand(async () =>
            {
                if (SavedImage == null) return;

                if (frameSource != null)
                {
                    frameSource.FrameArrived -= M_frameSource_FrameArrived;
                    var disposableFrameSource = frameSource as IDisposable;
                    if (disposableFrameSource != null)
                        disposableFrameSource.Dispose();
                }

                frameSource = await FrameSourceFactory.CreateFrameSourceAsync(SavedImage, (sender, message) => { });
                if (frameSource != null)
                {
                    frameSource.FrameArrived += M_frameSource_FrameArrived;
                    await frameSource.StartAsync();
                }

            });

            SaveImage = new RelayCommand(async () =>
            {
                FileSavePicker fileSavePicker = new FileSavePicker();
                fileSavePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                fileSavePicker.FileTypeChoices.Add("JPEG files", new List<string>() { ".jpg" });
                fileSavePicker.SuggestedFileName = "image";

                // Get the current window's HWND by passing in the Window object
                // Associate the HWND with the file picker
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(fileSavePicker, hwnd);

                var outputFile = await fileSavePicker.PickSaveFileAsync();

                if (outputFile != null)
                {

                    using IRandomAccessStream stream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
                    //BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);



                    // BitmapDecoder decoder = await BitmapDecoder.CreateAsync(;

                    // BitmapImage bit = ImagePrew as BitmapImage;

                    //bit.

                    // Stream pixelStream = bit..PixelBuffer.AsStream();

                    // encoder.SetSoftwareBitmap(bit);
                    // await encoder.FlushAsync();

                    // stream.Dispose();




                    //using IRandomAccessStream stream2 = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
                    //BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream2);
                    //var decoder = await BitmapDecoder.CreateAsync(stream);
                    //var softBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                    //encoder.SetSoftwareBitmap(softBitmap);
                    //await encoder.FlushAsync();
                    //stream2.Dispose();


                    //DisplayInformation display = DisplayInformation.GetForCurrentView();
                    //var renderTargetBitmap = new RenderTargetBitmap();
                    //await renderTargetBitmap.RenderAsync(ImagePrew, (int)frameSource.FrameWidth, (int)frameSource.FrameHeight);

                    //IBuffer pixels = await renderTargetBitmap.GetPixelsAsync();
                    //byte[] bytes = pixels.ToArray();




                }
                else
                    return;
            });
        }

        private async void M_frameSource_FrameArrived(object sender, Windows.Media.VideoFrame videoFrame)
        {
            DetectInProgress = true;
            await Task.Run(async () =>
            {
                #region get SoftwareBitmap from videoFrame

                SoftwareBitmap targetSoftwareBitmap = videoFrame.SoftwareBitmap;
                VideoFrame m_renderTargetFrame = null;

                // If we receive a Direct3DSurface-backed VideoFrame, convert to a SoftwareBitmap in a format that can be rendered via the UI element
                if (targetSoftwareBitmap == null)
                {
                    if (m_renderTargetFrame == null)
                        m_renderTargetFrame = new VideoFrame(BitmapPixelFormat.Bgra8, videoFrame.Direct3DSurface.Description.Width, videoFrame.Direct3DSurface.Description.Height, BitmapAlphaMode.Ignore);

                    // Leverage the VideoFrame.CopyToAsync() method that can convert the input Direct3DSurface-backed VideoFrame to a SoftwareBitmap-backed VideoFrame
                    await videoFrame.CopyToAsync(m_renderTargetFrame);
                    targetSoftwareBitmap = m_renderTargetFrame.SoftwareBitmap;
                }
                // Else, if we receive a SoftwareBitmap-backed VideoFrame, if its format cannot already be rendered via the UI element, convert it accordingly
                else
                {
                    if (targetSoftwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || targetSoftwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Ignore)
                    {
                        if (m_renderTargetFrame == null)
                            m_renderTargetFrame = new VideoFrame(BitmapPixelFormat.Bgra8, targetSoftwareBitmap.PixelWidth, targetSoftwareBitmap.PixelHeight, BitmapAlphaMode.Ignore);

                        // Leverage the VideoFrame.CopyToAsync() method that can convert the input SoftwareBitmap-backed VideoFrame to a different format
                        await videoFrame.CopyToAsync(m_renderTargetFrame);
                        targetSoftwareBitmap = m_renderTargetFrame.SoftwareBitmap;
                    }
                }

                #endregion get SoftwareBitmap from videoFrame

                #region get probability

                var predict = await _yolo4Service.PredictAsync(targetSoftwareBitmap);
                var probability = _yoloOutputParser.ParseOutputs(predict);

                #endregion get probability

                #region draw probability

                App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    DetectInProgress = false;
                    var bitmap = await _yolo4Service.RenderProbabilityAsync(probability, targetSoftwareBitmap);
                    ImagePrew = bitmap;
                });

                #endregion draw probability

            });
        }
    }
}