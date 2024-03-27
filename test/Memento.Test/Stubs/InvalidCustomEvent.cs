using Memento.Events;

namespace Memento.Test.Stubs;

internal class InvalidCustomEvent : BaseEvent
{
    public BatchEvent Batch { get; }

    public InvalidCustomEvent(BatchEvent batch)
    {
        Batch = batch;
    }

    protected override Task<BaseEvent> Rollback(CancellationToken cancellationToken)
    {
        return Task.FromResult<BaseEvent>(Batch);
    }
}