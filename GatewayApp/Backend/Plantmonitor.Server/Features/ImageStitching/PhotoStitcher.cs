using Emgu.CV;
using Emgu.CV.Features2D;
using Emgu.CV.Stitching;
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
        var imagesToStitch = files.OrderBy(f => f).Select(f => new Mat(f)).ToList();
        var stitcher = new Stitcher(Stitcher.Mode.Scans);
        var result = imagesToStitch[0];
        for (var i = 1; i < imagesToStitch.Count; i++)
        {
            var input = new VectorOfMat(result, imagesToStitch[i]);
            stitcher.Stitch(input, result);
        }

        result.Save(folderName + "/../result.png");
        foreach (var image in imagesToStitch) image.Dispose();
        result.Dispose();
        stitcher.Dispose();
    }

    /*
    Features Finder: Utilizes ORB (Oriented FAST and Rotated BRIEF) for feature detection. The number of features to find can be adjusted.
Features Matcher: Uses an affine matcher with specific parameters to match features between images.
Estimator: Employs a homography-based estimator for initial camera estimation.
Bundle Adjuster: Adjusts the camera parameters globally using the Ray method.
Warper: Applies a spherical warping to the images.
Exposure Compensator: Compensates for exposure differences using the Gain Blocks method.
Seam Finder: Finds the optimal seams with the DpSeamFinder using color gradients as the cost function.
Blender: Blends images together using a multi-band blender for seamless stitching.
     * */

    public void StitchPhotos2(string folderName)
    {
        var files = Directory.GetFiles(folderName).Take(2);
        var imagesToStitch = files.OrderBy(f => f).Select(f => new Mat(f)).ToList();

        // Features Finder
        var featuresFinder = new ORB();

        // Features Matcher
        var featuresMatcher = new AffineBestOf2NearestMatcher(false, false, 0.3f);

        // Estimator
        var estimator = new HomographyBasedEstimator(true);

        // Bundle Adjuster
        var adjuster = new BundleAdjusterRay();

        // Warper
        var warper = new SphericalWarper();

        // Exposure Compensator
        var compensator = new NoExposureCompensator();

        // Seam Finder
        var seamFinder = new DpSeamFinder(DpSeamFinder.CostFunction.ColorGrad);

        // Blender
        var blender = new MultiBandBlender();

        // Stitching Pipeline
        var stitcher = new Stitcher(Stitcher.Mode.Scans);
        stitcher.SetFeaturesFinder(featuresFinder);
        stitcher.SetFeaturesMatcher(featuresMatcher);
        stitcher.SetEstimator(estimator);
        stitcher.SetBundleAdjusterCompensator(adjuster);
        stitcher.SetWarper(warper);
        stitcher.SetExposureCompensator(compensator);
        stitcher.SetSeamFinder(seamFinder);
        stitcher.SetBlender(blender);

        //using (var result = new Mat())
        //{
        //    var status = stitcher.Stitch(imagesToStitch, result);

        //    if (status == Stitcher.Status.Ok)
        //    {
        //        result.Save(Path.Combine(folderName, "../result.png"));
        //        Console.WriteLine("Stitching completed successfully.");
        //    }
        //    else
        //    {
        //        Console.WriteLine($"Stitching failed: {status}");
        //    }
        //}

        // Cleanup
        foreach (var image in imagesToStitch) image.Dispose();
        featuresFinder.Dispose();
        featuresMatcher.Dispose();
        estimator.Dispose();
        adjuster.Dispose();
        warper.Dispose();
        compensator.Dispose();
        seamFinder.Dispose();
        blender.Dispose();
        stitcher.Dispose();
    }
}
