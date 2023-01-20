using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using ObjectDetection.WinApp.FrameSourceHelper;
using ObjectDetection.WinApp.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture.Frames;
using YOLO4.Shared.DataStructures;
using YOLO4.Shared.Parser;

namespace ObjectDetection.WinApp.MVVM.View
{
    public sealed partial class DetectPage : Page
    {
        private IFrameSource m_frameSource = null;

        // Frames
        private SoftwareBitmapSource m_processedBitmapSource;
        private VideoFrame m_renderTargetFrame = null;

        // Locks
        private SemaphoreSlim m_lock = new SemaphoreSlim(1);


        private Yolo4Service yolo4Service { get; set; }
        private Yolo4OutputParser outputParser = new();

        public DetectPage()
        {
            yolo4Service = App.GetService<Yolo4Service>();
            InitializeComponent();
        }

        private async void SelectCamera_Click(object sender, RoutedEventArgs e)
        {
            var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();

            var devicePicker = new DevicePicker();
            devicePicker.Filter.SupportedDeviceClasses.Add(DeviceClass.VideoCapture);

            // Get the current window's HWND by passing in the Window object
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            // Associate the HWND with the file picker
            WinRT.Interop.InitializeWithWindow.Initialize(devicePicker, hwnd);

            // Calculate the position to show the picker (right below the buttons)
            //GeneralTransform ge = SelectCamera.TransformToVisual(null);
            //Windows.Foundation.Point point = ge.TransformPoint(new Windows.Foundation.Point());
            //Windows.Foundation.Rect rect = new Windows.Foundation.Rect(point, new Windows.Foundation.Point(point.X + SelectCamera.ActualWidth, point.Y + SelectCamera.ActualHeight));

            //DeviceInformation di = await devicePicker.PickSingleDeviceAsync(rect);
            //if (di != null)
            //{
            //    try
            //    {
            //        //   await ConfigureFrameSourceAsync(di);
            //    }
            //    catch (Exception ex)
            //    {
            //        //  NotifyUser("Error occurred while initializating MediaCapture:\n" + ex.Message);
            //    }
            //}
        }

        //        private async Task ConfigureFrameSourceAsync(object source)
        //        {
        //            await m_lock.WaitAsync();
        //            {
        //                // Reset bitmap rendering component
        //                UIProcessedPreview.Source = null;
        //                m_renderTargetFrame = null;
        //                m_processedBitmapSource = new SoftwareBitmapSource();
        //                UIProcessedPreview.Source = m_processedBitmapSource;

        //                // Clean up previous frame source
        //                if (m_frameSource != null)
        //                {
        //                    m_frameSource.FrameArrived -= FrameSource_FrameAvailable;
        //                    var disposableFrameSource = m_frameSource as IDisposable;
        //                    if (disposableFrameSource != null)
        //                    {
        //                        // Lock disposal based on frame source consumers
        //                        disposableFrameSource.Dispose();
        //                    }
        //                }

        //                // Create new frame source and register a callback if the source fails along the way
        //                m_frameSource = await FrameSourceFactory.CreateFrameSourceAsync(
        //                    source,
        //                    (sender, message) =>
        //                    {
        //                        // NotifyUser(message);
        //                    });

        //                // TODO: Workaround for a bug in ObjectDetectorBinding when binding consecutively VideoFrames with Direct3DSurface and SoftwareBitmap
        //                //  await m_skillWrappers[0].InitializeSkillAsync(m_skillWrappers[0].Skill.Device);

        //                // Set additional input features as exposed in the UI
        //                //   await m_skillWrappers[0].Binding["InputObjectKindFilterList"].SetFeatureValueAsync(m_objectKindFilterList);
        //                //   await m_skillWrappers[0].Binding["InputConfidenceThreshold"].SetFeatureValueAsync((float)UIConfidenceThresholdControl.Value);
        //            }
        //            m_lock.Release();

        //            // If we obtained a valid frame source, start it
        //            if (m_frameSource != null)
        //            {
        //                m_frameSource.FrameArrived += FrameSource_FrameAvailable;
        //                await m_frameSource.StartAsync();
        //            }
        //        }

        //        private void FrameSource_FrameAvailable(object sender, VideoFrame frame)
        //        {
        //            // Locking behavior, so only one skill execution happens at a time
        //            if (m_lock.Wait(0))
        //            {
        //#pragma warning disable CS4014
        //                // Purposely don't await this: want handler to exit ASAP
        //                // so that realtime capture doesn't wait for completion.
        //                // Instead, we unlock only when processing finishes ensuring that
        //                // only one execution is active at a time, dropping frames or
        //                // aborting skill runs as necessary
        //                Task.Run(async () =>
        //                {
        //                    try
        //                    {
        //                        // Retrieve and filter results if requested
        //                      // IReadOnlyList<YoloV4Result> detectedObjects = await DetectObjectsAsync(frame);
        //                        IReadOnlyList<YoloV4Result> detectedObjects = new List<YoloV4Result>();
        //                        await DisplayFrameAndResultAsync(frame, detectedObjects);
        //                    }
        //                    catch (Exception ex)
        //                    {
        //                        // NotifyUser(ex.Message);
        //                    }
        //                    finally
        //                    {
        //                        m_lock.Release();
        //                    }
        //                });
        //#pragma warning restore CS4014
        //            }
        //        }

