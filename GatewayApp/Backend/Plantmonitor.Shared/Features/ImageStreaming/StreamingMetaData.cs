using System.Runtime.Serialization;

namespace Plantmonitor.Shared.Features.ImageStreaming;

[KnownType(typeof(StreamingMetaData))]
public record struct StreamingMetaData(float ResolutionDivider, int Quality, float DistanceInM, bool StoreData, int[] PositionsToStream);
