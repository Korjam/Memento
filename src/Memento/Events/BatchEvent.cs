﻿using System.Collections;

namespace Memento.Events;

/// <summary>
/// Represents a batch of events.
/// </summary>
public sealed class BatchEvent : BaseEvent, IEnumerable<BaseEvent>
{
    private readonly Stack<BaseEvent> _events;

    internal BatchEvent(BatchEvent? other = null)
    {
        _events = other == null
            ? new Stack<BaseEvent>()
            : new Stack<BaseEvent>(other._events.Reverse());
    }

    /// <summary>
    /// The number of events in this batch.
    /// </summary>
    public int Count => _events.Count;

    internal BaseEvent Pop() => _events.Pop();

    internal void Push(BaseEvent @event) => _events.Push(@event);

    internal void Clear() => _events.Clear();

    protected internal override async Task<BaseEvent> Rollback(CancellationToken cancellationToken)
    {
        var batch = new BatchEvent();

        while (Count > 0)
        {
            var reverse = await Pop().Rollback(cancellationToken);
            if (reverse == null)
            {
                continue;
            }

            if (reverse is BatchEvent)
            {
                throw new InvalidOperationException("Must not return BatchEvent in Rollback()");
            }

            batch.Push(reverse);
        }

        return batch;
    }

    /// <summary>
    /// Returns the enumerator to access child events.
    /// </summary>
    /// <returns>The enumerator to access child events.</returns>
    public IEnumerator<BaseEvent> GetEnumerator() => _events.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}