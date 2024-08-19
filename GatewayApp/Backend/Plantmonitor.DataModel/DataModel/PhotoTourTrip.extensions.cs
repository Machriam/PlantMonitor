using System;
using System.Collections.Generic;
using static System.Net.Mime.MediaTypeNames;

namespace Plantmonitor.DataModel.DataModel;

public partial class PhotoTourTrip
{
    public const string IrPrefix = "ir_";
    public const string VisPrefix = "vis_";
    public const string RawIrPrefix = "rawIr_";

    public string VirtualImageFileName(string folder)
    {
        return $"{folder}/trip_{Timestamp:yyyyMMdd_HHmmss_fff}.zip";
    }
}
