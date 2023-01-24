using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;

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
using System.Collections.Generic;
using Windows.Media;
using Windows.Graphics.DirectX.Direct3D11;
using System.Threading;
using ObjectDetection.WinApp.DirectXCaptureEncoder;
using CommunityToolkit.WinUI.UI.Controls;
using Windows.Media.Playback;

//using Windows.UI;
//using Windows.UI.Composition;
//using Windows.UI.Xaml;
//using Windows.UI.Xaml.Controls;
//using Windows.UI.Xaml.Hosting;


namespace ObjectDetection.WinApp.MVVM.View
{
    public sealed partial class CaptureTestPage : Page
    {
        // Capture API objects.
        private SizeInt32 _lastSize;
        private GraphicsCaptureItem _item;
        private Direct3D11CaptureFramePool _framePool;
        private GraphicsCaptureSession _session;

        // Non-API related members.
        private CanvasDevice _canvasDevice;
        private CompositionGraphicsDevice _compositionGraphicsDevice;
        private Compositor _compositor;
        private CompositionDrawingSurface _surface;
        private CanvasBitmap _currentFrame;
        private string _screenshotFilename = "test.png";

        public CaptureTestPage()
        {
            InitializeComponent();
            Setup();
        }

        private void Setup()
        {
            _canvasDevice = new CanvasDevice();

            _compositionGraphicsDevice = CanvasComposition.CreateCompositionGraphicsDevice(
                App.MainWindow.Compositor,
                _canvasDevice);

            _compositor = App.MainWindow.Compositor;

            _surface = _compositionGraphicsDevice.CreateDrawingSurface(
                new Size(400, 400),
                Microsoft.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                Microsoft.Graphics.DirectX.DirectXAlphaMode.Premultiplied);    // This is the only value that currently works with
                                                                               // the composition APIs.

            var visual = _compositor.CreateSpriteVisual();
            visual.RelativeSizeAdjustment = Vector2.One;
            var brush = _compositor.CreateSurfaceBrush(_surface);
            brush.HorizontalAlignmentRatio = 0.5f;
            brush.VerticalAlignmentRatio = 0.5f;
            brush.Stretch = CompositionStretch.Uniform;
            visual.Brush = brush;
            ElementCompositionPreview.SetElementChildVisual(this, visual);
        }

        public async Task StartCaptureAsync()
        {
            // The GraphicsCapturePicker follows the same pattern the
            // file pickers do.
            var picker = new GraphicsCapturePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            GraphicsCaptureItem item = await picker.PickSingleItemAsync();

            // The item may be null if the user dismissed the
            // control without making a selection or hit Cancel.
            if (item != null)
            {

                #region
                var w = (uint)item.Size.Width;
                var h = (uint)item.Size.Height;
                uint frn = 30;
                uint frd = 1;
                var videoProps = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, w, h);
                var videoDescriptor = new VideoStreamDescriptor(videoProps);

                //videoDescriptor.EncodingProperties.FrameRate.Numerator = frn;
                //videoDescriptor.EncodingProperties.FrameRate.Denominator = frd;
                //videoDescriptor.EncodingProperties.Bitrate = (uint)(frn * frd * w * h * 4);
                var streamSource = new MediaStreamSource(videoDescriptor);

                TimeSpan spanBuffer = new TimeSpan(0, 0, 0, 0, 250);
                streamSource.BufferTime = spanBuffer;

                streamSource.SampleRequested += StreamSource_SampleRequested;

                streamSource.Closed += (MediaStreamSource sender, MediaStreamSourceClosedEventArgs args) =>
                {
                };
                streamSource.Starting += (MediaStreamSource sender, MediaStreamSourceStartingEventArgs args) =>
                {
                    startedAt = DateTime.Now;
                };
                streamSource.Paused += (MediaStreamSource sender, object args) =>
                {
                };
                streamSource.SwitchStreamsRequested += (MediaStreamSource sender, MediaStreamSourceSwitchStreamsRequestedEventArgs args) =>
                {
                };

                #endregion

                #region
                var tc = new MediaTranscoder();

                //var prof = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
                var temp = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
                var bitrate = temp.Video.Bitrate;
                var prof = new MediaEncodingProfile();
                prof.Container.Subtype = "MPEG4";
                prof.Video.Subtype = "H264";
                prof.Video.Width = w;
                prof.Video.Height = h;
                prof.Video.Bitrate = bitrate;
                prof.Video.FrameRate.Numerator = frd;
                prof.Video.FrameRate.Denominator = 1;
                prof.Video.PixelAspectRatio.Numerator = 1;
                prof.Video.PixelAspectRatio.Denominator = 1;

                var tempFolder = ApplicationData.Current.LocalCacheFolder;
                var file = await tempFolder.CreateFileAsync("out2.mp4", CreationCollisionOption.ReplaceExisting);
                var outputStream = await file.OpenAsync(FileAccessMode.ReadWrite);
                try
                {

                    var result = await tc.PrepareMediaStreamSourceTranscodeAsync(streamSource, outputStream, prof);
                    if (result.CanTranscode)
                    {
                        var op = result.TranscodeAsync();
                        //op.Progress += new AsyncActionProgressHandler<double>(TranscodeProgress);
                        //op.Completed += new AsyncActionWithProgressCompletedHandler<double>(TranscodeComplete);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"error...{e}");
                    throw;
                }
                #endregion


                StartCaptureInternal(item);
            }
        }

