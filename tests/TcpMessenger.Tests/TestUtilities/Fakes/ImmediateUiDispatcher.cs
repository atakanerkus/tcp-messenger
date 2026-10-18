using TcpMessenger.App.Abstractions;

namespace TcpMessenger.Tests.TestUtilities.Fakes;

/// <summary><see cref="IUiDispatcher"/> that runs everything inline; view model tests have no UI thread.</summary>
internal sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public int PostCount { get; private set; }

    public void Post(Action action)
    {
        PostCount++;
        action();
    }

    public void PostBackground(Action action) => Post(action);
}