        //        private async Task<IReadOnlyList<YoloV4Result>> DetectObjectsAsync(VideoFrame frame)
        //        {
        //            var predict = await yolo4Service.Predict(frame);
        //            return outputParser.ParseOutputs(predict);
        //        }



        //        private async Task DisplayFrameAndResultAsync(VideoFrame frame, IReadOnlyList<YoloV4Result> detectedObjects)
        //        {
        //            App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
        //            {
        //                try
        //                {
        //                    SoftwareBitmap targetSoftwareBitmap = frame.SoftwareBitmap;

        //                    // If we receive a Direct3DSurface-backed VideoFrame, convert to a SoftwareBitmap in a format that can be rendered via the UI element
        //                    if (targetSoftwareBitmap == null)
        //                    {
        //                        if (m_renderTargetFrame == null)
        //                        {
        //                            m_renderTargetFrame = new VideoFrame(BitmapPixelFormat.Bgra8, frame.Direct3DSurface.Description.Width, frame.Direct3DSurface.Description.Height, BitmapAlphaMode.Ignore);
        //                        }

        //                        // Leverage the VideoFrame.CopyToAsync() method that can convert the input Direct3DSurface-backed VideoFrame to a SoftwareBitmap-backed VideoFrame
        //                        await frame.CopyToAsync(m_renderTargetFrame);
        //                        targetSoftwareBitmap = m_renderTargetFrame.SoftwareBitmap;
        //                    }
        //                    // Else, if we receive a SoftwareBitmap-backed VideoFrame, if its format cannot already be rendered via the UI element, convert it accordingly
        //                    else
        //                    {
        //                        if (targetSoftwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || targetSoftwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Ignore)
        //                        {
        //                            if (m_renderTargetFrame == null)
        //                            {
        //                                m_renderTargetFrame = new VideoFrame(BitmapPixelFormat.Bgra8, targetSoftwareBitmap.PixelWidth, targetSoftwareBitmap.PixelHeight, BitmapAlphaMode.Ignore);
        //                            }

        //                            // Leverage the VideoFrame.CopyToAsync() method that can convert the input SoftwareBitmap-backed VideoFrame to a different format
        //                            await frame.CopyToAsync(m_renderTargetFrame);
        //                            targetSoftwareBitmap = m_renderTargetFrame.SoftwareBitmap;
        //                        }
        //                    }
        //                    await m_processedBitmapSource.SetBitmapAsync(targetSoftwareBitmap);

        //                    // Update displayed results
        //                    // m_bboxRenderer.Render(detectedObjects);

        //                    // Update the displayed performance text
        //                    //UIPerfTextBlock.Text = $"bind: {m_bindTime.ToString("F2")}ms, eval: {m_evalTime.ToString("F2")}ms";
        //                }
        //                catch (TaskCanceledException)
        //                {
        //                    // no-op: we expect this exception when we change media sources
        //                    // and can safely ignore/continue
        //                }
        //                catch (Exception ex)
        //                {
        //                    // NotifyUser($"Exception while rendering results: {ex.Message}");
        //                }
        //            });
        //        }




        //        /// <summary>
        //        /// Triggered when the image control is resized, making sure the canvas size stays in sync with the frame display control.
        //        /// </summary>
        //        /// <param name="sender"></param>
        //        /// <param name="e"></param>
        //        private void UIProcessedPreview_SizeChanged(object sender, SizeChangedEventArgs e)
        //        {
        //            // Make sure the aspect ratio is honored when rendering the body limbs
        //            float cameraAspectRatio = (float)m_frameSource.FrameWidth / m_frameSource.FrameHeight;
        //            float previewAspectRatio = (float)(UIProcessedPreview.ActualWidth / UIProcessedPreview.ActualHeight);
        //            UIOverlayCanvas.Width = cameraAspectRatio >= previewAspectRatio ? UIProcessedPreview.ActualWidth : UIProcessedPreview.ActualHeight * cameraAspectRatio;
        //            UIOverlayCanvas.Height = cameraAspectRatio >= previewAspectRatio ? UIProcessedPreview.ActualWidth / cameraAspectRatio : UIProcessedPreview.ActualHeight;

        //            // m_bboxRenderer.ResizeContent(e);
        //        }


    }
}
