namespace TcpMessenger.Tests.E2E;

/// <summary>
/// UI automation drives real windows and the keyboard focus; running scenarios in parallel would
/// make them interfere with each other, so the whole category runs sequentially.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class E2ECollection
{
    public const string Name = "E2E";
}
