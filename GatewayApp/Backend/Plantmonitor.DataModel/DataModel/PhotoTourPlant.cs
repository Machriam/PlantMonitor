using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public partial class PhotoTourPlant
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string Comment { get; set; } = null!;

    public string? Position { get; set; }

    public long PhotoTourFk { get; set; }

    public virtual AutomaticPhotoTour PhotoTourFkNavigation { get; set; } = null!;

    public virtual ICollection<PlantExtractionTemplate> PlantExtractionTemplates { get; set; } = new List<PlantExtractionTemplate>();
}
