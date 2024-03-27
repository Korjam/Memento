using Memento.Events;

namespace Memento.Test.Stubs;

internal class CustomEvent : BaseEvent
{
    public bool IsRolledback { get; private set; }
    public CustomEvent? ReverseEvent { get; private set; }

    public CustomEvent(CustomEvent? reverseEvent = null)
    {
        ReverseEvent = reverseEvent;
    }

    protected override BaseEvent Rollback()
    {
        IsRolledback = true;
        ReverseEvent ??= new CustomEvent(this);
        return ReverseEvent;
    }
}