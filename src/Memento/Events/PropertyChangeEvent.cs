using System.Reflection;

namespace Memento.Events;

/// <summary>
/// Represents a property value change event.
/// </summary>
public sealed class PropertyChangeEvent : BaseEvent
{
    /// <summary>
    /// The target object this event occurs on.
    /// </summary>
    public object TargetObject { get; }

    /// <summary>
    /// The name of the changed property.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// The value to be restored to when undo.
    /// </summary>
    public object? PropertyValue { get; }

    /// <summary>
    /// Creates the event.
    /// </summary>
    /// <param name="target">The target object whose property is changed.</param>
    /// <param name="propertyName">The name of the property being changed.</param>
    /// <param name="propertyValue">The value of the property. If not supplied, use the current value of <paramref name="propertyName"/> in <paramref name="target"/></param>
    public PropertyChangeEvent(object target, string propertyName, object? propertyValue = null)
    {
        TargetObject = target ?? throw new ArgumentNullException(nameof(target));
        PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        PropertyValue = propertyValue ?? PropertyInfo().GetValue(target, null);
    }

    protected internal override Task<BaseEvent> Rollback()
    {
        var reverse = new PropertyChangeEvent(TargetObject, PropertyName);
        PropertyInfo().SetValue(TargetObject, PropertyValue, null);
        return Task.FromResult<BaseEvent>(reverse);
    }

    private PropertyInfo PropertyInfo()
    {
        return TargetObject.GetType().GetProperty(PropertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
    }
}