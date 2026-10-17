using System.Text;

namespace TcpMessenger.Protocol.Serialization;

/// <summary>
/// Fixed-width string fields: UTF-8, right-padded with 0x00, trailing 0x00 stripped on read.
/// UTF-8 keeps Turkish characters intact; the fixed width keeps the message size constant.
/// </summary>
public static class FixedString
{
    /// <summary>UTF-8 without BOM; invalid sequences decode to U+FFFD instead of throwing.</summary>
    public static Encoding Encoding { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    /// <summary>Number of bytes the value occupies once encoded.</summary>
    public static int GetByteCount(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Encoding.GetByteCount(value);
    }

    /// <summary>True when the encoded value fits into a field of the given size.</summary>
    public static bool Fits(string value, int size) => GetByteCount(value) <= size;

    /// <summary>Encodes the value into the destination and zero-fills the remainder.</summary>
    /// <exception cref="MessageFormatException">The encoded value is longer than the destination.</exception>
    public static void Write(Span<byte> destination, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        int byteCount = Encoding.GetByteCount(value);
        if (byteCount > destination.Length)
        {
            throw new MessageFormatException(
                $"'{value}' needs {byteCount} bytes but the field holds {destination.Length}.");
        }

        int written = Encoding.GetBytes(value, destination);
        destination[written..].Clear();
    }

    /// <summary>Decodes the value, ignoring trailing 0x00 padding.</summary>
    public static string Read(ReadOnlySpan<byte> source)
    {
        int end = source.Length;
        while (end > 0 && source[end - 1] == 0)
        {
            end--;
        }

        return Encoding.GetString(source[..end]);
    }
}
