namespace Memento.Events;

/// <summary>
/// Represents a collection element addition event.
/// </summary>
public sealed class ElementAdditionEvent<T> : BaseEvent
{
    /// <summary>
    /// The collection this event occurs on.
    /// </summary>
    public IList<T> Collection { get; }

    /// <summary>
    /// The element added.
    /// </summary>
    public T Element { get; }

    /// <summary>
    /// Creates the event.
    /// </summary>
    /// <param name="collection">The collection object.</param>
    /// <param name="element">The element added.</param>
    public ElementAdditionEvent(IList<T> collection, T element)
    {
        Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        Element = element;
    }

    protected internal override BaseEvent Rollback()
    {
        var reverse = new ElementRemovalEvent<T>(Collection, Element);
        Collection.Remove(Element);
        return reverse;
    }
}