using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public partial class PhotoTourJourney
{
    public long Id { get; set; }

    public long PhotoTourFk { get; set; }

    public string IrDataFolder { get; set; } = null!;

    public string VisDataFolder { get; set; } = null!;

    public virtual AutomaticPhotoTour PhotoTourFkNavigation { get; set; } = null!;
}
