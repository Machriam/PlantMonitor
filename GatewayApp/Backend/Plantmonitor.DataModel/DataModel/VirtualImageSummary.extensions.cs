using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public partial class VirtualImageSummary
{
    public List<PlantImageDescriptors> ImageDescriptors { get; set; } = [];
}
