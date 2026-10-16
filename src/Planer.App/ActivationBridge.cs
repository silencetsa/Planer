using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace Planer_App;

internal enum DesktopActivation : byte { Week = 1, Today = 2, Habits = 3, CheckNotifications = 4 }

/// <summary>Only a fixed, single-byte command can cross this current-user pipe; no paths or data are accepted.</summary>
internal sealed class ActivationBridge : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    internal static string KeyForDatabase(string path) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant())))[..24];
    public ActivationBridge(string key, Func<DesktopActivation, Task> dispatch) => _ = Listen(key, dispatch);
    private async Task Listen(string key, Func<DesktopActivation, Task> dispatch)
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream("Planer-" + key, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_stop.Token);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token); deadline.CancelAfter(TimeSpan.FromSeconds(10));
                var command = new byte[1];
                if (await server.ReadAsync(command, deadline.Token) == 1 && Enum.IsDefined((DesktopActivation)command[0]))
                {
                    await dispatch((DesktopActivation)command[0]);
                    await server.WriteAsync(new byte[] { 1 }, deadline.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }
    }
    internal static async Task<bool> Forward(string key, DesktopActivation activation)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", "Planer-" + key, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await client.ConnectAsync(deadline.Token); await client.WriteAsync(new[] { (byte)activation }, deadline.Token);
            var reply = new byte[1]; return await client.ReadAsync(reply, deadline.Token) == 1 && reply[0] == 1;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or TimeoutException) { return false; }
    }
    public void Dispose() => _stop.Cancel();
}
