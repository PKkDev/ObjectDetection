using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;

namespace ObjectDetection.WinApp.FrameSourceHelper.FrameSource
{
    public class DeviceFrameSource : IFrameSource, IDisposable
    {
        public FrameSourceType FrameSourceType => FrameSourceType.Camera;

        public uint FrameHeight { get; private set; }
        public uint FrameWidth { get; private set; }
        public double FrameDpi { get; private set; }

        public event EventHandler<VideoFrame> FrameArrived;

        private EventHandler<string> failureHandler;

        private readonly object m_lock = new();

        private MediaCapture mediaCapture;
        private MediaCaptureInitializationSettings mediaCaptureSettings;

        private MediaFrameReader frameReader;
        private MediaFrameSource frameSource;

        public static async Task<DeviceFrameSource> CreateAsync(DeviceInformation device, EventHandler<string> failureHandler)
        {
            var result = new DeviceFrameSource()
            {
                mediaCapture = new MediaCapture(),
                failureHandler = failureHandler,
                mediaCaptureSettings = new MediaCaptureInitializationSettings()
                {
                    VideoDeviceId = device.Id,
                    SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu
                }
            };
            result.mediaCapture.Failed += result.MediaCapture_Failed;

            await result.IntializeFrameSourceAsync();

            return result;
        }

        private DeviceFrameSource() { }

        /// <summary>
        /// Dispose method implementation
        /// </summary>
        public void Dispose()
        {
            lock (m_lock)
            {
                if (frameReader != null)
                {
                    frameReader.FrameArrived -= FrameReader_FrameArrived;
                    frameReader.Dispose();
                    frameReader = null;
                }
                mediaCapture?.Dispose();
                mediaCapture = null;
            }
        }

        /// <summary>
        /// Start frame playback
        /// </summary>
        public async Task StartAsync()
        {
            await frameReader.StartAsync();
        }

        /// <summary>
        /// Stop frame playback
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync()
        {
            await frameReader.StopAsync();
        }

        /// <summary>
        /// Handle the MediaCapture failure event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="errorEventArgs"></param>
        private async void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            // if we failed to initialize MediaCapture ExclusiveControl with MF_E_HW_MFT_FAILED_START_STREAMING,
            // let's retry in SharedReadOnly mode since this points to a camera already in use
            if (mediaCaptureSettings.SharingMode == MediaCaptureSharingMode.ExclusiveControl
                && errorEventArgs.Code == 0xc00d3704) // if device is already in use
            {
                Dispose();
                mediaCapture = new MediaCapture();
                mediaCapture.Failed += MediaCapture_Failed;
                mediaCaptureSettings.SharingMode = MediaCaptureSharingMode.SharedReadOnly;

                await IntializeFrameSourceAsync();
                await StartAsync();
            }
            else
            {
                failureHandler?.Invoke(this, $"Error {errorEventArgs.Code} : {errorEventArgs.Message}");
            }
        }

        /// <summary>
        /// Initialize the frame source and frame reader
        /// </summary>
        /// <returns></returns>
        private async Task IntializeFrameSourceAsync()
        {
            await mediaCapture.InitializeAsync(mediaCaptureSettings);
            await InitializeMediaFrameSourceAsync();
            await InitializeFrameReaderAsync();
        }

        /// <summary>
        /// Initializes MediaCapture's frame source with a compatible format, if possible.
        /// Throws Exception if no compatible stream(s) available
        /// </summary>
        /// <returns></returns>
        private async Task InitializeMediaFrameSourceAsync()
        {
            if (mediaCapture == null)
                return;

            // Get preview or record stream as source
            Func<KeyValuePair<string, MediaFrameSource>, MediaStreamType, bool> filterFrameSources = (source, type) =>
            {
                return (source.Value.Info.MediaStreamType == type && source.Value.Info.SourceKind == MediaFrameSourceKind.Color);
            };
            frameSource = mediaCapture.FrameSources.FirstOrDefault(source => filterFrameSources(source, MediaStreamType.VideoPreview)).Value
                            ?? mediaCapture.FrameSources.FirstOrDefault(source => filterFrameSources(source, MediaStreamType.VideoRecord)).Value;

            // if no preview stream is available, bail
            if (frameSource == null)
            {
                throw new Exception("No preview or record stream available");
            }


            int preferredFrameWidth = 1920;
            int preferredFrameHeight = 1080;
            string preferredMediaEncodingSubtype = MediaEncodingSubtypes.Argb32;

            // If we can, let's attempt to change the format set on the source to our preferences
            if (mediaCaptureSettings.SharingMode == MediaCaptureSharingMode.ExclusiveControl)
            {
                // Filter camera MediaType given frame format preference, and filter out non-compatible subtypes
                var selectedFormat = frameSource.SupportedFormats
                    .Where(format => format.FrameRate.Numerator / format.FrameRate.Denominator > 15 && string.Compare(format.Subtype, preferredMediaEncodingSubtype, true) == 0)?
                    .OrderBy(format => Math.Abs((int)(format.VideoFormat.Width * format.VideoFormat.Height) - (preferredFrameWidth * preferredFrameHeight))).FirstOrDefault();

                // Defer to other supported subtypes if the one prescribed is not supported on the source
                if (selectedFormat == null)
                {
                    selectedFormat = frameSource.SupportedFormats
                        .Where(format => format.FrameRate.Numerator / format.FrameRate.Denominator > 15
                        && (string.Compare(format.Subtype, MediaEncodingSubtypes.Bgra8, true) == 0
                            || string.Compare(format.Subtype, MediaEncodingSubtypes.Nv12, true) == 0
                            || string.Compare(format.Subtype, MediaEncodingSubtypes.Yuy2, true) == 0
                            || string.Compare(format.Subtype, MediaEncodingSubtypes.Rgb32, true) == 0))?
                        .OrderBy(format => Math.Abs((int)(format.VideoFormat.Width * format.VideoFormat.Height) - (preferredFrameWidth * preferredFrameHeight))).FirstOrDefault();
                }
                if (selectedFormat == null)
                {
                    throw new Exception("No compatible formats available");
                }

                await frameSource.SetFormatAsync(selectedFormat);
            }

            FrameWidth = frameSource.CurrentFormat.VideoFormat.Width;
            FrameHeight = frameSource.CurrentFormat.VideoFormat.Height;
            FrameDpi = 0;
        }

        /// <summary>
        /// Initializes MediaFrameReader and registers for MediaCapture callback
        /// </summary>
        /// <returns></returns>
        private async Task InitializeFrameReaderAsync()
        {
            if (frameSource == null)
                return;

            string preferredMediaEncodingSubtype = MediaEncodingSubtypes.Argb32;
            frameReader = await mediaCapture.CreateFrameReaderAsync(frameSource, preferredMediaEncodingSubtype);
            frameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
            frameReader.FrameArrived += FrameReader_FrameArrived; ;
        }

        private void FrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            MediaFrameReference frame = null;
            lock (m_lock)
            {
                try
                {
                    frame = sender.TryAcquireLatestFrame();
                }
                catch (System.ObjectDisposedException)
                {
                    frame = null;
                }
            }
            if (frame != null)
            {
                VideoFrame videoFrame = frame.VideoMediaFrame.GetVideoFrame();
                videoFrame.SystemRelativeTime = frame.SystemRelativeTime;
                FrameArrived?.Invoke(this, videoFrame);
            }
        }
    }
}
