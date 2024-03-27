using Memento.Events;

namespace Memento;

/// <summary>
/// Provides undo and redo services.
/// </summary>
public sealed class Mementor : IDisposable
{
    /// <summary>
    /// Fired after an undo or redo is performed.
    /// </summary>
    public event MementorChanged? Changed;

    private readonly BatchEvent _undoStack = new();
    private readonly BatchEvent _redoStack = new();
    private BatchEvent? _currentBatch;

    /// <summary>
    /// Creates an instance of <see cref="Mementor"/>.
    /// </summary>
    /// <param name="isEnabled">Whether this instance is enabled or not.</param>
    public Mementor(bool isEnabled = true)
    {
        IsTrackingEnabled = isEnabled;
    }

    #region Core

    /// <summary>
    /// Marks an event. This method also serves as an extensibility point for custom events.
    /// </summary>
    /// <param name="event">The event to be marked.</param>
    public void MarkEvent(BaseEvent @event)
    {
        if (!IsTrackingEnabled)
        {
            return;
        }
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        (_currentBatch ?? _undoStack).Push(@event);

        if (!IsInBatch)
        {
            PerformPostMarkAction(@event);
        }
    }

    /// <summary>
    /// Marks a batch during which all events are combined so that <see cref="Undo"/> only needs calling once.
    /// </summary>
    /// <param name="codeBlock">The code block performing batch change marking.</param>
    /// <seealso cref="BeginBatch"/>
    /// <remarks>Batches cannot be nested. At any point, there must be only one active batch.</remarks>
    public void Batch(Action codeBlock)
    {
        if (codeBlock == null)
        {
            throw new ArgumentNullException(nameof(codeBlock));
        }
        if (!IsTrackingEnabled)
        {
            codeBlock();
            return;
        }

        BeginBatch();

        try
        {
            codeBlock();
        }
        finally
        {
            // Must not call EndBatch() because CheckPreconditions() might return false
            var @event = InternalEndBatch(_undoStack);
            if (@event != null)
            {
                PerformPostMarkAction(@event);
            }
        }
    }

    /// <summary>
    /// Marks a batch during which all events are combined so that <see cref="Undo"/> only needs calling once.
    /// </summary>
    /// <param name="codeBlock">The code block performing batch change marking.</param>
    /// <seealso cref="BeginBatch"/>
    /// <remarks>Batches cannot be nested. At any point, there must be only one active batch.</remarks>
    public T Batch<T>(Func<T> codeBlock)
    {
        if (codeBlock == null)
        {
            throw new ArgumentNullException(nameof(codeBlock));
        }
        if (!IsTrackingEnabled)
        {
            return codeBlock();
        }

        BeginBatch();

        try
        {
            return codeBlock();
        }
        finally
        {
            // Must not call EndBatch() because CheckPreconditions() might return false
            var @event = InternalEndBatch(_undoStack);
            if (@event != null)
            {
                PerformPostMarkAction(@event);
            }
        }
    }

    /// <summary>
    /// Marks a batch during which all events are combined so that <see cref="Undo"/> only needs calling once.
    /// </summary>
    /// <param name="codeBlock">The code block performing batch change marking.</param>
    /// <seealso cref="BeginBatch"/>
    /// <remarks>Batches cannot be nested. At any point, there must be only one active batch.</remarks>
    public async Task BatchAsync(Func<Task> codeBlock)
    {
        if (codeBlock == null)
        {
            throw new ArgumentNullException(nameof(codeBlock));
        }
        if (!IsTrackingEnabled)
        {
            await codeBlock();
            return;
        }

        BeginBatch();

        try
        {
            await codeBlock();
        }
        finally
        {
            // Must not call EndBatch() because CheckPreconditions() might return false
            var @event = InternalEndBatch(_undoStack);
            if (@event != null)
            {
                PerformPostMarkAction(@event);
            }
        }
    }

    /// <summary>
    /// Marks a batch during which all events are combined so that <see cref="Undo"/> only needs calling once.
    /// </summary>
    /// <param name="codeBlock">The code block performing batch change marking.</param>
    /// <seealso cref="BeginBatch"/>
    /// <remarks>Batches cannot be nested. At any point, there must be only one active batch.</remarks>
    public async Task<T> BatchAsync<T>(Func<Task<T>> codeBlock)
    {
        if (codeBlock == null)
        {
            throw new ArgumentNullException(nameof(codeBlock));
        }
        if (!IsTrackingEnabled)
        {
            return await codeBlock();
        }

        BeginBatch();

        try
        {
            return await codeBlock();
        }
        finally
        {
            // Must not call EndBatch() because CheckPreconditions() might return false
            var @event = InternalEndBatch(_undoStack);
            if (@event != null)
            {
                PerformPostMarkAction(@event);
            }
        }
    }

    /// <summary>
    /// Explicitly marks the beginning of a batch. Use this instead of <see cref="Batch"/>
    /// changes can be made in different places instead of inside one certain block of code.
    /// When finish, end the batch by invoking <see cref="EndBatch"/>.
    /// </summary>
    public void BeginBatch()
    {
        if (!IsTrackingEnabled)
        {
            return;
        }
        if (IsInBatch)
        {
            throw new InvalidOperationException("Re-entrant batch is not supported");
        }

        _currentBatch = new BatchEvent();
    }

    /// <summary>
    /// Ends a batch.
    /// </summary>
    public void EndBatch()
    {
        if (!IsTrackingEnabled)
        {
            return;
        }
        if (!IsInBatch)
        {
            throw new InvalidOperationException("A batch has not been started yet");
        }

        var @event = InternalEndBatch(_undoStack);
        if (@event != null)
        {
            PerformPostMarkAction(@event);
        }
    }