        #region
        //public Queue<Direct3D11CaptureFrame> frames = new();
        public Queue<SurfaceWithInfo> frames = new();
        DateTime startedAt = DateTime.Now;
        public bool isRecording = false;
        #endregion

        #region
        private void StreamSource_SampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            //if (isRecording)
            //{
            //Thread.Sleep(TimeSpan.FromMilliseconds(500));
            //StreamSource_SampleRequested(sender, args);
            try
            {
                if (frames.Count == 0)
                {
                    //args.Request.Sample = null;
                    //return;
                    Thread.Sleep(TimeSpan.FromMilliseconds(500));
                    StreamSource_SampleRequested(sender, args);
                }
                var videoFrame = frames.Dequeue();

                if (videoFrame == null)
                {
                    args.Request.Sample = null;
                    return;
                }

                var timestamp = DateTime.Now - startedAt;

                // var samp = MediaStreamSample.CreateFromBuffer(videoFrame.Buffer.AsBuffer(), videoFrame.TimeStamp);
                var samp = MediaStreamSample.CreateFromDirect3D11Surface(videoFrame.Surface, videoFrame.SystemRelativeTime);

                //samp.Processed += OnSampleProcessed;
                args.Request.Sample = samp;
            }
            catch (Exception e)
            {
                args.Request.Sample = null;
                return;
            }
            //}
            //else
            //{
            //    //args.Request.Sample = null;
            //    //return;
            //    Thread.Sleep(TimeSpan.FromMilliseconds(500));
            //    StreamSource_SampleRequested(sender, args);
            //}

        }

        private void OnSampleProcessed(MediaStreamSample sender, object args)
        {

        }
        #endregion

        private async void StartCaptureInternal(GraphicsCaptureItem item)
        {
            // Stop the previous capture if we had one.
            StopCapture();

            _item = item;
            _lastSize = _item.Size;

            isRecording = true;

            _framePool = Direct3D11CaptureFramePool.Create(
               _canvasDevice, // D3D device
               Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, // Pixel format
               1, // Number of frames
               _item.Size); // Size of the buffers

            _framePool.FrameArrived += (s, a) =>
            {
                // The FrameArrived event is raised for every frame on the thread
                // that created the Direct3D11CaptureFramePool. This means we
                // don't have to do a null-check here, as we know we're the only
                // one dequeueing frames in our application.  

                // NOTE: Disposing the frame retires it and returns  
                // the buffer to the pool.

                using (var frame = _framePool.TryGetNextFrame())
                {
                    ProcessFrame(frame);
                }
            };

            _item.Closed += (s, a) =>
            {
                StopCapture();
            };

            _session = _framePool.CreateCaptureSession(_item);
            _session.StartCapture();

        }

