using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using ObjectDetection.WinApp.FrameSourceHelper;
using ObjectDetection.WinApp.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Storage;
using Windows.System;
using YOLO4.Shared.DataStructures;
using YOLO4.Shared.Parser;

namespace ObjectDetection.WinApp.MVVM.ViewModel
{
    public class CameraDetectViewModel : ObservableRecipient
    {
        //public ICommand StartCameraPreview { get; set; }
        //public ICommand StopCameraPreview { get; set; }
        public ICommand OpenLocalCacheFolder { get; set; }
        public ICommand SelectCamera { get; set; }

        private bool _isDetectChecked;
        public bool IsDetectChecked
        { get => _isDetectChecked; set => SetProperty(ref _isDetectChecked, value); }


        // BitmapImage SoftwareBitmapSource
        private ImageSource _cameraImage;
        public ImageSource CameraImage { get => _cameraImage; set => SetProperty(ref _cameraImage, value); }

        private string _activityLog;
        public string ActivityLog { get => _activityLog; set => SetProperty(ref _activityLog, value); }

        private readonly YoloOutputParser _yoloOutputParser;
        private readonly Yolo4Service _yolo4Service;

        //private MediaCapture mediaCaptureManager;
        //private MediaFrameReader mediaFrameReader;
        //private bool captureManagerInitialized = false;

        //private SoftwareBitmap backBitmapBuffer;
        //private bool taskFrameRenderRunning = false;

        // Locks
        private SemaphoreSlim m_lock = new SemaphoreSlim(1);

        private IFrameSource frameSource { get; set; }

        SoftwareBitmapSource CameraImageSource;

        public CameraDetectViewModel(Yolo4Service yolo4Service)
        {
            CameraImageSource = new SoftwareBitmapSource();

            _yolo4Service = yolo4Service;
            _yoloOutputParser = new();

            //StartCameraPreview = new RelayCommand(() =>
            //{
            //    StartPreviewAsync();
            //});

            //StopCameraPreview = new RelayCommand(async () =>
            //{
            //    await CleanupMediaCaptureAsync();
            //});

            SelectCamera = new RelayCommand(async () =>
            {
                var devicePicker = new DevicePicker();
                devicePicker.Filter.SupportedDeviceClasses.Add(DeviceClass.VideoCapture);

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(devicePicker, hwnd);

                Windows.Foundation.Rect rect = new Windows.Foundation.Rect(new Windows.Foundation.Point(0, 0), new Windows.Foundation.Point(0, 0));

                DeviceInformation di = await devicePicker.PickSingleDeviceAsync(rect);
                if (di != null)
                {
                    try
                    {
                        await ConfigureFrameSourceAsync(di);
                    }
                    catch (Exception ex)
                    {
                        SetCameraLog($"Error occurred while initializating MediaCapture: {ex.Message}");
                    }
                }
            });

            OpenLocalCacheFolder = new RelayCommand(async () => await Launcher.LaunchFolderAsync(ApplicationData.Current.LocalCacheFolder));

        }

        public void Unload()
        {
            CameraImage = null;

            if (frameSource != null)
            {
                frameSource.FrameArrived -= FrameSource_FrameArrived;
                var disposableFrameSource = frameSource as IDisposable;
                if (disposableFrameSource != null)
                    disposableFrameSource.Dispose();
            }
        }

        private async Task ConfigureFrameSourceAsync(object source)
        {
            await m_lock.WaitAsync();
            {
                // Reset bitmap rendering component
                //UIProcessedPreview.Source = null;
                //m_renderTargetFrame = null;
                //m_processedBitmapSource = new SoftwareBitmapSource();
                //UIProcessedPreview.Source = m_processedBitmapSource;

                CameraImage = null;

                // Clean up previous frame source
                if (frameSource != null)
                {
                    frameSource.FrameArrived -= FrameSource_FrameArrived;
                    var disposableFrameSource = frameSource as IDisposable;
                    if (disposableFrameSource != null)
                        disposableFrameSource.Dispose();
                }

                // Create new frame source and register a callback if the source fails along the way
                frameSource = await FrameSourceFactory.CreateFrameSourceAsync(
                    source,
                    (sender, message) => { SetCameraLog(message); });

            }
            m_lock.Release();

            // If we obtained a valid frame source, start it
            if (frameSource != null)
            {
                frameSource.FrameArrived += FrameSource_FrameArrived; ;
                await frameSource.StartAsync();
            }
        }

