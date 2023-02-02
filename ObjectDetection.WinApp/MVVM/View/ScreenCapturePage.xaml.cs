using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

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
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Windows.Media.Editing;
//
using Microsoft.AspNetCore.SignalR.Client;
using Windows.Graphics.Imaging;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Devices.Enumeration;
using Windows.Media.Capture.Frames;
using Windows.Media.Devices;
using Windows.Media.Capture;

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
        public Queue<SurfaceWithInfo> framesToSave = new();
        public Queue<IDirect3DSurface> framesToSend = new();
        public bool _isRecording = false;
        #endregion

        private MediaCapture mediaCapture;
        private MediaCaptureInitializationSettings mediaCaptureSettings;
        private LowLagMediaRecording MediaRecording;

        private WasapiLoopbackCapture Capture = null;
        private WaveFileWriter Writer = null;

        StorageFile filePCAudio;
        StorageFile fileMicroAudio;
        StorageFile fileVideo;


        HubConnection connection;

        public ScreenCapturePage()
        {
            InitializeComponent();

            Task t = Task.Run(async () =>
            {
                //var devicePicker = new DevicePicker();
                //devicePicker.Filter.SupportedDeviceClasses.Add(DeviceClass.AudioRender);
                //devicePicker.Filter.SupportedDeviceClasses.Add(DeviceClass.AudioCapture);
                //var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                //WinRT.Interop.InitializeWithWindow.Initialize(devicePicker, hwnd);
                //Windows.Foundation.Rect rect = new Windows.Foundation.Rect(new Windows.Foundation.Point(0, 0), new Windows.Foundation.Point(0, 0));
                //DeviceInformation di = await devicePicker.PickSingleDeviceAsync(rect);

                // //camera
                //var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();
                //MediaFrameSourceGroup selectedFrameSourceGroup = frameSourceGroups[0];
                //MediaFrameSourceInfo frameSourceInfo = selectedFrameSourceGroup.SourceInfos[0];

                #region micro

                //string audioCaptureSelector = MediaDevice.GetAudioCaptureSelector();
                //var audioCapture = await DeviceInformation.FindAllAsync(audioCaptureSelector);

                //string b = MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Communications);
                //DeviceInformation b1 = await DeviceInformation.CreateFromIdAsync(b);

                #endregion micro

                #region динамики

                //string audioRenderSelector = MediaDevice.GetAudioRenderSelector();
                //var audioRender = await DeviceInformation.FindAllAsync(audioRenderSelector);

                //string a = MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default);
                //DeviceInformation a1 = await DeviceInformation.CreateFromIdAsync(a);

                #endregion динамики

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
                ////MediaRecording = await mediaCapture.PrepareLowLagRecordToStorageFileAsync(MediaEncodingProfile.CreateMp3(AudioEncodingQuality.High), file);
                ////await MediaRecording.StartAsync();
            });
            Task.WaitAll(t);

            #region signalR connect

            //connection = new HubConnectionBuilder()
            //    .WithUrl("https://localhost:7139/video")
            //    .Build();

            //connection.Closed += async (error) =>
            //{
            //    await Task.Delay(TimeSpan.FromSeconds(5));
            //    await connection.StartAsync();
            //};

            //try
            //{
            //    Task startSignalTask = Task.Run(async () => await connection.StartAsync());
            //    startSignalTask.Wait();
            //}
            //catch (Exception e)
            //{

            //}

            //Task sendSignalTask = Task.Run(async () =>
            //{
            //    try
            //    {
            //        while (true)
            //        {
            //            while (framesToSend.Count == 0) { }

            //            var bytes = framesToSend.Dequeue();

            //            SoftwareBitmap softwareBitmap = null;
            //            softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(bytes);
            //            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            //                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            //            float newWidth = softwareBitmap.PixelWidth / 5;
            //            float newHeight = softwareBitmap.PixelHeight / 5;
            //            using var resourceCreator = CanvasDevice.GetSharedDevice();
            //            using var canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(resourceCreator, softwareBitmap);
            //            using CanvasRenderTarget canvasRenderTarget = new(resourceCreator, newWidth, newHeight, canvasBitmap.Dpi);
            //            using var drawingSession = canvasRenderTarget.CreateDrawingSession();
            //            using ScaleEffect scaleEffect = new();

            //            scaleEffect.Source = canvasBitmap;
            //            scaleEffect.Scale = new System.Numerics.Vector2(newWidth / softwareBitmap.PixelWidth, newHeight / softwareBitmap.PixelHeight);
            //            drawingSession.DrawImage(scaleEffect);
            //            drawingSession.Flush();

            //            var pixels = canvasRenderTarget.GetPixelBytes();
            //            var c = pixels.Count(x => x != 0);

            //            if (pixels.Any())
            //                await connection.InvokeAsync("UploadStream", pixels);

            //            //var news = SoftwareBitmap.CreateCopyFromBuffer(canvasRenderTarget.GetPixelBytes().AsBuffer(), BitmapPixelFormat.Bgra8, (int)newWidth, (int)newHeight, BitmapAlphaMode.Premultiplied);

            //            softwareBitmap?.Dispose();
            //        }
            //    }
            //    catch (Exception ex)
            //    {

            //    }
            //});

            #endregion signalR connect

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

                if (_isRecording)
                {
                    //framesToSend.Enqueue(frame.Surface);

                    framesToSave.Enqueue(new SurfaceWithInfo()
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

                #region capture PC audio

                filePCAudio = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync($"{nowDate}_pc.mp3", CreationCollisionOption.GenerateUniqueName);
                Capture = new WasapiLoopbackCapture();
                Writer = new WaveFileWriter(filePCAudio.Path, Capture.WaveFormat);

                Capture.DataAvailable += (s, a) =>
                {
                    Writer.Write(a.Buffer, 0, a.BytesRecorded);
                };

                Capture.RecordingStopped += (s, a) =>
                {
                    Writer?.Dispose();
                    Writer = null;
                    Capture?.Dispose();
                    Capture = null;
                };

                #endregion capture PC audio

                #region capture microphone

                fileMicroAudio = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync($"{nowDate}_micro.mp3", CreationCollisionOption.GenerateUniqueName);

                string defaultAudioCaptureId = MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Communications);
                DeviceInformation defaultAudioCapture = await DeviceInformation.CreateFromIdAsync(defaultAudioCaptureId);

                mediaCapture = new MediaCapture();
                mediaCaptureSettings = new MediaCaptureInitializationSettings
                {
                    AudioDeviceId = defaultAudioCapture.Id,
                    SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                    StreamingCaptureMode = StreamingCaptureMode.Audio,
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu
                };
                await mediaCapture.InitializeAsync(mediaCaptureSettings);

                mediaCapture.RecordLimitationExceeded += (MediaCapture sender) => { };
                mediaCapture.Failed += (MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs) =>
                {
                };

                MediaRecording = await mediaCapture.PrepareLowLagRecordToStorageFileAsync(
                    MediaEncodingProfile.CreateMp3(AudioEncodingQuality.High), fileMicroAudio);

                #endregion capture microphone

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
                    while (framesToSave.Count == 0) { }
                    var videoFrame = framesToSave.Dequeue();
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
                fileVideo = await tempFolder.CreateFileAsync($"{nowDate}_video_capture.mp4", CreationCollisionOption.ReplaceExisting);
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

                    var sine20Seconds = new SignalGenerator() { Gain = 0.2, Frequency = 500, Type = SignalGeneratorType.Sin }
                    .Take(TimeSpan.FromSeconds(2));
                    using var wo = new WaveOutEvent();
                    wo.Init(sine20Seconds);
                    wo.Play();

                    Capture.StartRecording();

                    await MediaRecording.StartAsync();

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


                while (framesToSave.Count == 0) { }
                //if (frames.Count == 0)
                //{
                //    Thread.Sleep(TimeSpan.FromMilliseconds(50));
                //    StreamSource_SampleRequested(sender, args);
                //}
                SurfaceWithInfo videoFrame = framesToSave.Dequeue();

                if (videoFrame == null)
                {
                    Capture?.StopRecording();
                    args.Request.Sample = null;
                    return;
                }

                var samp = MediaStreamSample.CreateFromDirect3D11Surface(videoFrame.Surface, videoFrame.SystemRelativeTime);

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
            if (MediaRecording != null)
                await MediaRecording.FinishAsync();

            _isRecording = false;
            framesToSave.Enqueue(null);

            StopCapture();

            await Task.Delay(TimeSpan.FromSeconds(5));
            await SaveToUnionFile();
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
            await canvasBitmap.SaveAsync(fileStream, CanvasBitmapFileFormat.Jpeg, 1f);
        }

        private async Task SaveToUnionFile()
        {
            if (filePCAudio != null && fileVideo != null && fileMicroAudio != null)
            {
                var fileUnionName = $"{DateTime.Now:yyyyMMdd-HHmm-ss}_unioun.mp4";
                var fileUnion = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync(fileUnionName, CreationCollisionOption.GenerateUniqueName);

                MediaComposition muxedStream = new MediaComposition();

                BackgroundAudioTrack pcAudioTrack = await BackgroundAudioTrack.CreateFromFileAsync(filePCAudio);
                BackgroundAudioTrack microAudioTrack = await BackgroundAudioTrack.CreateFromFileAsync(fileMicroAudio);
                MediaClip videoTrack = await MediaClip.CreateFromFileAsync(fileVideo);

                muxedStream.BackgroundAudioTracks.Add(pcAudioTrack);
                muxedStream.BackgroundAudioTracks.Add(microAudioTrack);
                muxedStream.Clips.Add(videoTrack);

                await muxedStream.RenderToFileAsync(fileUnion, MediaTrimmingPreference.Precise);

                //MediaStreamSource mss = muxedStream.GenerateMediaStreamSource();
                //mpElement.Source = MediaSource.CreateFromMediaStreamSource(mss);
            }
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
