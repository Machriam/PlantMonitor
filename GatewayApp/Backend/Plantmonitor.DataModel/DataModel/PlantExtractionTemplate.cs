using System;
using System.Collections.Generic;
using NpgsqlTypes;

namespace Plantmonitor.DataModel.DataModel;

public partial class PlantExtractionTemplate
{
    public long Id { get; set; }

    public long PhotoTripFk { get; set; }

    public long PhotoTourPlantFk { get; set; }

    public NpgsqlPolygon PhotoBoundingBox { get; set; }

    public NpgsqlPoint IrBoundingBoxOffset { get; set; }

    public virtual PhotoTourPlant PhotoTourPlantFkNavigation { get; set; } = null!;

    public virtual PhotoTourTrip PhotoTripFkNavigation { get; set; } = null!;
}