    /// <summary>
    /// Executes the supplied code block with <see cref="IsTrackingEnabled"/> turned off.
    /// </summary>
    /// <param name="codeBlock">The code block to be executed.</param>
    /// <seealso cref="IsTrackingEnabled"/>
    public void ExecuteNoTrack(Action codeBlock)
    {
        var previousState = IsTrackingEnabled;
        IsTrackingEnabled = false;
        try
        {
            codeBlock();
        }
        finally
        {
            IsTrackingEnabled = previousState;
        }
    }

    /// <summary>
    /// Executes the supplied code block with <see cref="IsTrackingEnabled"/> turned off.
    /// </summary>
    /// <param name="codeBlock">The code block to be executed.</param>
    /// <seealso cref="IsTrackingEnabled"/>
    public async Task ExecuteNoTrackAsync(Func<Task> codeBlock)
    {
        var previousState = IsTrackingEnabled;
        IsTrackingEnabled = false;
        try
        {
            await codeBlock();
        }
        finally
        {
            IsTrackingEnabled = previousState;
        }
    }

    /// <summary>
    /// Performs an undo.
    /// </summary>
    public async Task Undo(CancellationToken cancellationToken = default)
    {
        if (!CanUndo)
        {
            throw new InvalidOperationException("There is nothing to undo");
        }
        if (IsInBatch)
        {
            throw new InvalidOperationException("Finish the active batch first");
        }

        var @event = _undoStack.Pop();
        await RollbackEvent(@event switch
        {
            BatchEvent batch => new BatchEvent(batch),
            _ => @event,
        }, true, cancellationToken);
        NotifyChange(@event);
    }

    /// <summary>
    /// Performs a redo.
    /// </summary>
    public async Task Redo(CancellationToken cancellationToken = default)
    {
        if (!CanRedo)
        {
            throw new InvalidOperationException("There is nothing to redo");
        }
        if (IsInBatch)
        {
            throw new InvalidOperationException("Finish the active batch first");
        }

        var @event = _redoStack.Pop();
        await RollbackEvent(@event switch
        {
            BatchEvent batch => new BatchEvent(batch),
            _ => @event,
        }, false, cancellationToken);
        NotifyChange(@event);
    }

    /// <summary>
    /// Returns <c>true</c> if can undo.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Returns <c>true</c> if can redo.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// How many undos in the stack.
    /// </summary>
    public int UndoCount => _undoStack.Count;

    /// <summary>
    /// How many redos in the stack.
    /// </summary>
    public int RedoCount => _redoStack.Count;

    /// <summary>
    /// If <c>true</c>, all calls to mark changes are ignored.
    /// </summary>
    /// <seealso cref="ExecuteNoTrack"/>
    public bool IsTrackingEnabled { get; set; }

    /// <summary>
    /// Returns <c>true</c> if a batch is being already begun but not ended.
    /// </summary>
    public bool IsInBatch => _currentBatch != null;

    /// <summary>
    /// Resets the state of this <see cref="Mementor"/> object to its initial state.
    /// This effectively clears the redo stack, undo stack and current batch (if one is active).
    /// </summary>
    public void Reset()
    {
        var shouldNotify = UndoCount > 0 || RedoCount > 0;

        _undoStack.Clear();
        _redoStack.Clear();

        _currentBatch = null;

        IsTrackingEnabled = true;

        if (shouldNotify)
        {
            NotifyChange(null);
        }
    }

    /// <summary>
    /// Disposes the this mementor and clears redo and undo stacks.
    /// This method won't fire <see cref="Changed"/> event.
    /// </summary>
    public void Dispose()
    {
        if (Changed is not null)
        {
            foreach (var changedEvent in Changed.GetInvocationList().Cast<MementorChanged>())
            {
                Changed -= changedEvent;
            }
        }

        _undoStack.Clear();
        _redoStack.Clear();

        _currentBatch = null;
    }

    #endregion

    #region Private

    private async Task RollbackEvent(BaseEvent @event, bool undoing, CancellationToken cancellationToken)
    {
        await ExecuteNoTrackAsync(async () =>
        {
            var reverse = await @event.Rollback(cancellationToken);
            if (reverse == null)
            {
                return;
            }

            if (reverse is BatchEvent batch)
            {
                if (@event is not BatchEvent)
                {
                    throw new InvalidOperationException("Must not return BatchEvent in Rollback()");
                }

                reverse = ProcessBatch(batch);
                if (reverse == null)
                {
                    return;
                }
            }

            (undoing ? _redoStack : _undoStack).Push(reverse);
        });
    }

    private BaseEvent? InternalEndBatch(BatchEvent stack)
    {
        var processed = ProcessBatch(_currentBatch);

        if (processed != null)
        {
            stack.Push(processed);
        }

        _currentBatch = null;

        return processed;
    }

    private static BaseEvent? ProcessBatch(BatchEvent? batchEvent)
    {
        if (batchEvent is null)
        {
            return null;
        }

        return batchEvent.Count switch
        {
            0 => null,
            1 => batchEvent.Pop(),
            _ => batchEvent
        };
    }

    private void PerformPostMarkAction(BaseEvent @event)
    {
        _redoStack.Clear();
        NotifyChange(@event);
    }

    private void NotifyChange(BaseEvent? @event)
    {
        Changed?.Invoke(this, new MementorChangedEventArgs(@event));
    }

    #endregion
}