using FluentAssertions;
using NSubstitute;
using PlantMonitorControl.Features.ImageTaking;
using System;
using Xunit;

namespace PlantMonitorControl.Tests.Features.ImageTaking
{
    public class ExposureSettingsEditorTests
    {
        private const string TestData = """
                        [1:23:09.766782619] [2185] ^[[1;32m INFO ^[[1;37mCamera ^[[1;34mcamera_manager.cpp:284 ^[[0mlibcamera v0.1.0+118-563cd78e
            [1:23:09.940533570] [2188] ^[[1;33m WARN ^[[1;37mRPiSdn ^[[1;34msdn.cpp:39 ^[[0mUsing legacy SDN tuning - please consider moving SDN inside rpi.denoise
            [1:23:09.946790782] [2188] ^[[1;32m INFO ^[[1;37mRPI ^[[1;34mvc4.cpp:444 ^[[0mRegistered camera /base/soc/i2c0mux/i2c@1/imx708@1a to Unicam device /dev/media3 and ISP device /dev/media0
            [1:23:09.946956824] [2188] ^[[1;32m INFO ^[[1;37mRPI ^[[1;34mpipeline_base.cpp:1142 ^[[0mUsing configuration file '/usr/share/libcamera/pipeline/rpi/vc4/rpi_apps.yaml'
            [1:23:10.232629157] [2188] ^[[1;33m WARN ^[[1;37mV4L2 ^[[1;34mv4l2_pixelformat.cpp:338 ^[[0mUnsupported V4L2 pixel format Y16
            Preview window unavailable
            Mode selection for 2304:1296:12:P
                SRGGB10_CSI2P,1536x864/0 - Score: 3400
                SRGGB10_CSI2P,2304x1296/0 - Score: 1000
                SRGGB10_CSI2P,4608x2592/0 - Score: 1900
            Stream configuration adjusted
            [1:23:10.237024539] [2185] ^[[1;32m INFO ^[[1;37mCamera ^[[1;34mcamera.cpp:1183 ^[[0mconfiguring streams: (0) 2304x1296-YUV420 (1) 2304x1296-SBGGR10_CSI2P
            [1:23:10.237684229] [2188] ^[[1;32m INFO ^[[1;37mRPI ^[[1;34mvc4.cpp:608 ^[[0mSensor: /base/soc/i2c0mux/i2c@1/imx708@1a - Selected sensor format: 2304x1296-SBGGR10_1X10 - Selected unicam format: 2304x1296-pBAA
            #0 (0.00 fps) exp 240.00 ag 1.12 dg 1.04
            #1 (30.01 fps) exp 240.00 ag 1.12 dg 1.04
            #2 (30.01 fps) exp 240.00 ag 1.12 dg 1.04
            #3 (30.01 fps) exp 240.00 ag 1.12 dg 1.04
            #4 (30.01 fps) exp 240.00 ag 1.12 dg 1.04
            #5 (30.01 fps) exp 240.00 ag 1.12 dg 1.04
            #6 (30.01 fps) exp 240.00 ag 1.12 dg 1.04
            #7 (30.01 fps) exp 240.00 ag 1.12 dg 1.04
            #8 (30.01 fps) exp 240.00 ag 1.12 dg 1.04
            #9 (30.01 fps) exp 320.00 ag 1.20 dg 1.04
            """;

        private ExposureSettingsEditor CreateExposureSettingsEditor()
        {
            return new ExposureSettingsEditor();
        }

        [Fact]
        public void GetExposureFromStdout_ShouldWork()
        {
            var sut = CreateExposureSettingsEditor();
            var result = sut.GetExposureFromStdout(TestData);
            result.Should().BeEquivalentTo(new ExposureSettings()
            {
                ExposureTimeInMicroSeconds = 240,
                Gain = 1.16f
            });
        }
    }
}
