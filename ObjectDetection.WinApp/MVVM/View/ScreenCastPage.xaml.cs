using Microsoft.UI.Xaml.Controls;
using ObjectDetection.WinApp.MVVM.ViewModel;
using System;
using System.Threading.Tasks;

using ObjectDetection.WinApp.DirectXCaptureEncoder;

using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;

using SharpDX.Direct3D11;
using Windows.Media.MediaProperties;
using Windows.Media.Core;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Threading;
using Windows.Graphics.DirectX;
using System.Diagnostics;
using Windows.Graphics.Imaging;

namespace ObjectDetection.WinApp.MVVM.View
{
    public sealed partial class ScreenCastPage : Page
    {
        public ScreenCastViewModel ViewModel { get; set; }

        public ScreenCastPage()
        {
            InitializeComponent();
            ViewModel = App.GetService<ScreenCastViewModel>();

            Task.Run(async () =>
            {
                await SetupEncoding();
            });
        }

        private IDirect3DDevice _device;
        private SharpDX.Direct3D11.Device _sharpDxD3dDevice;

        private GraphicsCaptureItem _captureItem;

        private SharpDX.Direct3D11.Texture2D _composeTexture;
        private RenderTargetView _composeRenderTargetView;

        private MediaEncodingProfile _encodingProfile;

        private VideoStreamDescriptor _videoDescriptor;

        private MediaStreamSource _mediaStreamSource;

        private MediaTranscoder _transcoder;

        private bool _isRecording = false;

        private async Task SetupEncoding()
        {
            // capture is unsupported
            if (!GraphicsCaptureSession.IsSupported())
                return;

            // Create the D3D device and SharpDX device
            if (_device == null)
            {
                _device = Direct3D11Helpers.CreateDevice();
            }
            if (_sharpDxD3dDevice == null)
            {
                _sharpDxD3dDevice = Direct3D11Helpers.CreateSharpDXDevice(_device);
            }

            try
            {
                // Let the user pick an item to capture
                var picker = new GraphicsCapturePicker();

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                _captureItem = await picker.PickSingleItemAsync();

                if (_captureItem == null)
                {
                    return;
                }
                else
                {
                    // Initialize a blank texture and render target view for copying frames, using the same size as the capture item
                    _composeTexture = Direct3D11Helpers.InitializeComposeTexture(_sharpDxD3dDevice, _captureItem.Size);
                    _composeRenderTargetView = new SharpDX.Direct3D11.RenderTargetView(_sharpDxD3dDevice, _composeTexture);

                    // This example encodes video using the item's actual size.
                    var width = (uint)_captureItem.Size.Width;
                    var height = (uint)_captureItem.Size.Height;

                    // Make sure the dimensions are are even. Required by some encoders.
                    width = (width % 2 == 0) ? width : width + 1;
                    height = (height % 2 == 0) ? height : height + 1;

                    var temp = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
                    var bitrate = temp.Video.Bitrate;
                    uint framerate = 30;

                    _encodingProfile = new MediaEncodingProfile();
                    _encodingProfile.Container.Subtype = "MPEG4";
                    _encodingProfile.Video.Subtype = "H264";
                    _encodingProfile.Video.Width = width;
                    _encodingProfile.Video.Height = height;
                    _encodingProfile.Video.Bitrate = bitrate;
                    _encodingProfile.Video.FrameRate.Numerator = framerate;
                    _encodingProfile.Video.FrameRate.Denominator = 1;
                    _encodingProfile.Video.PixelAspectRatio.Numerator = 1;
                    _encodingProfile.Video.PixelAspectRatio.Denominator = 1;

                    var videoProperties = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, width, height);
                    _videoDescriptor = new VideoStreamDescriptor(videoProperties);

                    // Create our MediaStreamSource
                    _mediaStreamSource = new MediaStreamSource(_videoDescriptor);
                    _mediaStreamSource.BufferTime = TimeSpan.FromSeconds(0);
                    _mediaStreamSource.Starting += OnMediaStreamSourceStarting;
                    _mediaStreamSource.SampleRequested += OnMediaStreamSourceSampleRequested;

                    // Create our transcoder
                    _transcoder = new MediaTranscoder();
                    _transcoder.HardwareAccelerationEnabled = true;

                    // Create a destination file - Access to the VideosLibrary requires the "Videos Library" capability
                    var name = DateTime.Now.ToString("yyyyMMdd-HHmm-ss");

                    StorageFile savedImage = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync($"{name}.mp4", CreationCollisionOption.ReplaceExisting);
                    using IRandomAccessStream stream = await savedImage.OpenAsync(FileAccessMode.ReadWrite);
                   
                    //var folder = KnownFolders.VideosLibrary;
                    //var file = await folder.CreateFileAsync($"{name}.mp4");
                    //using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);

                    await EncodeAsync(stream);
                }
            }
            catch (Exception ex)
            {

            }
        }

