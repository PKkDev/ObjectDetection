using System.Text.Json.Serialization;

namespace YOLO26.Shared.CvatWorker.Model
{
    public class CvatJsonAnnotation
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("image_id")]
        public int ImageId { get; set; }

        [JsonPropertyName("category_id")]
        public int CategoryId { get; set; }

        [JsonPropertyName("segmentation")]
        public List<List<double>> Segmentation { get; set; }

        [JsonPropertyName("area")]
        public double Area { get; set; }

        [JsonPropertyName("bbox")]
        public List<double> Bbox { get; set; }

        [JsonPropertyName("iscrowd")]
        public int Iscrowd { get; set; }

        [JsonPropertyName("attributes")]
        public CvatJsonAttributes Attributes { get; set; }
    }

    public class CvatJsonAttributes
    {
        [JsonPropertyName("occluded")]
        public bool Occluded { get; set; }
    }

    public class CvatJsonCategory
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("supercategory")]
        public string Supercategory { get; set; }
    }

    public class CvatJsonImageCvat
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("file_name")]
        public string FileName { get; set; }

        [JsonPropertyName("license")]
        public int License { get; set; }

        [JsonPropertyName("flickr_url")]
        public string FlickrUrl { get; set; }

        [JsonPropertyName("coco_url")]
        public string CocoUrl { get; set; }

        [JsonPropertyName("date_captured")]
        public int DateCaptured { get; set; }
    }

    public class CvatJsonInfo
    {
        [JsonPropertyName("contributor")]
        public string Contributor { get; set; }

        [JsonPropertyName("date_created")]
        public string DateCreated { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("year")]
        public string Year { get; set; }
    }

    public class CvatJsonLicense
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    public class CvatJsonResult
    {
        [JsonPropertyName("licenses")]
        public List<CvatJsonLicense> Licenses { get; set; }

        [JsonPropertyName("info")]
        public CvatJsonInfo Info { get; set; }

        [JsonPropertyName("categories")]
        public List<CvatJsonCategory> Categories { get; set; }

        [JsonPropertyName("images")]
        public List<CvatJsonImageCvat> Images { get; set; }

        [JsonPropertyName("annotations")]
        public List<CvatJsonAnnotation> Annotations { get; set; }
    }
}
