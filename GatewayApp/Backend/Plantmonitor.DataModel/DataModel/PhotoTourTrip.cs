using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public partial class PhotoTourTrip
{
    public long Id { get; set; }

    public long PhotoTourFk { get; set; }

    public string IrDataFolder { get; set; } = null!;

    public string VisDataFolder { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    public string? VirtualPicturePath { get; set; }

    public string? SegmentationTemplate { get; set; }

    public virtual AutomaticPhotoTour PhotoTourFkNavigation { get; set; } = null!;

    public virtual ICollection<PlantExtractionTemplate> PlantExtractionTemplates { get; set; } = new List<PlantExtractionTemplate>();
}
