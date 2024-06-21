using NSubstitute;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.ImageStitching;
using System;
using Xunit;

namespace Plantmonitor.Server.Tests.Features.ImageStitching
{
    public class PhotoStitcherTests
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
    }
}
