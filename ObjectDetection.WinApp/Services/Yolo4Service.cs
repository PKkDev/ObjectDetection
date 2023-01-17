using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using YOLO4.Shared.DataStructures;
using YOLO4.Shared.Settings;
using Windows.UI;
using Microsoft.ML;
using System.IO;
using Windows.Graphics.Imaging;
using Windows.Storage;
using SkiaSharp;

namespace ObjectDetection.WinApp.Services
{
    public class Yolo4Service
    {
        private readonly MLContext _mlContext;

        private DataViewSchema _modelSchema { get; set; }
        private PredictionEngine<YoloV4InputData, YoloV4OutputData> _predictionEngine;

        public Yolo4Service()
        {
            _mlContext = new();
            LoadModel();
        }

        private void LoadModel()
        {
            if (_modelSchema != null && _predictionEngine != null) return;

            var modelLocation = Path.Combine(AppContext.BaseDirectory, "Assets\\YoloModel\\modelYolo4Path.zip");

            FileInfo fi = new(modelLocation);
            if (!fi.Exists) throw new Exception("saved yolo model not found");

            DataViewSchema modelSchema;
            ITransformer trainedModel = _mlContext.Model.Load(modelLocation, out modelSchema);
            _modelSchema = modelSchema;
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<YoloV4InputData, YoloV4OutputData>(trainedModel);
        }

        public YoloV4OutputData Predict(YoloV4InputData input)
        {
            YoloV4OutputData predict = _predictionEngine.Predict(input);
            return predict;
        }

        public async Task<YoloV4OutputData> PredictAsync(YoloV4InputData input)
        {
            var t = Task.Run(() =>
            {
                YoloV4OutputData predict = _predictionEngine.Predict(input);
                return predict;
            });
            t.Wait();
            return t.Result;
        }

        public async Task<YoloV4OutputData> PredictAsync(SoftwareBitmap softwareBitmap)
        {
            StorageFile savedImage = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync("detecttempo.jpg", CreationCollisionOption.ReplaceExisting);
            using IRandomAccessStream stream = await savedImage.OpenAsync(FileAccessMode.ReadWrite);
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
            encoder.SetSoftwareBitmap(softwareBitmap);
            await encoder.FlushAsync();

            stream.Dispose();

            YoloV4InputData input = new() { ImagePath = savedImage.Path };
            YoloV4OutputData predict = _predictionEngine.Predict(input);
            return predict;
        }

        public async Task<BitmapImage> DrawTest(List<YoloV4Result> probability, string savedImagePath, uint width, uint height, double imageDpi, string savefileName = null)
        {
            CanvasDevice device = CanvasDevice.GetSharedDevice();
            CanvasRenderTarget offscreen = new(device, width, height, (int)imageDpi);

            var cbi = await CanvasBitmap.LoadAsync(device, savedImagePath);

            using (var ds = offscreen.CreateDrawingSession())
            {
                ds.DrawImage(cbi);

                var format = new CanvasTextFormat()
                {
                    FontSize = 18,
                    HorizontalAlignment = CanvasHorizontalAlignment.Left,
                    VerticalAlignment = CanvasVerticalAlignment.Top,
                    WordWrapping = CanvasWordWrapping.Wrap,
                    FontFamily = "Decor",
                };

                foreach (var res in probability)
                {
                    var x = res.BBox[0];
                    var y = res.BBox[1];
                    var drawWidth = res.BBox[2] - res.BBox[0];
                    var drawHeight = res.BBox[3] - res.BBox[1];

                    Console.WriteLine($"originaly - x:{x}, y:{y}, w:{drawWidth}, h:{drawHeight}, l:{res.Label}, c:{res.Confidence}");

                    x = width * x / ImageNetSettings.imageWidth;
                    y = height * y / ImageNetSettings.imageHeight;
                    drawWidth = width * drawWidth / ImageNetSettings.imageWidth;
                    drawHeight = height * drawHeight / ImageNetSettings.imageHeight;

                    ds.DrawRectangle(x, y, drawWidth, drawHeight, Color.FromArgb(255, 230, 0, 1), 2);

                    var text = $"{res.Label} ({res.Confidence * 100:0}%)";
                    ds.DrawText(text, x, y, Color.FromArgb(255, 230, 0, 1), format);
                }

                format.Dispose();
            }

            using var stream = new InMemoryRandomAccessStream();
            stream.Seek(0);
            await offscreen.SaveAsync(stream, CanvasBitmapFileFormat.Jpeg);

            BitmapImage image = new();
            image.SetSource(stream);
            //imagePlace2.Source = image;

            #region save file if savefileName

            //if (savefileName != null)
            //{
            //    StorageFile savedImage = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync($"{savefileName}_pred.jpg", CreationCollisionOption.GenerateUniqueName);
            //    using IRandomAccessStream stream2 = await savedImage.OpenAsync(FileAccessMode.ReadWrite);
            //    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream2);
            //    var decoder = await BitmapDecoder.CreateAsync(stream);
            //    var softBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            //    encoder.SetSoftwareBitmap(softBitmap);
            //    await encoder.FlushAsync();
            //    stream2.Dispose();
            //}

            #endregion save file if savefileName

            cbi.Dispose();
            stream.Dispose();

            return image;
        }

