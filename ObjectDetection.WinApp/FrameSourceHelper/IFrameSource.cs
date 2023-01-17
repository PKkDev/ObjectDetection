using System;
using System.Threading.Tasks;
using Windows.Media;

namespace ObjectDetection.WinApp.FrameSourceHelper
{
    /// <summary>
    /// Standard FrameSource interface to wrap Video, Camera, and any other media streams in
    /// </summary>
    public interface IFrameSource
    {
        FrameSourceType FrameSourceType { get; }
        uint FrameHeight { get; }
        uint FrameWidth { get; }
        double FrameDpi { get; }

        event EventHandler<VideoFrame> FrameArrived;

        Task StartAsync();
        Task StopAsync();
    }
}
