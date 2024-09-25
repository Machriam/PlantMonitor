using Emgu.CV;
using Emgu.CV.CvEnum;

namespace Plantmonitor.Server;

public static class MatExtensions
{
    public static IManagedMat AsManaged(this Mat mat)
    {
        return new ManagedMat(mat);
    }
}
