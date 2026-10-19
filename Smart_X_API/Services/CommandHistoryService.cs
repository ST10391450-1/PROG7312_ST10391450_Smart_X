
namespace Smart_X_API.Services;

public sealed record ReversibleCommand(
    Guid CommandId,
    string NodeId,
    string Command,
    bool PreviousState,
    bool NewState,
    DateTime CreatedAt);

public sealed class CommandHistoryService
{
    private readonly object _sync = new();
    private readonly Stack<ReversibleCommand> _undoStack = new();

    public void Push(ReversibleCommand command)
    {
        lock (_sync)
        {
            _undoStack.Push(command);

            if (_undoStack.Count > 500)
            {
                var retained = _undoStack
                    .Take(500)
                    .Reverse()
                    .ToArray();

                _undoStack.Clear();

                foreach (var item in retained)
                    _undoStack.Push(item);
            }
        }
    }

    public ReversibleCommand? Peek()
    {
        lock (_sync)
            return _undoStack.TryPeek(out var command) ? command : null;
    }

    public ReversibleCommand? Pop()
    {
        lock (_sync)
            return _undoStack.TryPop(out var command) ? command : null;
    }

    public int Count
    {
        get
        {
            lock (_sync)
                return _undoStack.Count;
        }
    }
}