        private void OnMediaStreamSourceSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            //&& !_closed
            if (_isRecording)
            {
                try
                {
                    using (var frame = WaitForNewFrame())
                    {
                        if (frame == null)
                        {
                            args.Request.Sample = null;
                            Stop();
                            Cleanup();
                            return;
                        }

                        var timeStamp = frame.SystemRelativeTime;

                        var sample = MediaStreamSample.CreateFromDirect3D11Surface(frame.Surface, timeStamp);
                        args.Request.Sample = sample;
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    Debug.WriteLine(e.StackTrace);
                    Debug.WriteLine(e);
                    args.Request.Sample = null;
                    Stop();
                    Cleanup();
                }
            }
            else
            {
                args.Request.Sample = null;
                Stop();
                Cleanup();
            }
        }

        private void OnMediaStreamSourceStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            using (var frame = WaitForNewFrame())
            {
                args.Request.SetActualStartPosition(frame.SystemRelativeTime);
            }
        }

        public SurfaceWithInfo WaitForNewFrame()
        {
            // Let's get a fresh one.
            _currentFrame?.Dispose();
            _frameEvent.Reset();

            var signaledEvent = _events[WaitHandle.WaitAny(_events)];
            if (signaledEvent == _closedEvent)
            {
                Cleanup();
                return null;
            }

            var result = new SurfaceWithInfo();
            result.SystemRelativeTime = _currentFrame.SystemRelativeTime;
            using (var multithreadLock = new MultithreadLock(_multithread))
            using (var sourceTexture = Direct3D11Helpers.CreateSharpDXTexture2D(_currentFrame.Surface))
            {

                _sharpDxD3dDevice.ImmediateContext.ClearRenderTargetView(_composeRenderTargetView, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 1));

                var width = Math.Clamp(_currentFrame.ContentSize.Width, 0, _currentFrame.Surface.Description.Width);
                var height = Math.Clamp(_currentFrame.ContentSize.Height, 0, _currentFrame.Surface.Description.Height);
                var region = new SharpDX.Direct3D11.ResourceRegion(0, 0, 0, width, height, 1);
                _sharpDxD3dDevice.ImmediateContext.CopySubresourceRegion(sourceTexture, 0, region, _composeTexture, 0);

                var description = sourceTexture.Description;
                description.Usage = SharpDX.Direct3D11.ResourceUsage.Default;
                description.BindFlags = SharpDX.Direct3D11.BindFlags.ShaderResource | SharpDX.Direct3D11.BindFlags.RenderTarget;
                description.CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.None;
                description.OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None;

                using (var copyTexture = new SharpDX.Direct3D11.Texture2D(_sharpDxD3dDevice, description))
                {
                    _sharpDxD3dDevice.ImmediateContext.CopyResource(_composeTexture, copyTexture);
                    result.Surface = Direct3D11Helpers.CreateDirect3DSurfaceFromSharpDXTexture(copyTexture);
                }
            }

            return result;
        }

        private async Task EncodeAsync(IRandomAccessStream stream)
        {
            if (!_isRecording)
            {
                _isRecording = true;

                StartCapture();

                var transcode = await _transcoder.PrepareMediaStreamSourceTranscodeAsync(_mediaStreamSource, stream, _encodingProfile);

                await transcode.TranscodeAsync();
            }
        }

        private Multithread _multithread;
        private ManualResetEvent _frameEvent;
        private ManualResetEvent _closedEvent;
        private ManualResetEvent[] _events;
        private Direct3D11CaptureFramePool _framePool;
        private GraphicsCaptureSession _session;

        public void StartCapture()
        {

            _multithread = _sharpDxD3dDevice.QueryInterface<SharpDX.Direct3D11.Multithread>();
            _multithread.SetMultithreadProtected(true);
            _frameEvent = new ManualResetEvent(false);
            _closedEvent = new ManualResetEvent(false);
            _events = new[] { _closedEvent, _frameEvent };

            _captureItem.Closed += OnClosed;

            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                _captureItem.Size);
            _framePool.FrameArrived += OnFrameArrived;
            _session = _framePool.CreateCaptureSession(_captureItem);
            _session.StartCapture();
        }

        private Direct3D11CaptureFrame _currentFrame;

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            _currentFrame = sender.TryGetNextFrame();
            _frameEvent.Set();
        }

        private void OnClosed(GraphicsCaptureItem sender, object args)
        {
            _closedEvent.Set();
        }

        private void Cleanup()
        {
            _framePool?.Dispose();
            _session?.Dispose();
            if (_captureItem != null)
            {
                _captureItem.Closed -= OnClosed;
            }
            _captureItem = null;
            _device = null;
            _sharpDxD3dDevice = null;
            _composeTexture?.Dispose();
            _composeTexture = null;
            _composeRenderTargetView?.Dispose();
            _composeRenderTargetView = null;
            _currentFrame?.Dispose();
        }

        private void Stop()
        {
            _closedEvent.Set();
        }





    }
}