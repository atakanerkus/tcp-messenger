using TcpMessenger.App.ViewModels;
using TcpMessenger.Core.Abstractions;
using TcpMessenger.Protocol.Abstractions;

namespace TcpMessenger.App.Services;

/// <summary>
/// <see cref="IOutgoingMessageProvider"/> backed by the message editors on screen: the automatic
/// reply and the periodic sender send whatever the user currently has in the form. Editors are
/// registered after construction because they themselves depend on the session.
/// </summary>
public sealed class FormMessageProvider : IOutgoingMessageProvider
{
    private readonly Dictionary<int, MessageEditorViewModelBase> _editors = [];

    /// <summary>Registers the editor responsible for a message type.</summary>
    public void Register(MessageEditorViewModelBase editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        _editors[editor.MessageId] = editor;
    }

    /// <inheritdoc />
    public IMessage? GetOutgoingMessage(int messageId) =>
        _editors.TryGetValue(messageId, out var editor) ? editor.CurrentMessage : null;
}
