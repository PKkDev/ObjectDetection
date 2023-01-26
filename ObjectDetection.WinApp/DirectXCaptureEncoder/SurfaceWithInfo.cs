using System;
using Windows.Devices.PointOfService;
using Windows.Graphics.DirectX.Direct3D11;

namespace ObjectDetection.WinApp.DirectXCaptureEncoder
{
    public sealed class SurfaceWithInfo : IDisposable
    {
        public IDirect3DSurface Surface { get; internal set; }
        public TimeSpan SystemRelativeTime { get; internal set; }

        public void Dispose()
        {
            Surface?.Dispose();
            Surface = null;
        }
    }

    public static class CaptureSettings
    {

        public static SizeUInt32[] Resolutions => new SizeUInt32[]
        {
            new SizeUInt32() { Width = 1280, Height = 720 },
            new SizeUInt32() { Width = 1920, Height = 1080 },
            new SizeUInt32() { Width = 3840, Height = 2160 },
            new SizeUInt32() { Width = 7680, Height = 4320 }
        };

        public static uint[] Bitrates => new uint[] { 9000000, 18000000, 36000000, 72000000 };

        public static uint[] FrameRates => new uint[] { 24, 30, 60 };
    }
}
