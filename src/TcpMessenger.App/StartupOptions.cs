using System.Globalization;
using TcpMessenger.Core.Session;

namespace TcpMessenger.App;

/// <summary>
/// Command-line presets so that a demo or an end-to-end test can open the window with the form
/// already filled in. Nothing is started automatically; the user (or the test) still clicks Başlat.
/// </summary>
/// <example><c>TcpMessenger.App.exe --mode server --port 5000 --autoreply on</c></example>
public sealed record StartupOptions(
    SessionMode Mode = SessionMode.Server,
    string Host = "127.0.0.1",
    int Port = 5000,
    bool AutoReply = true)
{
    /// <summary>Parses <c>--mode server|client</c>, <c>--host</c>, <c>--port</c>, <c>--autoreply on|off</c>. Unknown or malformed values keep the default.</summary>
    public static StartupOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var options = new StartupOptions();
        for (int i = 0; i + 1 < args.Count; i++)
        {
            string key = args[i].ToLowerInvariant();
            string value = args[i + 1];

            switch (key)
            {
                case "--mode" when value.Equals("server", StringComparison.OrdinalIgnoreCase):
                    options = options with { Mode = SessionMode.Server };
                    i++;
                    break;
                case "--mode" when value.Equals("client", StringComparison.OrdinalIgnoreCase):
                    options = options with { Mode = SessionMode.Client };
                    i++;
                    break;
                case "--host":
                    options = options with { Host = value };
                    i++;
                    break;
                case "--port" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port):
                    options = options with { Port = port };
                    i++;
                    break;
                case "--autoreply":
                    options = options with { AutoReply = value.Equals("on", StringComparison.OrdinalIgnoreCase) || value == "1" };
                    i++;
                    break;
                default:
                    break;
            }
        }

        return options;
    }
}
