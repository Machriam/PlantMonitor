using System.Runtime.CompilerServices;
using Emgu.CV;
using Plantmonitor.Server.Features.DeviceConfiguration;

namespace Plantmonitor.Server.Tests.Features.AutomaticPhotoTourTests
{
    public static class TestExtensions
    {
        public static void ShowImage(this Mat mat, string name, int timeout = 1000, [CallerFilePath] string callerFile = "", [CallerMemberName] string caller = "")
        {
            var build = CvInvoke.BuildInformation;
            var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
            var cvOut = $"{applicationPath}/PlantMonitorControl.Tests/CvOut";
            Directory.CreateDirectory(cvOut);
            var file = Path.GetFileNameWithoutExtension(callerFile);
            var resultFile = $"{cvOut}/{file}_{caller}_{name}.png";
            CvInvoke.Imwrite(resultFile, mat);
            if (build.Contains("WIN32UI"))
            {
                CvInvoke.Imshow(resultFile, mat);
                CvInvoke.WaitKey(timeout);
            }
        }
    }
}
