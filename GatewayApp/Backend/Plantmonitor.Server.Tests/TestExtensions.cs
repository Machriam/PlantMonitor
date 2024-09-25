using System.Runtime.CompilerServices;
using Emgu.CV;
using Plantmonitor.Server.Features.DeviceConfiguration;

namespace Plantmonitor.Server.Tests.Features.AutomaticPhotoTourTests
{
    public static class TestExtensions
    {
        public static void ShowImage(this IManagedMat mat, string name, int timeout = 1000, [CallerFilePath] string callerFile = "", [CallerMemberName] string caller = "")
        {
            var build = CvInvoke.BuildInformation;
            var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
            var cvOut = $"{applicationPath}/PlantMonitorControl.Tests/CvOut";
            Directory.CreateDirectory(cvOut);
            var file = Path.GetFileNameWithoutExtension(callerFile);
            var resultFile = $"{cvOut}/{file}_{caller}_{name}.png";
            File.WriteAllBytes(resultFile, mat.BytesFromMat());
            if (build.Contains("WIN32UI"))
            {
                mat.Execute(x =>
                {
                    CvInvoke.Imshow(resultFile, x);
                    CvInvoke.WaitKey(timeout);
                });
            }
        }
    }
}
