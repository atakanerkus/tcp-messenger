using System.Globalization;
using System.Text;

namespace TcpMessenger.App.Services;

/// <summary>Classic 16-bytes-per-line hex dump for the detail pane.</summary>
public static class HexDump
{
    private const int BytesPerLine = 16;

    /// <summary>Formats the bytes as <c>offset  hex bytes  |ascii|</c> lines.</summary>
    public static string Format(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (int offset = 0; offset < bytes.Length; offset += BytesPerLine)
        {
            var line = bytes.Slice(offset, Math.Min(BytesPerLine, bytes.Length - offset));

            builder.Append(offset.ToString("X4", CultureInfo.InvariantCulture)).Append("  ");

            for (int i = 0; i < BytesPerLine; i++)
            {
                if (i < line.Length)
                {
                    builder.Append(line[i].ToString("X2", CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append("  ");
                }

                builder.Append(i == 7 ? "  " : " ");
            }

            builder.Append(" |");
            foreach (byte b in line)
            {
                builder.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
            }

            builder.Append('|');

            if (offset + BytesPerLine < bytes.Length)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}
