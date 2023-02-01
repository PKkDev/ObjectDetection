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
//
using System.Linq;
//
using NAudio.Wave;
using Windows.Media.Editing;
using NAudio.Wave.SampleProviders;
//
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json.Linq;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.PointOfService;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Graphics.Canvas.Effects;

namespace ObjectDetection.WinApp.MVVM.View
{
    public sealed partial class ScreenCapturePage : Page
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
        public Queue<IDirect3DSurface> framesBytes = new();
        public bool _isRecording = false;
        #endregion

        //private LowLagMediaRecording MediaRecording;

        private WasapiLoopbackCapture Capture = null;
        private WaveFileWriter Writer = null;

        StorageFile fileAudio;
        StorageFile fileVideo;


        HubConnection connection;

        public ScreenCapturePage()
        {
            InitializeComponent();

            Task t = Task.Run(async () =>
            {
                //StorageFile file1 = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync("NAudio.mp3", CreationCollisionOption.GenerateUniqueName);
                //Capture = new WasapiLoopbackCapture();
                //var writer = new WaveFileWriter(file1.Path, Capture.WaveFormat);

                //Capture.DataAvailable += (s, a) =>
                //{
                //    writer.Write(a.Buffer, 0, a.BytesRecorded);
                //    //if (writer.Position > Capture.WaveFormat.AverageBytesPerSecond * 20)
                //    //{
                //    //    Capture.StopRecording();
                //    //}
                //};

                //Capture.RecordingStopped += (s, a) =>
                //{
                //    writer.Dispose();
                //    writer = null;
                //    Capture.Dispose();
                //};

                //Capture.StartRecording();





                //var devicePicker = new DevicePicker();
                //devicePicker.Filter.SupportedDeviceClasses.Add(DeviceClass.AudioRender);
                //var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                //WinRT.Interop.InitializeWithWindow.Initialize(devicePicker, hwnd);
                //Windows.Foundation.Rect rect = new Windows.Foundation.Rect(new Windows.Foundation.Point(0, 0), new Windows.Foundation.Point(0, 0));
                //DeviceInformation di = await devicePicker.PickSingleDeviceAsync(rect);

                //MediaStreamSource.

                //var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();
                //MediaFrameSourceGroup selectedFrameSourceGroup = frameSourceGroups[0];
                //MediaFrameSourceInfo frameSourceInfo = selectedFrameSourceGroup.SourceInfos[0];




                //string audioCaptureSelector = MediaDevice.GetAudioCaptureSelector();
                //var audioCapture = await DeviceInformation.FindAllAsync(audioCaptureSelector);

                //string audioRenderSelector = MediaDevice.GetAudioRenderSelector();
                //var audioRender = await DeviceInformation.FindAllAsync(audioRenderSelector);

                //string b = MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Communications);
                //DeviceInformation b1 = await DeviceInformation.CreateFromIdAsync(b);

                //string a = MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default);
                //DeviceInformation a1 = await DeviceInformation.CreateFromIdAsync(a);

                //var mediaCapture = new MediaCapture();
                //var settings = new MediaCaptureInitializationSettings
                //{
                //    AudioDeviceId = b1.Id,
                //    //SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                //    StreamingCaptureMode = StreamingCaptureMode.Audio,
                //    //MemoryPreference = MediaCaptureMemoryPreference.Cpu
                //};
                //await mediaCapture.InitializeAsync(settings);

                //mediaCapture.RecordLimitationExceeded += (MediaCapture sender) => { };
                //mediaCapture.Failed += (MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs) =>
                //{
                //};

                //var localFolder = ApplicationData.Current.LocalCacheFolder;
                //StorageFile file = await localFolder.CreateFileAsync("audio.mp3", CreationCollisionOption.GenerateUniqueName);
                //MediaRecording = await mediaCapture.PrepareLowLagRecordToStorageFileAsync(MediaEncodingProfile.CreateMp3(AudioEncodingQuality.High), file);
                //await MediaRecording.StartAsync();
            });
            Task.WaitAll(t);

            connection = new HubConnectionBuilder()
                .WithUrl("https://localhost:7139/video", Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets)
                .Build();

            connection.Closed += async (error) =>
            {
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await connection.StartAsync();
            };

            Task t1 = Task.Run(async () =>
            {
                await connection.StartAsync();
            });
            t1.Wait();

            Task t2 = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        while (framesBytes.Count == 0) { }

                        var bytes = framesBytes.Dequeue();

                        SoftwareBitmap softwareBitmap = null;
                        softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(bytes);
                        if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                            softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                        float newWidth = softwareBitmap.PixelWidth / 5;
                        float newHeight = softwareBitmap.PixelHeight / 5;
                        using (var resourceCreator = CanvasDevice.GetSharedDevice())
                        using (var canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(resourceCreator, softwareBitmap))
                        using (var canvasRenderTarget = new CanvasRenderTarget(resourceCreator, newWidth, newHeight, canvasBitmap.Dpi))
                        using (var drawingSession = canvasRenderTarget.CreateDrawingSession())
                        using (var scaleEffect = new ScaleEffect())
                        {
                            scaleEffect.Source = canvasBitmap;
                            scaleEffect.Scale = new System.Numerics.Vector2(newWidth / softwareBitmap.PixelWidth, newHeight / softwareBitmap.PixelHeight);
                            drawingSession.DrawImage(scaleEffect);
                            drawingSession.Flush();

                            var pixels = canvasRenderTarget.GetPixelBytes();
                            var c = pixels.Count(x => x != 0);

                            if (pixels.Any())
                                await connection.InvokeAsync("UploadStream", pixels);

                            //var news = SoftwareBitmap.CreateCopyFromBuffer(canvasRenderTarget.GetPixelBytes().AsBuffer(), BitmapPixelFormat.Bgra8, (int)newWidth, (int)newHeight, BitmapAlphaMode.Premultiplied);
                        }

                        //var resourceCreator = CanvasDevice.GetSharedDevice();
                        //var canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(resourceCreator, softwareBitmap);
                        //var canvasRenderTarget = new CanvasRenderTarget(resourceCreator, (int)(softwareBitmap.PixelWidth), (int)(softwareBitmap.PixelHeight), 96);

                        //using var cds = canvasRenderTarget.CreateDrawingSession();
                        //cds.DrawImage(canvasBitmap, canvasRenderTarget.Bounds);

                        //var pixels = canvasRenderTarget.GetPixelBytes();
                        //var c = pixels.Count(x => x != 0);

                        //if (pixels.Any())
                        //    await connection.InvokeAsync("UploadStream", pixels);

                        softwareBitmap?.Dispose();
                    }
                }
                catch (Exception ex)
                {

                }
            });

            Setup();
        }

        // Locks
        private SemaphoreSlim m_lock = new SemaphoreSlim(1);
        byte[] bytes;

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

            _framePool.FrameArrived += (Direct3D11CaptureFramePool sender, object args) =>
            {
                using Direct3D11CaptureFrame frame = _framePool.TryGetNextFrame();
                ProcessFrame(frame);
            };

            _captureItem.Closed += (GraphicsCaptureItem sender, object args) =>
            {
                StopCapture();
            };

            _session = _framePool.CreateCaptureSession(_captureItem);
            _session.StartCapture();
        }

        private void ProcessFrame(Direct3D11CaptureFrame frame)
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

                #region FillSurfaceWithBitmap

                CanvasComposition.Resize(_surface, canvasBitmap.Size);

                using var session = CanvasComposition.CreateDrawingSession(_surface);
                session.Clear(Colors.Transparent);
                session.DrawImage(canvasBitmap);

                #endregion FillSurfaceWithBitmap

                //var canvasRenderTarget = new CanvasRenderTarget(_canvasDevice, (int)(canvasBitmap.Bounds.Width / 5), (int)(canvasBitmap.Bounds.Height / 5), canvasBitmap.Dpi);
                //using var cds = canvasRenderTarget.CreateDrawingSession();
                //cds.DrawImage(canvasBitmap, canvasRenderTarget.Bounds);
                //var pixelBytes = canvasRenderTarget.GetPixelBytes();
                //var count = pixelBytes.Count(x => x != 0);
                //framesBytes.Enqueue(pixelBytes);

                framesBytes.Enqueue(frame.Surface);

                if (_isRecording)
                {
                    frames.Enqueue(new SurfaceWithInfo()
                    {
                        Surface = frame.Surface,
                        SystemRelativeTime = frame.SystemRelativeTime,
                    });
                }
            }
            catch (Exception e) when (_canvasDevice.IsDeviceLost(e.HResult))
            {
                needsReset = true;
                recreateDevice = true;
            }

            if (needsReset)
                ResetFramePool(frame.ContentSize, recreateDevice);
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
                var nowDate = $"{DateTime.Now:yyyyMMdd-HHmm-ss}";

                #region

                fileAudio = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync($"{nowDate}.mp3", CreationCollisionOption.GenerateUniqueName);
                Capture = new WasapiLoopbackCapture();
                Writer = new WaveFileWriter(fileAudio.Path, Capture.WaveFormat);

                Capture.DataAvailable += (s, a) =>
                {
                    Writer.Write(a.Buffer, 0, a.BytesRecorded);
                    //if (writer.Position > Capture.WaveFormat.AverageBytesPerSecond * 20)
                    //{ Capture.StopRecording(); }
                };

                Capture.RecordingStopped += (s, a) =>
                {
                    Writer?.Dispose();
                    Writer = null;
                    Capture?.Dispose();
                    Capture = null;
                };

                #endregion

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
                    while (frames.Count == 0) { }
                    var videoFrame = frames.Dequeue();
                    args.Request.SetActualStartPosition(videoFrame.SystemRelativeTime);
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
                fileVideo = await tempFolder.CreateFileAsync($"{nowDate}.mp4", CreationCollisionOption.ReplaceExisting);
                var outputStream = await fileVideo.OpenAsync(FileAccessMode.ReadWrite);
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
                    //await Task.Delay((int)TimeSpan.FromSeconds(5).TotalMilliseconds);
                    var op = transcode.TranscodeAsync();
                    //op.Progress += new AsyncActionProgressHandler<double>(TranscodeProgress);
                    //op.Completed += new AsyncActionWithProgressCompletedHandler<double>(TranscodeComplete);


                    //var sine20Seconds = new SignalGenerator()
                    //{ Gain = 0.2, Frequency = 500, Type = SignalGeneratorType.Sin }
                    //.Take(TimeSpan.FromSeconds(2));
                    //using var wo = new WaveOutEvent();
                    //wo.Init(sine20Seconds);
                    //wo.Play();

                    //Capture.StartRecording();

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
                //if (_isRecording)
                //{
                //    while (frames.Count == 0) { }
                //    lock (frames)
                //    {
                //        var videoFrame = frames.Dequeue();
                //        var samp = MediaStreamSample.CreateFromDirect3D11Surface(videoFrame.Surface, videoFrame.SystemRelativeTime);
                //        args.Request.Sample = samp;
                //    }
                //}
                //else
                //{
                //    args.Request.Sample = null;
                //    StopCapture();
                //}


                while (frames.Count == 0) { }
                //if (frames.Count == 0)
                //{
                //    Thread.Sleep(TimeSpan.FromMilliseconds(50));
                //    StreamSource_SampleRequested(sender, args);
                //}
                SurfaceWithInfo videoFrame = frames.Dequeue();

                if (videoFrame == null)
                {
                    Capture?.StopRecording();
                    args.Request.Sample = null;
                    return;
                }

                //Task t = Task.Run(async () =>
                //{
                //    SoftwareBitmap softwareBitmap = null;
                //    try
                //    {
                //        //var memoryRandomAccessStream = new InMemoryRandomAccessStream();
                //        //var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, memoryRandomAccessStream);
                //        softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(videoFrame.Surface);
                //        if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                //            softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                //        var canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(_canvasDevice, softwareBitmap);
                //        //var pixelBytes1 = canvasBitmap.GetPixelBytes();
                //        var canvasRenderTarget = new CanvasRenderTarget(_canvasDevice, (int)(softwareBitmap.PixelWidth / 10), (int)(softwareBitmap.PixelHeight / 10), 96);

                //        using var cds = canvasRenderTarget.CreateDrawingSession();
                //        cds.DrawImage(canvasBitmap, canvasRenderTarget.Bounds);

                //        var pixelBytes = canvasRenderTarget.GetPixelBytes();


                //        //var writeableBitmap = new WriteableBitmap((int)(softwareBitmap.PixelWidth * 0.5), (int)(softwareBitmap.PixelHeight * 0.5));
                //        //using var stream = writeableBitmap.PixelBuffer.AsStream();

                //        //await stream.WriteAsync(pixelBytes, 0, pixelBytes.Length);


                //        //var scaledSoftwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, (int)(softwareBitmap.PixelWidth * 0.5), (int)(softwareBitmap.PixelHeight * 0.5));
                //        //scaledSoftwareBitmap.CopyFromBuffer(writeableBitmap.PixelBuffer);

                //        //if (pixelBytes.Any())
                //        //    await connection.InvokeAsync("UploadStream", pixelBytes);

                //    }
                //    catch (Exception e)
                //    {

                //    }
                //    finally
                //    {
                //        softwareBitmap?.Dispose();
                //    }

                //    // encoder.SetSoftwareBitmap(softwareBitmap);
                //    // encoder.IsThumbnailGenerated = false;
                //    // await encoder.FlushAsync();

                //    try
                //    {
                //        //bytes = new byte[memoryRandomAccessStream.Size];
                //        //await memoryRandomAccessStream.ReadAsync(bytes.AsBuffer(), (uint)memoryRandomAccessStream.Size, InputStreamOptions.None);

                //        //if (videoFrame.Bytes.Any())
                //        //    await connection.InvokeAsync("UploadStream", videoFrame.Bytes);
                //    }
                //    catch (Exception ex)
                //    {
                //        System.Diagnostics.Debug.WriteLine(ex.Message);
                //    }
                //    finally
                //    {
                //        //softwareBitmap?.Dispose();
                //        //memoryRandomAccessStream?.Dispose();
                //    }
                //});

                var samp = MediaStreamSample.CreateFromDirect3D11Surface(videoFrame.Surface, videoFrame.SystemRelativeTime);
                //var samp = MediaStreamSample.CreateFromDirect3D11Surface(videoFrame.Surface, timestamp);

                samp.Processed += (MediaStreamSample sender, object args) => { };
                args.Request.Sample = samp;
            }
            catch (Exception ex)
            {
                Capture?.StopRecording();
                args.Request.Sample = null;
                return;
            }
        }

        private async void Click_StopCapture(object sender, RoutedEventArgs e)
        {
            //await MediaRecording.StopAsync();

            _isRecording = false;
            frames.Enqueue(null);

            StopCapture();

            //await SaveToUnionFile();
        }

        private async void Click_TakeScreenShot(object sender, RoutedEventArgs e)
        {
            await SaveImageAsync();
        }

        private async Task SaveToUnionFile()
        {
            if (fileAudio != null && fileVideo != null)
            {
                var fileUnionName = $"{DateTime.Now:yyyyMMdd-HHmm-ss}_unioun.mp4";
                var fileUnion = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(fileUnionName, CreationCollisionOption.GenerateUniqueName);

                MediaComposition muxedStream = new MediaComposition();

                BackgroundAudioTrack audioTrack = await BackgroundAudioTrack.CreateFromFileAsync(fileAudio);
                MediaClip videoTrack = await MediaClip.CreateFromFileAsync(fileVideo);

                muxedStream.BackgroundAudioTracks.Add(audioTrack);
                muxedStream.Clips.Add(videoTrack);

                await muxedStream.RenderToFileAsync(fileUnion, MediaTrimmingPreference.Precise);

                //MediaStreamSource mss = muxedStream.GenerateMediaStreamSource();
                //mpElement.Source = MediaSource.CreateFromMediaStreamSource(mss);
            }
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
            //if (_captureItem != null)
            //    _captureItem.Closed -= OnCaptureItemClosed;
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