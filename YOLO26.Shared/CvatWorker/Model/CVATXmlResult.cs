using System.Xml.Serialization;

namespace YOLO26.Shared.CvatWorker.Model
{
    [XmlRoot(ElementName = "segment")]
    public class CvatXmlSegment
    {

        [XmlElement(ElementName = "id")]
        public int Id { get; set; }

        [XmlElement(ElementName = "start")]
        public int Start { get; set; }

        [XmlElement(ElementName = "stop")]
        public int Stop { get; set; }

        [XmlElement(ElementName = "url")]
        public string Url { get; set; }
    }

    [XmlRoot(ElementName = "segments")]
    public class CvatXmlSegments
    {

        [XmlElement(ElementName = "segment")]
        public CvatXmlSegment Segment { get; set; }
    }

    [XmlRoot(ElementName = "owner")]
    public class CvatXmlOwner
    {

        [XmlElement(ElementName = "username")]
        public string Username { get; set; }

        [XmlElement(ElementName = "email")]
        public string Email { get; set; }
    }

    [XmlRoot(ElementName = "label")]
    public class CvatXmlLabel
    {

        [XmlElement(ElementName = "name")]
        public string Name { get; set; }

        [XmlElement(ElementName = "color")]
        public string Color { get; set; }

        [XmlElement(ElementName = "type")]
        public string Type { get; set; }

        [XmlElement(ElementName = "attributes")]
        public object Attributes { get; set; }
    }

    [XmlRoot(ElementName = "labels")]
    public class CvatXmlLabels
    {

        [XmlElement(ElementName = "label")]
        public List<CvatXmlLabel> Label { get; set; }
    }

    [XmlRoot(ElementName = "job")]
    public class CvatXmlJob
    {

        [XmlElement(ElementName = "id")]
        public int Id { get; set; }

        [XmlElement(ElementName = "size")]
        public int Size { get; set; }

        [XmlElement(ElementName = "mode")]
        public string Mode { get; set; }

        [XmlElement(ElementName = "overlap")]
        public int Overlap { get; set; }

        [XmlElement(ElementName = "bugtracker")]
        public object Bugtracker { get; set; }

        [XmlElement(ElementName = "created")]
        public string Created { get; set; }

        [XmlElement(ElementName = "updated")]
        public string Updated { get; set; }

        [XmlElement(ElementName = "subset")]
        public string Subset { get; set; }

        [XmlElement(ElementName = "start_frame")]
        public int StartFrame { get; set; }

        [XmlElement(ElementName = "stop_frame")]
        public int StopFrame { get; set; }

        [XmlElement(ElementName = "frame_filter")]
        public object FrameFilter { get; set; }

        [XmlElement(ElementName = "segments")]
        public CvatXmlSegments Segments { get; set; }

        [XmlElement(ElementName = "owner")]
        public CvatXmlOwner Owner { get; set; }

        [XmlElement(ElementName = "assignee")]
        public object Assignee { get; set; }

        [XmlElement(ElementName = "labels")]
        public CvatXmlLabels Labels { get; set; }
    }

    [XmlRoot(ElementName = "meta")]
    public class CvatXmlMeta
    {

        [XmlElement(ElementName = "job")]
        public CvatXmlJob Job { get; set; }

        [XmlElement(ElementName = "dumped")]
        public string Dumped { get; set; }
    }

    [XmlRoot(ElementName = "polygon")]
    public class CvatXmlPolygon
    {

        [XmlAttribute(AttributeName = "label")]
        public string Label { get; set; }

        [XmlAttribute(AttributeName = "source")]
        public string Source { get; set; }

        [XmlAttribute(AttributeName = "occluded")]
        public int Occluded { get; set; }

        [XmlAttribute(AttributeName = "points")]
        public string Points { get; set; }

        [XmlAttribute(AttributeName = "z_order")]
        public int ZOrder { get; set; }
    }

    [XmlRoot(ElementName = "image")]
    public class CvatXmlImage
    {

        [XmlElement(ElementName = "polygon")]
        public List<CvatXmlPolygon> Polygon { get; set; }

        [XmlAttribute(AttributeName = "id")]
        public int Id { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "width")]
        public int Width { get; set; }

        [XmlAttribute(AttributeName = "height")]
        public int Height { get; set; }
    }

    [XmlRoot(ElementName = "annotations")]
    public class CvatXmlResult
    {

        [XmlElement(ElementName = "version")]
        public string Version { get; set; }

        [XmlElement(ElementName = "meta")]
        public CvatXmlMeta Meta { get; set; }

        [XmlElement(ElementName = "image")]
        public List<CvatXmlImage> Image { get; set; }
    }


}
