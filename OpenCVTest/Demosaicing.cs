using OpenCvSharp;

public class Demosaicing
{
    public void Demosaice()
    {
        const string rawImage = "../../../../../PflanzschrankBilder\\pflanzschrankBilder\\videoraw\\test_00000.raw";
        var fs = File.OpenRead(rawImage);
        fs.ReadAsync()
        using var testSource = new Mat(rawImage, ImreadModes.AnyColor);
        Cv2.Demosaicing(testSource, testSource, ColorConversionCodes.BayerBG2BGR);
        using var windowTest = new Window("Demosaicing", WindowFlags.GuiExpanded);
        windowTest.ShowImage(testSource);
    }
}