using Emgu.CV;
using Emgu.CV.CvEnum;

namespace Plantmonitor.ImageWorker;

public static class MatExtensions
{
    public static IManagedMat AsManaged(this Mat mat)
    {
        return new ManagedMat(mat);
    }
}
