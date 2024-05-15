using System.Diagnostics;
using System.Text;
using Serilog;

namespace PlantMonitorControl.Features.ImageTaking;

public static class ProcessExtensions
{
    public static async Task RunProcess(this Process process, string program, string arguments)
    {
        Log.Logger.Information("Starting process {program} with {arguments}", program, arguments);
        process.StartInfo = new ProcessStartInfo(program, arguments);
        process.Start();
        await process.WaitForExitAsync();
    }

    public static async Task<string> GetProcessStdout(this Process process, string program, string arguments)
    {
        Log.Logger.Information("Starting process {program} with {arguments}", program, arguments);
        process.StartInfo = new ProcessStartInfo(program, arguments)
        {
            RedirectStandardOutput = true
        };
        StringBuilder result = new();
        process.OutputDataReceived += (sender, args) => result.AppendLine(args.Data);
        process.BeginOutputReadLine();
        process.Start();
        await process.WaitForExitAsync();
        return result.ToString();
    }
}
