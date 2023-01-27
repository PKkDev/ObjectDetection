using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Collections.Generic;
using System.Threading;

using ObjectDetection.WinApp.DirectXCaptureEncoder;

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Storage;
//
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Graphics.DirectX.Direct3D11;

namespace ObjectDetection.WinApp.MVVM.View
{
    public sealed partial class CaptureTestPage : Page
    {
        // Capture API objects.
        private SizeInt32 _lastSize;
        private GraphicsCaptureItem _captureItem;
        private Direct3D11CaptureFramePool _framePool;
        private GraphicsCaptureSession _session;

        // Non-API related members.
        private CanvasDevice _canvasDevice;
        private CompositionGraphicsDevice _compositionGraphicsDevice;
        private Compositor _compositor;
        private CompositionDrawingSurface _surface;
        private IDirect3DSurface _currentFrame;

        #region
        public Queue<SurfaceWithInfo> frames = new();
        public DateTime startedAt = DateTime.Now;
        public bool _isRecording = false;
        #endregion

        public CaptureTestPage()
        {
            InitializeComponent();
            Setup();
        }

        private void Setup()
        {
            _canvasDevice = new CanvasDevice();

            _compositor = App.MainWindow.Compositor;

            _compositionGraphicsDevice = CanvasComposition.CreateCompositionGraphicsDevice(
                _compositor,
                _canvasDevice);

            _surface = _compositionGraphicsDevice.CreateDrawingSurface(
                new Size(400, 400),
                Microsoft.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                Microsoft.Graphics.DirectX.DirectXAlphaMode.Premultiplied);    // This is the only value that currently works with
                                                                               // the composition APIs
            var visual = _compositor.CreateSpriteVisual();
            visual.RelativeSizeAdjustment = Vector2.One;
            var brush = _compositor.CreateSurfaceBrush(_surface);
            brush.HorizontalAlignmentRatio = 0.5f;
            brush.VerticalAlignmentRatio = 0.5f;
            brush.Stretch = CompositionStretch.Uniform;
            visual.Brush = brush;
            ElementCompositionPreview.SetElementChildVisual(gridToPreview, visual);
        }

        private async void Click_SelectGraphicsCapture(object sender, RoutedEventArgs e)
        {
            GraphicsCapturePicker picker = new();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            GraphicsCaptureItem captureItem = await picker.PickSingleItemAsync();

            if (captureItem != null)
            {
                StartCaptureInternal(captureItem);
            }
        }

        private async void StartCaptureInternal(GraphicsCaptureItem item)
        {
            // Stop the previous capture if we had one.
            StopCapture();

            _captureItem = item;
            _lastSize = _captureItem.Size;

            _framePool = Direct3D11CaptureFramePool.Create(
                _canvasDevice,
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                _captureItem.Size);

            _framePool.FrameArrived += OnFrameArrived;

            _captureItem.Closed += OnCaptureItemClosed;

            _session = _framePool.CreateCaptureSession(_captureItem);
            _session.StartCapture();
        }

        private void OnCaptureItemClosed(GraphicsCaptureItem sender, object args)
        {
            StopCapture();
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            using Direct3D11CaptureFrame frame = _framePool.TryGetNextFrame();
            ProcessFrame(frame);
        }
        private async void ProcessFrame(Direct3D11CaptureFrame frame)
        {
            bool needsReset = false;
            bool recreateDevice = false;

            if ((frame.ContentSize.Width != _lastSize.Width) || (frame.ContentSize.Height != _lastSize.Height))
            {
                needsReset = true;
                _lastSize = frame.ContentSize;
            }

            try
            {
                _currentFrame = frame.Surface;

                CanvasBitmap canvasBitmap = CanvasBitmap.CreateFromDirect3D11Surface(_canvasDevice, frame.Surface);
                FillSurfaceWithBitmap(canvasBitmap);

                //var sb = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
                //if (sb.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || sb.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                //    sb = SoftwareBitmap.Convert(sb, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                //var sfbs = new SoftwareBitmapSource();
                //await sfbs.SetBitmapAsync(sb);
                //previewImage.Source = sfbs;
                //sb.Dispose();

                frames.Enqueue(new SurfaceWithInfo()
                {
                    Surface = frame.Surface,
                    SystemRelativeTime = frame.SystemRelativeTime
                });
            }
            catch (Exception e) when (_canvasDevice.IsDeviceLost(e.HResult))
            {
                needsReset = true;
                recreateDevice = true;
            }

            if (needsReset)
                ResetFramePool(frame.ContentSize, recreateDevice);
        }
        private void FillSurfaceWithBitmap(CanvasBitmap canvasBitmap)
        {
            CanvasComposition.Resize(_surface, canvasBitmap.Size);

            using var session = CanvasComposition.CreateDrawingSession(_surface);
            session.Clear(Colors.Transparent);
            session.DrawImage(canvasBitmap);
        }
        private void ResetFramePool(SizeInt32 size, bool recreateDevice)
        {
            do
            {
                try
                {
                    if (recreateDevice)
                    {
                        _canvasDevice = new CanvasDevice();
                    }

                    _framePool.Recreate(
                        _canvasDevice,
                        Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                        2,
                        size);
                }
                // This is the device-lost convention for Win2D.
                catch (Exception e) when (_canvasDevice.IsDeviceLost(e.HResult))
                {
                    _canvasDevice = null;
                    recreateDevice = true;
                }
            } while (_canvasDevice == null);
        }

