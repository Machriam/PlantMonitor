using System.Runtime.Serialization;

namespace Plantmonitor.Shared.Features.ImageStreaming;

[AttributeUsage(AttributeTargets.Field)]
public class CameraTypeInfo : Attribute
{
    public string SignalRMethod { get; set; } = "";
    public string FileEnding { get; set; } = "";
}

public enum CameraType
{
    [CameraTypeInfo(SignalRMethod = "StreamJpg", FileEnding = ".jpg")]
    Vis,

    [CameraTypeInfo(SignalRMethod = "StreamIrData", FileEnding = ".rawir")]
    IR
}

[KnownType(typeof(StreamingMetaData))]
public record struct StreamingMetaData(float ResolutionDivider, int Quality, float DistanceInM, bool StoreData, int[] PositionsToStream, CameraType Type);
