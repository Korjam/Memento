using Memento.Events;

namespace Memento.Test.Stubs;

internal class InvalidCustomEvent : BaseEvent
{
    public BatchEvent Batch { get; }

    public InvalidCustomEvent(BatchEvent batch)
    {
        Batch = batch;
    }

    protected override Task<BaseEvent> Rollback()
    {
        return Task.FromResult<BaseEvent>(Batch);
    }
}