        public async Task<SoftwareBitmap> RenderProbabilityAsync(List<YoloV4Result> probability, SoftwareBitmap softwareBitmap)
        {
            var width = softwareBitmap.PixelWidth;
            var height = softwareBitmap.PixelHeight;
            var frameDpi = Math.Max(softwareBitmap.DpiX, softwareBitmap.DpiY);

            CanvasDevice device = CanvasDevice.GetSharedDevice();
            CanvasRenderTarget offscreen = new(device, width, height, (int)frameDpi);

            var cbi = CanvasBitmap.CreateFromSoftwareBitmap(device, softwareBitmap);

            using var ds = offscreen.CreateDrawingSession();

            ds.DrawImage(cbi);

            var format = new CanvasTextFormat()
            {
                FontSize = 18,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
                VerticalAlignment = CanvasVerticalAlignment.Top,
                WordWrapping = CanvasWordWrapping.Wrap,
                FontFamily = "Decor",
            };

            foreach (var res in probability)
            {
                var x = res.BBox[0];
                var y = res.BBox[1];
                var drawWidth = res.BBox[2] - res.BBox[0];
                var drawHeight = res.BBox[3] - res.BBox[1];

                x = width * x / ImageNetSettings.imageWidth;
                y = height * y / ImageNetSettings.imageHeight;
                drawWidth = width * drawWidth / ImageNetSettings.imageWidth;
                drawHeight = height * drawHeight / ImageNetSettings.imageHeight;

                ds.DrawRectangle(x, y, drawWidth, drawHeight, Color.FromArgb(255, 230, 0, 1), 2);

                var text = $"{res.Label} ({res.Confidence * 100:0}%)";
                ds.DrawText(text, x, y, Color.FromArgb(255, 230, 0, 1), format);
            }

            ds.Dispose();
            format.Dispose();

            using var stream = new InMemoryRandomAccessStream();
            stream.Seek(0);
            await offscreen.SaveAsync(stream, CanvasBitmapFileFormat.Jpeg);

            #region return SoftwareBitmap

            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            SoftwareBitmap sb = await decoder.GetSoftwareBitmapAsync();
            SoftwareBitmap result = SoftwareBitmap.Convert(sb, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);

            #endregion return SoftwareBitmap

            #region return BitmapImage

            //BitmapImage result = new();
            //result.SetSource(stream);

            #endregion return BitmapImage

            #region test save

            //StorageFile savedImage = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync($"test_pred.jpg", CreationCollisionOption.GenerateUniqueName);
            //using IRandomAccessStream stream2 = await savedImage.OpenAsync(FileAccessMode.ReadWrite);
            //BitmapEncoder encoder1 = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream2);
            ////var decoder1 = await BitmapDecoder.CreateAsync(stream);
            ////var softBitmap = await decoder1.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            ////SoftwareBitmap softBitmap2 = SoftwareBitmap.Convert(softBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
            //encoder1.SetSoftwareBitmap(result);
            //await encoder1.FlushAsync();
            //stream2.Dispose();

            #endregion test save

            cbi.Dispose();
            stream.Dispose();

            return result;
        }

    }
}
