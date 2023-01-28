using System;
using System.Threading.Tasks;
using Windows.Media;

namespace ObjectDetection.WinApp.FrameSourceHelper.FrameSource
{
    public class VideoFrameSource : IFrameSource
    {
        public FrameSourceType FrameSourceType => throw new NotImplementedException();

        public uint FrameHeight => throw new NotImplementedException();

        public uint FrameWidth => throw new NotImplementedException();

        public double FrameDpi => throw new NotImplementedException();

        public event EventHandler<VideoFrame> FrameArrived;

        public Task StartAsync()
        {
            throw new NotImplementedException();
        }

        public Task StopAsync()
        {
            throw new NotImplementedException();
        }
    }
}
