namespace Plantmonitor.DataModel.DataModel;

public class PlantImageDescriptors
{
    public ReferencedPlant Plant { get; set; } = new();
    public float SizeInMm2 { get; set; }
    public float AverageTemperature { get; set; }
    public float MedianTemperature { get; set; }
    public float TemperatureDev { get; set; }
    public float MaxTemperature { get; set; }
    public float MinTemperature { get; set; }
    public float HeightInMm { get; set; }
    public float WidthInMm { get; set; }
    public float Extent { get; set; }
    public float ConvexHullAreaInMm2 { get; set; }
    public float Solidity { get; set; }
    public int LeafCount { get; set; }
    public bool LeafOutOfRange { get; set; }
    public float[] HslAverage { get; set; } = [];
    public float[] HslMedian { get; set; } = [];
    public float[] HslMax { get; set; } = [];
    public float[] HslMin { get; set; } = [];
    public float[] HslDeviation { get; set; } = [];
    public bool NoImage { get; set; }
}

public class ReferencedPlant
{
    public int ImageIndex { get; set; }
    public string ImageName { get; set; } = "";
    public string ImageComment { get; set; } = "";
    public bool HasIr { get; set; }
    public bool HasVis { get; set; }
    public DateTime IrTime { get; set; }
    public DateTime VisTime { get; set; }
    public float IrTempInC { get; set; }
}