        private void FrameSource_FrameArrived(object sender, Windows.Media.VideoFrame videoFrame)
        {
            if (m_lock.Wait(0))
            {
                Task.Run(async () =>
                {
                    try
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

                        YoloV4OutputData predict = null;
                        if (IsDetectChecked)
                            predict = await _yolo4Service.PredictAsync(targetSoftwareBitmap);

                        #endregion get probability

                        App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                        {
                            try
                            {
                                if (IsDetectChecked && predict != null)
                                {
                                    var probability = _yoloOutputParser.ParseOutputs(predict);
                                    var bitmap = await _yolo4Service.RenderProbabilityAsync(probability, targetSoftwareBitmap);
                                    await CameraImageSource.SetBitmapAsync(bitmap);
                                    CameraImage = CameraImageSource;
                                }
                                else
                                {
                                    await CameraImageSource.SetBitmapAsync(targetSoftwareBitmap);
                                    CameraImage = CameraImageSource;
                                }
                            }
                            catch (Exception ex)
                            {
                                SetCameraLog(ex.Message);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        SetCameraLog(ex.Message);
                    }
                    finally
                    {
                        m_lock.Release();
                    }
                });
            }
        }

        //private async void StartPreviewAsync()
        //{
        //    if (captureManagerInitialized == true)
        //        return;

        //    try
        //    {
        //        SetCameraLog("Start camera preview");

        //        //1. Select frame sources and frame source groups//
        //        var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();
        //        if (frameSourceGroups.Count <= 0)
        //        {
        //            SetCameraLog("No source groups found");
        //            return;
        //        }

        //        //Get the first frame source group and first frame source, Or write your code to select them//
        //        MediaFrameSourceGroup selectedFrameSourceGroup = frameSourceGroups[0];
        //        MediaFrameSourceInfo frameSourceInfo = selectedFrameSourceGroup.SourceInfos[0];

        //        //2. Initialize the MediaCapture object to use the selected frame source group//
        //        mediaCaptureManager = new MediaCapture();
        //        var settings = new MediaCaptureInitializationSettings
        //        {
        //            SourceGroup = selectedFrameSourceGroup,
        //            SharingMode = MediaCaptureSharingMode.SharedReadOnly,
        //            StreamingCaptureMode = StreamingCaptureMode.Video,
        //            MemoryPreference = MediaCaptureMemoryPreference.Cpu
        //        };
        //        await mediaCaptureManager.InitializeAsync(settings);

        //        // Возникает при возникновении ошибки во время записи мультимедиа.
        //        mediaCaptureManager.Failed += (MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs) =>
        //        {
        //            App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
        //            {
        //                SetCameraLog($"{errorEventArgs.Code}: {errorEventArgs.Message}");
        //                await CleanupMediaCaptureAsync();
        //            });
        //        };
        //        // Происходит при изменении состояния потока камеры.
        //        mediaCaptureManager.CameraStreamStateChanged += (MediaCapture sender, object args) => { };
        //        // Происходит при изменении состояния монопольного управления устройства захвата.
        //        mediaCaptureManager.CaptureDeviceExclusiveControlStatusChanged += (MediaCapture sender, MediaCaptureDeviceExclusiveControlStatusChangedEventArgs args) => { };
        //        // Происходит при превышении предела для записей.
        //        mediaCaptureManager.RecordLimitationExceeded += (MediaCapture sender) => { };

        //        //3. Initialize Image Preview Element with xaml Image Element.//
        //        // imagePreviewElement = imagePreview;
        //        // imagePreviewElement.Source = new SoftwareBitmapSource();

        //        //4. Create a frame reader for the frame source//
        //        MediaFrameSource mediaFrameSource = mediaCaptureManager.FrameSources[frameSourceInfo.Id];
        //        mediaFrameReader = await mediaCaptureManager.CreateFrameReaderAsync(mediaFrameSource, MediaEncodingSubtypes.Argb32);
        //        mediaFrameReader.FrameArrived += MediaFrameReader_FrameArrived;
        //        await mediaFrameReader.StartAsync();

        //        captureManagerInitialized = true;
        //        SetCameraLog($"Media preview from device: {selectedFrameSourceGroup.DisplayName}");
        //    }
        //    catch (Exception e)
        //    {
        //        SetCameraLog(e.Message);
        //    }
        //}

        //private void MediaFrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        //{
        //    try
        //    {
        //        var mediaFrameReference = sender.TryAcquireLatestFrame();
        //        var videoMediaFrame = mediaFrameReference?.VideoMediaFrame;
        //         var softwareBitmap = videoMediaFrame?.SoftwareBitmap;

        //        if (softwareBitmap != null)
        //        {
        //            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
        //                softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
        //            {
        //                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        //            }

        //            // Swap the processed frame to backBuffer and dispose of the unused image.
        //            softwareBitmap = Interlocked.Exchange(ref backBitmapBuffer, softwareBitmap);
        //            softwareBitmap?.Dispose();





        //            // Changes to XAML ImageElement must happen on UI thread through Dispatcher
        //            //var task = imagePreviewElement.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
        //            App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
        //            {
        //                // Don't let two copies of this task run at the same time.
        //                if (taskFrameRenderRunning) return;

        //                taskFrameRenderRunning = true;

        //                // Keep draining frames from the backbuffer until the backbuffer is empty.
        //                SoftwareBitmap latestBitmap;
        //                while ((latestBitmap = Interlocked.Exchange(ref backBitmapBuffer, null)) != null)
        //                {
        //                    #region from/to file

        //                    StorageFile savedImage = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync("camPhoto.jpg", CreationCollisionOption.ReplaceExisting);
        //                    using IRandomAccessStream stream = await savedImage.OpenAsync(FileAccessMode.ReadWrite);
        //                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
        //                    encoder.SetSoftwareBitmap(latestBitmap);
        //                    await encoder.FlushAsync();

        //                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.JpegDecoderId, stream);
        //                    var imageDpi = Math.Max(decoder.DpiX, decoder.DpiY);

        //                    stream.Dispose();

        //                    var savedImageProps = await savedImage.Properties.GetImagePropertiesAsync();

        //                    YoloV4InputData image = new() { ImagePath = savedImage.Path };
        //                    var predict = _yolo4Service.Predict(image);
        //                    var probability = _yoloOutputParser.ParseOutputs(predict);
        //                    //var probability = new List<YoloV4Result>();


        //                    var bitmap = await _yolo4Service.DrawTest(probability, savedImage.Path, savedImageProps.Width, savedImageProps.Height, imageDpi, savedImage.DisplayName);
        //                    CameraImage = bitmap;


        //                    #endregion fro/to file

        //                    //var CameraImageSource = new SoftwareBitmapSource();
        //                    //await CameraImageSource.SetBitmapAsync(latestBitmap);
        //                    //CameraImage = CameraImageSource;

        //                    latestBitmap.Dispose();
        //                }

        //                taskFrameRenderRunning = false;
        //            });
        //        }

        //        if (mediaFrameReference != null)
        //        {
        //            mediaFrameReference.Dispose();
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        SetCameraLog(e.Message);
        //    }
        //}

        //private async Task CleanupMediaCaptureAsync()
        //{
        //    if (mediaCaptureManager != null)
        //    {
        //        using var mediaCapture = mediaCaptureManager;
        //        mediaCaptureManager = null;

        //        mediaFrameReader.FrameArrived -= MediaFrameReader_FrameArrived;
        //        await mediaFrameReader.StopAsync();
        //        mediaFrameReader.Dispose();
        //    }

        //    captureManagerInitialized = false;
        //    SetCameraLog("Media preview has canceled");
        //}

        private void SetCameraLog(string message)
            => App.MainWindow.DispatcherQueue.TryEnqueue(() => ActivityLog = $"{ActivityLog} {Environment.NewLine} {message}");
    }
}
