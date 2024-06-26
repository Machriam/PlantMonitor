using NSubstitute;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.ImageStitching;
using System;
using System.Diagnostics;
using Xunit;

namespace Plantmonitor.Server.Tests.Features.ImageStitching
{
    public class PhotoStitcherTests : IDisposable
    {
        public PhotoStitcherTests()
        {
        }

        private PhotoStitcher CreatePhotoStitcher()
        {
            return new PhotoStitcher();
        }

        [Fact]
        public void StitchPhotos_StateUnderTest_ExpectedBehavior()
        {
            var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
            var sut = CreatePhotoStitcher();
            var pictureFolder = $"{applicationPath}/PlantMonitorControl.Tests/TestData/HomographyTest";
            sut.StitchPhotos(pictureFolder);
        }

        [Fact]
        public void StitchPhotosManual_StateUnderTest_ExpectedBehavior()
        {
            var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
            var sut = CreatePhotoStitcher();
            var pictureFolder = $"{applicationPath}/PlantMonitorControl.Tests/TestData/HomographyTest";
            sut.StitchPhotosManual(pictureFolder);
        }

        public void Dispose()
        {
            foreach (var process in Process.GetProcessesByName("testhost"))
            {
                process.Kill();
            }
        }
    }
}