        public void StopCapture()
        {
            _session?.Dispose();
            _framePool?.Dispose();
            _item = null;
            _session = null;
            _framePool = null;
        }

        private async void ProcessFrame(Direct3D11CaptureFrame frame)
        {
            // Resize and device-lost leverage the same function on the
            // Direct3D11CaptureFramePool. Refactoring it this way avoids
            // throwing in the catch block below (device creation could always
            // fail) along with ensuring that resize completes successfully and
            // isn’t vulnerable to device-lost.
            bool needsReset = false;
            bool recreateDevice = false;

            if ((frame.ContentSize.Width != _lastSize.Width) ||
                (frame.ContentSize.Height != _lastSize.Height))
            {
                needsReset = true;
                _lastSize = frame.ContentSize;
            }

            try
            {
                // Take the D3D11 surface and draw it into a  
                // Composition surface.

                // Convert our D3D11 surface into a Win2D object.
                CanvasBitmap canvasBitmap = CanvasBitmap.CreateFromDirect3D11Surface(_canvasDevice, frame.Surface);

                Windows.Storage.Streams.IBuffer b;
                var g = canvasBitmap.GetPixelBytes();

                #region
                //var lastSampleTime = frame.SystemRelativeTime;
                //var w = (int)canvasBitmap.Size.Width;
                //var h = (int)canvasBitmap.Size.Height;
                //var c = new VideoFrame(Windows.Graphics.Imaging.BitmapPixelFormat.Rgba8, w, h);

                //var vf = VideoFrame.CreateWithDirect3D11Surface(frame.Surface);

                //var videoFrame = new VideoFrame(canvasBitmap.GetPixelBytes(), videoHead);
                //videoHead = videoHead.Add(TimeSpan.FromMilliseconds(_timeStepMillis));
                frames.Enqueue(new SurfaceWithInfo()
                {
                    Surface = frame.Surface,
                    SystemRelativeTime = frame.SystemRelativeTime
                });
                #endregion

                _currentFrame = canvasBitmap;

                // Helper that handles the drawing for us.
                FillSurfaceWithBitmap(canvasBitmap);
            }

            // This is the device-lost convention for Win2D.
            catch (Exception e) when (_canvasDevice.IsDeviceLost(e.HResult))
            {
                // We lost our graphics device. Recreate it and reset
                // our Direct3D11CaptureFramePool.  
                needsReset = true;
                recreateDevice = true;
            }

            if (needsReset)
            {
                ResetFramePool(frame.ContentSize, recreateDevice);
            }
        }

        private void FillSurfaceWithBitmap(CanvasBitmap canvasBitmap)
        {
            CanvasComposition.Resize(_surface, canvasBitmap.Size);

            using (var session = CanvasComposition.CreateDrawingSession(_surface))
            {
                session.Clear(Colors.Transparent);
                session.DrawImage(canvasBitmap);
            }
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

        private async void Button_ClickAsync(object sender, RoutedEventArgs e)
        {
            await StartCaptureAsync();
        }

        private async void ScreenshotButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            isRecording = false;
            this.frames.Enqueue(null);

            StopCapture();

            await SaveImageAsync(_screenshotFilename, _currentFrame);
        }

        private async Task SaveImageAsync(string filename, CanvasBitmap frame)
        {
            StorageFile file = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(filename, CreationCollisionOption.GenerateUniqueName);

            //StorageFolder pictureFolder = KnownFolders.SavedPictures;

            //StorageFile file = await pictureFolder.CreateFileAsync(
            //    filename,
            //    CreationCollisionOption.ReplaceExisting);

            using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await frame.SaveAsync(fileStream, CanvasBitmapFileFormat.Png, 1f);
            }
        }
    }
}