        private async void Click_StartCapture(object sender, RoutedEventArgs e)
        {
            if (!_isRecording)
            {

                #region
                var width = _captureItem.Size.Width;
                var height = _captureItem.Size.Height;

                VideoEncodingProperties videoProps = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, (uint)width, (uint)height);
                VideoStreamDescriptor videoDescriptor = new(videoProps);

                MediaStreamSource streamSource = new(videoDescriptor);
                streamSource.BufferTime = TimeSpan.FromSeconds(0);
                streamSource.SampleRequested += StreamSource_SampleRequested;
                streamSource.Closed += (MediaStreamSource sender, MediaStreamSourceClosedEventArgs args) => { };
                streamSource.Starting += (MediaStreamSource sender, MediaStreamSourceStartingEventArgs args) =>
                {
                    startedAt = DateTime.Now;
                    //using (var frame = _frameGenerator.WaitForNewFrame()) { args.Request.SetActualStartPosition(frame.SystemRelativeTime); }
                };
                streamSource.Paused += (MediaStreamSource sender, object args) => { };
                streamSource.SwitchStreamsRequested += (MediaStreamSource sender, MediaStreamSourceSwitchStreamsRequestedEventArgs args) => { };
                #endregion

                #region
                MediaEncodingProfile encodingProfile = new();
                encodingProfile.Container.Subtype = "MPEG4";
                encodingProfile.Video.Subtype = "H264";
                encodingProfile.Video.Width = 1920;
                encodingProfile.Video.Height = 1080;
                encodingProfile.Video.Bitrate = 18000000;
                encodingProfile.Video.FrameRate.Numerator = 30;
                encodingProfile.Video.FrameRate.Denominator = 1;
                encodingProfile.Video.PixelAspectRatio.Numerator = 1;
                encodingProfile.Video.PixelAspectRatio.Denominator = 1;
                #endregion

                #region
                var tempFolder = ApplicationData.Current.LocalCacheFolder;
                var file = await tempFolder.CreateFileAsync($"{DateTime.Now:yyyyMMdd-HHmm-ss}.mp4", CreationCollisionOption.ReplaceExisting);
                var outputStream = await file.OpenAsync(FileAccessMode.ReadWrite);
                #endregion

                #region
                try
                {
                    MediaTranscoder transcoder = new();
                    //transcoder.HardwareAccelerationEnabled = true;

                    var transcode = await transcoder.PrepareMediaStreamSourceTranscodeAsync(streamSource, outputStream, encodingProfile);
                    if (!transcode.CanTranscode)
                        throw new Exception($"transcode can not transcode");

                    // start streamSource
                    await Task.Delay((int)TimeSpan.FromSeconds(5).TotalMilliseconds);
                    var op = transcode.TranscodeAsync();
                    //op.Progress += new AsyncActionProgressHandler<double>(TranscodeProgress);
                    //op.Completed += new AsyncActionWithProgressCompletedHandler<double>(TranscodeComplete);

                    _isRecording = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex}");
                    throw;
                }
                #endregion

            }
        }
        private void StreamSource_SampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            try
            {
                if (frames.Count == 0)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(50));
                    StreamSource_SampleRequested(sender, args);
                }
                var videoFrame = frames.Dequeue();

                if (videoFrame == null)
                {
                    args.Request.Sample = null;
                    return;
                }

                var timestamp = DateTime.Now - startedAt;
                //var samp = MediaStreamSample.CreateFromDirect3D11Surface(videoFrame.Surface, videoFrame.SystemRelativeTime);
                var samp = MediaStreamSample.CreateFromDirect3D11Surface(videoFrame.Surface, timestamp);

                samp.Processed += (MediaStreamSample sender, object args) => { };
                args.Request.Sample = samp;
            }
            catch (Exception ex)
            {
                args.Request.Sample = null;
                return;
            }
        }

        private async void Click_StopCapture(object sender, RoutedEventArgs e)
        {
            _isRecording = false;
            frames.Enqueue(null);
            StopCapture();
        }

        private async void Click_TakeScreenShot(object sender, RoutedEventArgs e)
        {
            await SaveImageAsync();
        }

        private async Task SaveImageAsync()
        {
            if (_currentFrame == null)
                return;

            CanvasBitmap canvasBitmap = CanvasBitmap.CreateFromDirect3D11Surface(_canvasDevice, _currentFrame);

            StorageFile file = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync("screenShot.png", CreationCollisionOption.GenerateUniqueName);
            using var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite);
            await canvasBitmap.SaveAsync(fileStream, CanvasBitmapFileFormat.Png, 1f);
        }

        public void StopCapture()
        {
            if (_captureItem != null)
                _captureItem.Closed -= OnCaptureItemClosed;
            _captureItem = null;

            //_canvasDevice.Dispose();
            //_canvasDevice = null;

            _framePool?.Dispose();
            _framePool = null;

            _session?.Dispose();
            _session = null;
        }

    }
}
