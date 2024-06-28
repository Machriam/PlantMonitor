using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public partial class PhotoTourEvent
{
    public long Id { get; set; }

    public long PhotoTourFk { get; set; }

    public string Message { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    public long? ReferencesEvent { get; set; }

    public virtual ICollection<PhotoTourEvent> InverseReferencesEventNavigation { get; set; } = new List<PhotoTourEvent>();

    public virtual AutomaticPhotoTour PhotoTourFkNavigation { get; set; } = null!;

    public virtual PhotoTourEvent? ReferencesEventNavigation { get; set; }
}
