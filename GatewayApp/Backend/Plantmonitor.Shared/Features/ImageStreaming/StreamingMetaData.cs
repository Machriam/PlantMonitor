using System.Runtime.Serialization;
using Plantmonitor.Shared.Extensions;

namespace Plantmonitor.Shared.Features.ImageStreaming;

[AttributeUsage(AttributeTargets.Field)]
public class CameraTypeInfo : Attribute
{
    public string SignalRMethod { get; set; } = "";
    public string CustomStorageMethod { get; set; } = "";
    public string FileEnding { get; set; } = "";
    public string MetaDataFile { get; set; } = "";
}

public enum CameraType
{
    [CameraTypeInfo(CustomStorageMethod = "StoreJpg", SignalRMethod = "StreamJpg", FileEnding = ".jpg", MetaDataFile = "")]
    Vis,

    [CameraTypeInfo(CustomStorageMethod = "StoreIrData", SignalRMethod = "StreamIrData", FileEnding = ".rawir", MetaDataFile = ".metair")]
    IR
}

public static class CameraTypeExtensions
{
    public static CameraTypeInfo GetInfo(this CameraType type) => type.Attribute<CameraTypeInfo>();
}

[KnownType(typeof(StreamingMetaData))]
public record struct StreamingMetaData(float ResolutionDivider, int Quality, float DistanceInM, bool StoreData, int[] PositionsToStream, string Type)
{
    private static readonly Dictionary<string, CameraType> s_cameraTypeByText = Enum.GetValues<CameraType>().Select(v => (Name: Enum.GetName(v) ?? "", Value: v)).ToDictionary(x => x.Name, x => x.Value);
    public readonly CameraType GetCameraType() => s_cameraTypeByText[Type];
    public static StreamingMetaData Create(float resolutionDivider, int quality, float distanceInM, bool storeData, int[] positionsToStream, CameraType type)
    {
        return new(resolutionDivider, quality, distanceInM, storeData, positionsToStream, Enum.GetName(type) ?? "");
    }
}
