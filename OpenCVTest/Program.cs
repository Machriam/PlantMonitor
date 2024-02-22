using OpenCvSharp;
using System.Text.Json;

var demosaicing = new Demosaicing();
demosaicing.Demosaice();
const string testImage = "../../../../../PflanzschrankBilder\\pflanzschrankBilder\\images\\test_0.png";
using var src = new Mat(testImage, ImreadModes.Color);
using var testMat = new Mat();
using var hsvImage = new Mat();
using var mask = new Mat();
using var dest = new Mat();
using var maskColorMat = new Mat();
using var cannyMat = new Mat();
using var contourMat = new Mat();

using var destWindow = new Window("Dest", WindowFlags.GuiExpanded);
using var openedMask = new Window("Opening", WindowFlags.GuiExpanded);
using var maskWindow = new Window("Mask", WindowFlags.GuiExpanded);
using var maskColorWindow = new Window("MaskColor", WindowFlags.GuiExpanded);
using var cannyWindow = new Window("Canny", WindowFlags.GuiExpanded);
Cv2.CvtColor(src, hsvImage, ColorConversionCodes.BGR2HSV);
var lowGreen = InputArray.Create([30, 0, 0]);
var highGreen = InputArray.Create([100, 255, 255]);
Cv2.InRange(hsvImage, lowGreen, highGreen, mask);
maskWindow.ShowImage(mask);
Cv2.BitwiseAnd(src, src, maskColorMat, mask);
maskColorWindow.ShowImage(maskColorMat);
Cv2.CvtColor(maskColorMat, dest, ColorConversionCodes.BGR2GRAY);
Cv2.Erode(dest, dest, null, null, 10);
Cv2.Dilate(dest, dest, null, null, 10);
Cv2.Threshold(dest, dest, 50, 255, ThresholdTypes.Binary);
openedMask.ShowImage(dest);
Cv2.FindContours(dest, out var contours, OutputArray.Create(testMat), RetrievalModes.External, ContourApproximationModes.ApproxSimple);
var areas = new List<double>();
foreach (var contour in contours)
{
    areas.Add(contour.ContourArea());
    Cv2.DrawContours(src, [contour], -1, Scalar.Orange, thickness: 10);
}
cannyWindow.ShowImage(dest);
Console.WriteLine(areas.Max());
Console.WriteLine(areas.Min());
Console.WriteLine(areas[areas.Count / 2]);
Console.WriteLine(JsonSerializer.Serialize(areas));
destWindow.ShowImage(src);
foreach (var window in new List<Window> { destWindow, cannyWindow, maskColorWindow, maskWindow, openedMask })
{
    Cv2.ImWrite(window.Name + ".png", window.Image);
}
Cv2.WaitKey();