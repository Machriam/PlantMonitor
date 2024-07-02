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

    public virtual AutomaticPhotoTour PhotoTourFkNavigation { get; set; } = null!;
}
