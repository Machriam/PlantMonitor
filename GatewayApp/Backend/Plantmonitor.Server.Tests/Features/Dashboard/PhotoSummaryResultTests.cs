using Emgu.CV;
using NSubstitute;
using Plantmonitor.Server.Features.AutomaticPhotoTour;
using Plantmonitor.Server.Features.Dashboard;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Tests.Features.AutomaticPhotoTourTests;
using Plantmonitor.Shared.Extensions;
using System;
using System.Reflection;
using Xunit;
using static Plantmonitor.Server.Features.Dashboard.PhotoSummaryResult;

namespace Plantmonitor.Server.Tests.Features.Dashboard;

public class PhotoSummaryResultTests
{
    private Mat CreateSubImage(PhotoSummaryResult sut, List<PixelInfo> pixelInfo)
    {
        var sutMethod = sut.GetType().GetMethod(nameof(CreateSubImage), BindingFlags.NonPublic | BindingFlags.Static);
        return (Mat?)sutMethod!.Invoke(sut, [pixelInfo]) ?? new Mat();
    }

    [Fact]
    public void CreateSubImage_ShouldWork()
    {
        var sut = new PhotoSummaryResult(0.2f);
        var path = Path.Combine(Directory.GetCurrentDirectory().GetApplicationRootGitPath()!, "PlantMonitorControl.Tests", "TestData", "PhotoTourSummaryTest", "St 3.json");
        var json = File.ReadAllText(path).FromJson<List<PixelInfo>>();
        var image = CreateSubImage(sut, json!).AsManaged();
        image.ShowImage("St 3");
    }
}
