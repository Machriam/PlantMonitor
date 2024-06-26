﻿using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Stitching;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace Plantmonitor.Server.Features.ImageStitching;

public class PhotoStitcher
{
    /// <summary>
    /// Comparison of Feature algorithms: https://ieeexplore.ieee.org/document/8346440
    /// </summary>
    /// <param name="folderName"></param>
    public void StitchPhotos(string folderName)
    {
        var files = Directory.GetFiles(folderName);
        var imagesToStitch = files.OrderBy(f => f).Select(f => new Mat(f)).Take(2).ToList();
        var stitcher = new Stitcher(Stitcher.Mode.Scans);
        var result = imagesToStitch[0];
        for (var i = 1; i < imagesToStitch.Count; i++)
        {
            var input = new VectorOfMat(result, imagesToStitch[i]);
            var status = stitcher.Stitch(input, result);
        }

        result.Save(folderName + "/../result.png");
        foreach (var image in imagesToStitch) image.Dispose();
        result.Dispose();
        stitcher.Dispose();
    }

    private static void CalculateHomography(Mat modelImage, Mat observedImage, out VectorOfKeyPoint modelKeyPoints,
        out VectorOfKeyPoint observedKeyPoints, VectorOfVectorOfDMatch matches, out Mat mask, out Mat? homography)
    {
        const int K = 2;
        const double UniquenessThreshold = 0.80d;
        homography = null;
        modelKeyPoints = new VectorOfKeyPoint();
        observedKeyPoints = new VectorOfKeyPoint();
        using var uModelImage = modelImage.GetUMat(AccessType.Read);
        using var uObservedImage = observedImage.GetUMat(AccessType.Read);
        using var featureDetector = new ORB(9000);
        using var modelDescriptors = new Mat();
        featureDetector.DetectAndCompute(uModelImage, null, modelKeyPoints, modelDescriptors, false);
        using var observedDescriptors = new Mat();
        featureDetector.DetectAndCompute(uObservedImage, null, observedKeyPoints, observedDescriptors, false);
        using var matcher = new BFMatcher(DistanceType.Hamming, false);
        matcher.Add(modelDescriptors);

        matcher.KnnMatch(observedDescriptors, matches, K, null);
        mask = new Mat(matches.Size, 1, DepthType.Cv8U, 1);
        mask.SetTo(new MCvScalar(255));
        Features2DToolbox.VoteForUniqueness(matches, UniquenessThreshold, mask);

        var nonZeroCount = CvInvoke.CountNonZero(mask);
        if (nonZeroCount < 4) return;
        nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(modelKeyPoints, observedKeyPoints, matches, mask, 1.5, 20);
        if (nonZeroCount < 4) return;
        homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(modelKeyPoints, observedKeyPoints, matches, mask, 2);
    }

    public static Mat Draw(Mat modelImage, Mat observedImage)
    {
        using var matches = new VectorOfVectorOfDMatch();
        CalculateHomography(modelImage, observedImage, out var modelKeyPoints, out var observedKeyPoints, matches, out var mask, out var homography);
        var result = new Mat();
        Features2DToolbox.DrawMatches(modelImage, modelKeyPoints, observedImage, observedKeyPoints,
            matches, result, new MCvScalar(255, 0, 0), new MCvScalar(0, 0, 255), mask);

        if (homography == null) return result;
        var imgWarped = new Mat();
        CvInvoke.WarpPerspective(observedImage, imgWarped, homography, modelImage.Size, Inter.Linear, Warp.InverseMap);
        var rect = new Rectangle(Point.Empty, modelImage.Size);
        var pts = new PointF[]
        {
                  new(rect.Left, rect.Bottom),
                  new(rect.Right, rect.Bottom),
                  new(rect.Right, rect.Top),
                  new(rect.Left, rect.Top)
        };

        pts = CvInvoke.PerspectiveTransform(pts, homography);
        var points = new Point[pts.Length];
        for (var i = 0; i < points.Length; i++) points[i] = Point.Round(pts[i]);

        using var vp = new VectorOfPoint(points);
        CvInvoke.Polylines(result, vp, true, new MCvScalar(255, 0, 0, 255), 5);
        CvInvoke.Resize(result, result, default, 0.5, 0.5);
        return result;
    }

    public void StitchPhotosManual(string folderName)
    {
        var files = Directory.GetFiles(folderName).Take(2);
        var imagesToStitch = files.OrderBy(f => f).Select(f => new Mat(f)).ToList();
        var matches = new VectorOfVectorOfDMatch();
        CalculateHomography(imagesToStitch[0], imagesToStitch[1], out var keyPoints, out var keyPoints2, matches, out var mask, out var homography);
        var translationHomography = new Matrix<double>(new double[,] { { 1, 0, -100 }, { 0, 1, -100 }, { 0, 0, 1 } });
        var translatedSource = new Mat();
        var warpedImage = new Mat();
        CvInvoke.Gemm(homography, translationHomography, 1, null, 1, homography);
        var result = new Mat(imagesToStitch[1].Size + new Size(200, 200), imagesToStitch[1].Depth, imagesToStitch[1].NumberOfChannels);
        CvInvoke.WarpPerspective(imagesToStitch[1], warpedImage, homography, imagesToStitch[1].Size + new Size(200, 200), Inter.Linear, Warp.InverseMap);
        CvInvoke.CopyMakeBorder(imagesToStitch[0], translatedSource, 100, 100, 100, 100, BorderType.Constant, default);
        warpedImage.CopyTo(result);
        ShowImage(warpedImage, "result1");
        ShowImage(translatedSource, "result2");
        translatedSource.CopyTo(result, translatedSource);
        ShowImage(result, "result3");
        CvInvoke.WaitKey();
        keyPoints.Dispose();
        keyPoints2.Dispose();
        matches.Dispose();
        mask.Dispose();
        homography?.Dispose();
        result.Dispose();
    }

    private static void ShowImage(Mat image, string name, float scale = 0.8f)
    {
        var displayImage = new Mat();
        CvInvoke.Resize(image, displayImage, default, scale, scale);
        CvInvoke.Imshow(name, displayImage);
        displayImage.Dispose();
    }
}
