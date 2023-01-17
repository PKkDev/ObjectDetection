using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ObjectDetection.WinApp.FrameSourceHelper.FrameSource
{
    public class ImageFileFrameSource : IFrameSource
    {
        public FrameSourceType FrameSourceType => FrameSourceType.Photo;

        public uint FrameHeight { get; private set; }
        public uint FrameWidth { get; private set; }
        public double FrameDpi { get; private set; }

        public event EventHandler<VideoFrame> FrameArrived;

        private VideoFrame m_videoFrame;

        public static async Task<ImageFileFrameSource> CreateAsync(StorageFile file)
        {
            var result = new ImageFileFrameSource();
            await result.GetFrameFromFileAsync(file);
            return result;
        }

        private async Task GetFrameFromFileAsync(StorageFile file)
        {
            //var savedImageProps = await file.Properties.GetImagePropertiesAsync();

            //BitmapDecoder decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.JpegDecoderId, stream);
            //var imageDpi = Math.Max(decoder.DpiX, decoder.DpiY);

            // Decoding image file content into a SoftwareBitmap, and wrap into VideoFrame
            SoftwareBitmap softwareBitmap = null;
            using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);

            // Create the decoder from the stream 
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

            // Get the SoftwareBitmap representation of the file in BGRA8 format
            softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            // Convert to preferred format if specified and encapsulate the image in a VideoFrame instance
            var convertedSoftwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
            m_videoFrame = VideoFrame.CreateWithSoftwareBitmap(convertedSoftwareBitmap);

            // Extract frame dimensions
            FrameDpi = Math.Max(softwareBitmap.DpiX, softwareBitmap.DpiY);
            FrameWidth = (uint)softwareBitmap.PixelWidth;
            FrameHeight = (uint)softwareBitmap.PixelHeight;
        }

        public Task StartAsync()
        {
            FrameArrived?.Invoke(this, m_videoFrame);

            // Async not needed, return success
            return Task.FromResult(true);
        }

        public Task StopAsync()
        {
            // no-op, return success
            return Task.FromResult(true);
        }
    }
}
