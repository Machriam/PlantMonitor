
namespace PlantMonitorControl.Features.ImageTaking;

public static class IrStringExtensions
{
    public static byte[] GetBytesFromIrFilePath(this string irFilePath)
    {
        return File.ReadAllText(irFilePath)
            .Replace("\n", " ")
            .Split(" ")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .SelectMany(x => BitConverter.GetBytes(int.Parse(x.Trim())))
            .ToArray();
    }
}
