using ObjectDetection.WinApp.FrameSourceHelper.FrameSource;
using System;
using System.Threading.Tasks;

namespace ObjectDetection.WinApp.FrameSourceHelper
{
    /// <summary>
    /// IFrameSource factory
    /// </summary>
    public static class FrameSourceFactory
    {
        /// <summary>
        /// Create an IFrameSource from a source object. Currently supports Windows.Storage.StorageFile
        /// and Windows.Media.Capture.MediaCapture source objects.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="failureHandler"></param>
        /// <param name="imageDescriptor"></param>
        /// <returns></returns>
        public static async Task<IFrameSource> CreateFrameSourceAsync(object source, EventHandler<string> failureHandler)
        {
            try
            {
                if (source is Windows.Storage.StorageFile)
                {
                    var sourceFile = source as Windows.Storage.StorageFile;
                    if (sourceFile.ContentType.StartsWith("image"))
                    {
                        return await ImageFileFrameSource.CreateAsync(sourceFile);
                    }
                    else if (sourceFile.ContentType.StartsWith("video"))
                    {
                        //return await MediaPlayerFrameSource.CreateFromStorageFileAsyncTask(sourceFile, imageDescriptor, failureHandler);
                        throw new ArgumentException("Invalid file type received: " + sourceFile.ContentType);
                    }
                    else
                    {
                        throw new ArgumentException("Invalid file type received: " + sourceFile.ContentType);
                    }
                }
                else if (source is Windows.Devices.Enumeration.DeviceInformation)
                {
                    var device = source as Windows.Devices.Enumeration.DeviceInformation;
                    return await DeviceFrameSource.CreateAsync(device, failureHandler);
                }
                else
                {
                    throw new ArgumentException();
                }
            }
            catch (Exception ex)
            {
                failureHandler(null, ex.Message);
            }
            return null;
        }
    }
}
