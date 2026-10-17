using System.Net;
using System.Net.Sockets;

namespace TcpMessenger.Tests.TestUtilities;

/// <summary>
/// Hands out a currently free TCP port for tests that must agree on a port across processes
/// (the e2e tests launch two application instances). In-process tests should prefer port 0.
/// </summary>
internal static class PortAllocator
{
    public static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try
        {
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }
        finally
        {
            probe.Stop();
        }
    }
}
