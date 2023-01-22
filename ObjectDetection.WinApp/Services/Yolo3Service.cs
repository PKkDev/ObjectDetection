using Microsoft.ML;
using System.IO;
using System;
using YOLO3.Shared.DataStructures;
using YOLO4.Shared.DataStructures;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.Storage;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas;
using System.Collections.Generic;
using YOLO4.Shared.Settings;
using Windows.UI;

namespace ObjectDetection.WinApp.Services
{
    public class Yolo3Service
    {
        private readonly MLContext _mlContext;

        private DataViewSchema _modelSchema { get; set; }
        private PredictionEngine<Yolo3InputData, Yolo3OutputData> _predictionEngine;

        public Yolo3Service()
        {
            _mlContext = new();
            //LoadModel();
        }

        private void LoadModel()
        {
            if (_modelSchema != null && _predictionEngine != null) return;

            var modelLocation = Path.Combine(AppContext.BaseDirectory, "Assets\\YoloModel\\modelYolo312int8Path.zip");

            FileInfo fi = new(modelLocation);
            if (!fi.Exists) throw new Exception("saved yolo model not found");

            DataViewSchema modelSchema;
            ITransformer trainedModel = _mlContext.Model.Load(modelLocation, out modelSchema);
            _modelSchema = modelSchema;
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<Yolo3InputData, Yolo3OutputData>(trainedModel);
        }

        public async Task<Yolo3OutputData> PredictAsync(SoftwareBitmap softwareBitmap)
        {
            StorageFile savedImage = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync("detecttempo.jpg", CreationCollisionOption.ReplaceExisting);
            using IRandomAccessStream stream = await savedImage.OpenAsync(FileAccessMode.ReadWrite);
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
            encoder.SetSoftwareBitmap(softwareBitmap);
            await encoder.FlushAsync();

            stream.Dispose();

            Yolo3InputData input = new() { ImagePath = savedImage.Path };
            Yolo3OutputData predict = _predictionEngine.Predict(input);
            return predict;
        }

        public async Task<SoftwareBitmap> RenderProbabilityAsync(List<Yolo3Result> probability, SoftwareBitmap softwareBitmap)
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
                var x = res.BBox[1];
                var y = res.BBox[0];
                var drawWidth = res.BBox[3] - res.BBox[1];
                var drawHeight = res.BBox[2] - res.BBox[0];

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
