using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using System.Threading.Tasks;
using System;
using Windows.Media.Playback;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Media;
using ObjectDetection.WinApp.MVVM.ViewModel;
using Windows.Graphics.Imaging;
using Microsoft.UI.Xaml.Media.Imaging;
using YOLO3.Shared.DataStructures;
using ObjectDetection.WinApp.Services;
using YOLO3.Shared.Parser;
using System.Linq;
using System.Threading;
using YOLO4.Shared.Parser;
using YOLO4.Shared.DataStructures;

namespace ObjectDetection.WinApp.MVVM.View
{
    public sealed partial class VideoDetectPage : Page
    {
        public VideoDetectViewModel ViewModel { get; set; }

        private SoftwareBitmapSource CameraImageSource { get; set; }

        public uint FrameWidth { get; private set; }
        public uint FrameHeight { get; private set; }

        private MediaPlayer m_mediaPlayer = null;
        private VideoFrame m_videoFrame = null;

        private readonly Yolo4OutputParser _yoloOutputParser;
        private readonly Yolo4Service _yoloService;

        // Locks
        private SemaphoreSlim m_lock = new(1);

        public VideoDetectPage()
        {
            InitializeComponent();
            ViewModel = App.GetService<VideoDetectViewModel>();

            _yoloService = App.GetService<Yolo4Service>();
            _yoloOutputParser = new();

            CameraImageSource = new();
            imagePreview.Source = CameraImageSource;
        }

        private async void SelectVideo(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            FileOpenPicker picker = new();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".wmv");
            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".wma");
            picker.FileTypeFilter.Add(".mp3");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                await CameraImageSource.SetBitmapAsync(null);

                m_mediaPlayer = new MediaPlayer()
                {
                    Source = MediaSource.CreateFromStorageFile(file),
                    IsVideoFrameServerEnabled = true,
                    RealTimePlayback = true,
                    IsMuted = false,
                    IsLoopingEnabled = true
                };
                m_mediaPlayer.CommandManager.IsEnabled = false;
                m_mediaPlayer.MediaOpened += M_mediaPlayer_MediaOpened;
                m_mediaPlayer.MediaEnded += (MediaPlayer sender, object args) =>
                {
                    Dispose();
                };
                m_mediaPlayer.MediaFailed += (MediaPlayer sender, MediaPlayerFailedEventArgs args) =>
                {
                    Dispose();
                };
            }
        }

        private void M_mediaPlayer_MediaOpened(MediaPlayer sender, object args)
        {
            FrameWidth = m_mediaPlayer.PlaybackSession.NaturalVideoWidth;
            FrameHeight = m_mediaPlayer.PlaybackSession.NaturalVideoHeight;

            m_videoFrame = VideoFrame.CreateAsDirect3D11SurfaceBacked(
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                (int)FrameWidth,
                (int)FrameHeight);

            m_mediaPlayer.VideoFrameAvailable += M_mediaPlayer_VideoFrameAvailable;
        }

        private void M_mediaPlayer_VideoFrameAvailable(MediaPlayer sender, object args)
        {
            m_mediaPlayer.CopyFrameToVideoSurface(m_videoFrame.Direct3DSurface);
            m_videoFrame.SystemRelativeTime = m_mediaPlayer.PlaybackSession.Position;

            DisplayImage(m_videoFrame);
        }

        private void DisplayImage(Windows.Media.VideoFrame videoFrame)
        {
            if (m_lock.Wait(0))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        #region get SoftwareBitmap from videoFrame

                        SoftwareBitmap targetSoftwareBitmap = m_videoFrame.SoftwareBitmap;
                        VideoFrame m_renderTargetFrame = null;

                        if (targetSoftwareBitmap == null)
                        {
                            if (m_renderTargetFrame == null)
                                m_renderTargetFrame = new VideoFrame(BitmapPixelFormat.Bgra8, m_videoFrame.Direct3DSurface.Description.Width, m_videoFrame.Direct3DSurface.Description.Height, BitmapAlphaMode.Ignore);

                            await m_videoFrame.CopyToAsync(m_renderTargetFrame);
                            targetSoftwareBitmap = m_renderTargetFrame.SoftwareBitmap;
                        }
                        else
                        {
                            if (targetSoftwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || targetSoftwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Ignore)
                            {
                                if (m_renderTargetFrame == null)
                                    m_renderTargetFrame = new VideoFrame(BitmapPixelFormat.Bgra8, targetSoftwareBitmap.PixelWidth, targetSoftwareBitmap.PixelHeight, BitmapAlphaMode.Ignore);

                                await m_videoFrame.CopyToAsync(m_renderTargetFrame);
                                targetSoftwareBitmap = m_renderTargetFrame.SoftwareBitmap;
                            }
                        }

                        #endregion get SoftwareBitmap from videoFrame

                        #region get probability

                        var IsDetectChecked = true;
                        YoloV4OutputData predict = await _yoloService.PredictAsync(targetSoftwareBitmap);

                        #endregion get probability

                        App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                        {
                            try
                            {
                                if (IsDetectChecked && predict != null)
                                {
                                    var probability = _yoloOutputParser.ParseOutputs(predict);
                                    var bitmap = await _yoloService.RenderProbabilityAsync(probability.ToList(), targetSoftwareBitmap);
                                    await CameraImageSource.SetBitmapAsync(bitmap);
                                    //CameraImage = CameraImageSource;
                                }
                                else
                                {
                                    await CameraImageSource.SetBitmapAsync(targetSoftwareBitmap);
                                    //CameraImage = CameraImageSource;
                                }
                            }
                            catch (Exception ex)
                            {
                                throw;
                            }
                        });

                        //App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                        //{
                        //    await CameraImageSource.SetBitmapAsync(targetSoftwareBitmap);
                        //});
                    }
                    catch (Exception ex)
                    {

                    }
                    finally
                    {
                        m_lock.Release();
                    }
                });
            }
        }

        /// <summary>
        /// Dispose method implementation
        /// </summary>
        public void Dispose()
        {
            m_mediaPlayer?.Pause();
            m_mediaPlayer?.Dispose();
        }

        public Task StartAsync()
        {
            m_mediaPlayer.Play();
            return Task.FromResult(true);
        }

        public Task StopAsync()
        {
            m_mediaPlayer.Pause();
            return Task.FromResult(true);
        }


        private async void StartVideo(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await StartAsync();
        }

        private async void StopVideo(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await StopAsync();
        }

    }
}
