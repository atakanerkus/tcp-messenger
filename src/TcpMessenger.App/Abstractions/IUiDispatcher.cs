namespace TcpMessenger.App.Abstractions;

/// <summary>
/// Marshals work onto the UI thread. Core raises its events on thread-pool threads; view models
/// route them through this so that bound collections and properties are only touched on the UI thread.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Runs the action on the UI thread: inline when already there, otherwise queued.</summary>
    void Post(Action action);
}
