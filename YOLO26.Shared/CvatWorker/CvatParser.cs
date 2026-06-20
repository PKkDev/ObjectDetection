using SixLabors.ImageSharp;
using System.Text.Json;
using System.Xml.Serialization;
using YOLO26.Shared.CvatWorker.Model;
using System.Globalization;

namespace YOLO26.Shared.CvatWorker
{
    /// <summary>
    /// Пробразования экспорта из COCO 1.0 (импорт как COCO 1.0)
    /// Пробразования экспорта из CVAT for Images 1.1 (импорт как CVAT 1.1)
    /// </summary>
    public class CvatParser
    {
        public List<ParkingData> ParkingData { get; set; } = [];

        public ParkingData? this[string id]
        {
            get => ParkingData.FirstOrDefault(x => x.Id == id);
        }

        /// <summary>
        /// .json - экспорта из COCO 1.0
        /// .xml - экспорта из CVAT for Images 1.1
        /// </summary> 
        public void Load(string id, string path)
        {
            try
            {
                var fileInfo = new FileInfo(path);

                switch (fileInfo.Extension.ToLower())
                {
                    case ".json":
                        {
                            var cvatResult = JsonSerializer.Deserialize<CvatJsonResult>(File.ReadAllText(path));
                            List<ParkingSpace> parkingSlots = [];

                            if (cvatResult != null)
                            {
                                foreach (var annotation in cvatResult.Annotations)
                                {
                                    var parkingSpace = new ParkingSpace(annotation.Id);

                                    var segmentation = annotation.Segmentation.First();
                                    parkingSpace.Points[0] = new PointF((float)segmentation[0], (float)segmentation[1]);
                                    parkingSpace.Points[1] = new PointF((float)segmentation[2], (float)segmentation[3]);
                                    parkingSpace.Points[2] = new PointF((float)segmentation[4], (float)segmentation[5]);
                                    parkingSpace.Points[3] = new PointF((float)segmentation[6], (float)segmentation[7]);

                                    parkingSlots.Add(parkingSpace);
                                }
                            }

                            ParkingData.Add(new ParkingData(id, parkingSlots));

                            break;
                        }
                    case ".xml":
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(CvatXmlResult));
                            List<ParkingSpace> parkingSlots = [];

                            using StringReader reader = new StringReader(File.ReadAllText(path));

                            var data = (CvatXmlResult)serializer.Deserialize(reader);

                            if (data != null && data.Image.Any())
                            {
                                var polygons = data.Image.First().Polygon;
                                var tempoId = 1;
                                foreach (var polygon in polygons)
                                {
                                    var parkingSpace = new ParkingSpace(tempoId);

                                    var segmentation = polygon.Points.Split(';').Select(x => x.Split(',').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray()).ToArray();
                                    parkingSpace.Points[0] = new PointF((float)segmentation[0][0], (float)segmentation[0][1]);
                                    parkingSpace.Points[1] = new PointF((float)segmentation[1][0], (float)segmentation[1][1]);
                                    parkingSpace.Points[2] = new PointF((float)segmentation[2][0], (float)segmentation[2][1]);
                                    parkingSpace.Points[3] = new PointF((float)segmentation[3][0], (float)segmentation[3][1]);

                                    tempoId++;
                                    parkingSlots.Add(parkingSpace);
                                }
                            }

                            ParkingData.Add(new ParkingData(id, parkingSlots));
                        }
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Can not load COCO 1.0 export - {ex.Message}");
            }
        }


    }
}
