using System;

namespace ObjectDetection.WinApp.DirectXCaptureEncoder
{
    public class MultithreadLock : IDisposable
    {
        public MultithreadLock(SharpDX.Direct3D11.Multithread multithread)
        {
            _multithread = multithread;
            _multithread?.Enter();
        }

        public void Dispose()
        {
            _multithread?.Leave();
            _multithread = null;
        }

        private SharpDX.Direct3D11.Multithread _multithread;
    }
}
