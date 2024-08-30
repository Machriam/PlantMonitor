using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public partial class VirtualImageSummary
{
    public long Id { get; set; }

    public string VirtualImagePath { get; set; } = null!;

    public DateTime VirtualImageCreationDate { get; set; }

    public string ImageDescriptorsJson { get; set; } = null!;
}
