using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public partial class PhotoTourEvent
{
    public long Id { get; set; }

    public long PhotoTourFk { get; set; }

    public string EventClass { get; set; } = null!;

    public string Message { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    public virtual AutomaticPhotoTour PhotoTourFkNavigation { get; set; } = null!;
}
