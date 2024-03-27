using FluentAssertions;
using Memento.Events;
using Memento.Test.Stubs;

namespace Memento.Test;

public class Features : IDisposable
{
    private Mementor m;

    public Features()
    {
        m = Session.New();
    }

    public void Dispose()
    {
        Session.End();
    }

    [Fact]
    public void Should_initialize_correctly()
    {
        UndoCount(0).RedoCount(0);
        m.IsTrackingEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Should_undo_redo_property_change()
    {
        var c = new Circle();
        for (int i = 0; i < 10; i++)
        {
            c.Radius = i + 1;
            UndoCount(i + 1).RedoCount(0);
        }
        for (int i = 9; i >= 0; i--)
        {
            await m.Undo();
            c.Radius.Should().Be(i);
            UndoCount(i).RedoCount(9 - i + 1);
        }
        for (int i = 0; i < 10; i++)
        {
            await m.Redo();
            c.Radius.Should().Be(i + 1);
            UndoCount(i + 1).RedoCount(9 - i);
        }
    }

    [Fact]
    public async Task Should_allow_provide_property_value()
    {
        var c = new Circle();
        UndoCount(0);
        m.PropertyChange(c, () => c.Radius, 10);
        await m.Undo();
        c.Radius.Should().Be(10);
    }

    [Fact]
    public async Task Should_undo_redo_complex_property_change()
    {
        var c = new Circle();
        for (int i = 0; i < 10; i++)
        {
            c.Center = new Point(i + 1, i + 1);
            UndoCount(i + 1).RedoCount(0);
        }
        for (int i = 9; i >= 0; i--)
        {
            await m.Undo();
            c.Center.Should().Be(new Point(i, i));
            UndoCount(i).RedoCount(9 - i + 1);
        }
        for (int i = 0; i < 10; i++)
        {
            await m.Redo();
            c.Center.Should().Be(new Point(i + 1, i + 1));
            UndoCount(i + 1).RedoCount(9 - i);
        }
    }

    [Fact]
    public async Task Should_undo_multiple_properties_change()
    {
        var c = new Circle { Radius = 10, Center = new Point(10, 10) };
        UndoCount(2);

        await m.Undo();
        c.Center.Should().Be(new Point(0, 0));
        UndoCount(1);

        await m.Undo();
        c.Radius.Should().Be(0);
        UndoCount(0);
    }

    [Fact]
    public void Should_reset_to_initial_states()
    {
        new Circle { Radius = 10, Center = new Point(10, 10) };
        UndoCount(2);

        m.Reset();
        UndoCount(0).RedoCount(0);
    }

    [Fact]
    public async Task Should_clear_redo_after_a_forward_change()
    {
        var c = new Circle { Radius = 10 };
        UndoCount(1).RedoCount(0);

        await m.Undo();
        UndoCount(0).RedoCount(1);

        c.Radius++;
        UndoCount(1).RedoCount(0);
    }

    [Fact]
    public async Task Should_be_able_to_piggy_back_undo_redo()
    {
        var c = new Circle { Radius = 10 };
        UndoCount(1).RedoCount(0);

        await m.Undo();
        c.Radius.Should().Be(0);
        UndoCount(0).RedoCount(1);

        await m.Redo();
        c.Radius.Should().Be(10);
        UndoCount(1).RedoCount(0);

        await m.Undo();
        c.Radius.Should().Be(0);
        UndoCount(0).RedoCount(1);

        await m.Redo();
        c.Radius.Should().Be(10);
        UndoCount(1).RedoCount(0);
    }

    [Fact]
    public async Task Should_undo_redo_whole_batch()
    {
        var circles = new Circle[10];
        for (int i = 0; i < circles.Length; i++)
        {
            circles[i] = new Circle();
        }

        m.Batch(() =>
        {
            foreach (Circle circle in circles)
            {
                circle.Radius = 5;
                circle.Center = new Point(5, 5);
            }
        });
        UndoCount(1);

        await m.Undo();
        foreach (Circle circle in circles)
        {
            circle.Radius.Should().Be(0);
            circle.Center.Should().Be(new Point(0, 0));
        }
        RedoCount(1);

        await m.Redo();
        foreach (Circle circle in circles)
        {
            circle.Radius.Should().Be(5);
            circle.Center.Should().Be(new Point(5, 5));
        }
    }

    [Fact]
    public void Should_throw_if_nesting_batches()
    {
        var action = () =>
        {
            m.Batch(() => m.Batch(() => new Circle() { Radius = 5 }));
        };
        action.Should().ThrowExactly<InvalidOperationException>();
    }

    [Fact]
    public void Should_throw_if_nesting_batches_via_explicit_calls()
    {
        var action = () =>
        {
            m.BeginBatch();
            m.BeginBatch();
            m.EndBatch();
        };
        action.Should().ThrowExactly<InvalidOperationException>();
    }

    [Fact]
    public void Should_throw_if_end_batch_without_starting_one()
    {
        var action = () =>
        {
            m.EndBatch();
        };
        action.Should().ThrowExactly<InvalidOperationException>();
    }

    [Fact]
    public void Should_not_throw_if_end_batch_after_starting_one()
    {
        for (var i = 0; i < 10; i++)
        {
            m.BeginBatch();
            m.EndBatch();
        }
    }

    [Fact]
    public void Should_track_based_on_enabling_setting()
    {
        m.IsTrackingEnabled = false;
        new Circle { Radius = 5 };
        UndoCount(0);

        m.IsTrackingEnabled = true;
        new Circle { Radius = 5 };
        UndoCount(1);
    }

    [Fact]
    public void Should_not_track_during_a_none_tracking_execution()
    {
        m.IsTrackingEnabled.Should().BeTrue();
        m.ExecuteNoTrack(() =>
        {
            m.IsTrackingEnabled.Should().BeFalse();
            new Circle { Radius = 5, Center = new Point(5, 5) };
        });
        m.IsTrackingEnabled.Should().BeTrue();
        UndoCount(0);
    }

    [Fact]
    public void Should_allow_nested_disabling_tracking()
    {
        m.IsTrackingEnabled.Should().BeTrue();
        m.ExecuteNoTrack(() =>
        {
            new Circle { Radius = 5, Center = new Point(5, 5) };
            m.ExecuteNoTrack(() => { new Circle { Radius = 5, Center = new Point(5, 5) }; });
        });
        m.IsTrackingEnabled.Should().BeTrue();
        UndoCount(0);
    }

    [Fact]
    public void Should_restore_to_previous_tracking_states_in_a_recursive_manner()
    {
        m.IsTrackingEnabled = false;
        m.ExecuteNoTrack(() =>
        {
            m.IsTrackingEnabled = true;
            m.ExecuteNoTrack(() => { });
            m.IsTrackingEnabled.Should().BeTrue();
        });
        m.IsTrackingEnabled.Should().BeFalse();
    }

    [Fact]
    public void Should_allow_temporary_enabling_during_no_track_context()
    {
        m.ExecuteNoTrack(() =>
        {
            var c = new Circle { Radius = 5 };
            m.IsTrackingEnabled = true;
            c.Radius++;
            m.IsTrackingEnabled = false;
            c.Radius++;
        });
        UndoCount(1);
    }

    [Fact]
    public async Task Should_undo_redo_collection_addition()
    {
        var screen = new Screen();
        var circle = new Circle();
        screen.Add(circle);
        UndoCount(1);

        await m.Undo();
        screen.Shapes.Should().BeEmpty();

        await m.Redo();
        screen.Shapes.Should().ContainSingle().And.Contain(circle);
    }

    [Fact]
    public async Task Should_undo_redo_collection_removal()
    {
        var screen = new Screen();
        var circle = new Circle();
        screen.Add(circle);
        m.Reset();

        screen.Remove(circle);
        UndoCount(1);

        await m.Undo();
        screen.Shapes.Should().ContainSingle().And.Contain(circle);

        await m.Redo();
        screen.Shapes.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_undo_redo_collection_position_change()
    {
        var screen = new Screen();
        Circle circle1, circle2;
        screen.Add(circle1 = new Circle());
        screen.Add(circle2 = new Circle());
        m.Reset();

        screen.MoveToFront(1);
        screen.Shapes.Should().ContainInOrder(circle2, circle1);

        await m.Undo();
        screen.Shapes.Should().ContainInOrder(circle1, circle2);

        await m.Redo();
        screen.Shapes.Should().ContainInOrder(circle2, circle1);
    }

    [Fact]
    public void Should_throw_when_removing_non_existent_element()
    {
        var action = () =>
        {
            var screen = new Screen();
            screen.Remove(new Circle());
        };
        action.Should().ThrowExactly<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Should_throw_when_changing_position_of_non_existent_element()
    {
        var action = () =>
        {
            var screen = new Screen();
            screen.Add(new Circle());
            m.ElementIndexChange(screen.Shapes, new Circle());
        };
        action.Should().ThrowExactly<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Should_undo_redo_collection_changes_in_batch()
    {
        var screen = new Screen();
        m.Batch(() =>
        {
            var circle = new Circle();
            screen.Add(new Circle { Radius = 10 });
            screen.Add(circle);
            screen.MoveToFront(1);
            screen.Remove(circle);
        });
        screen.Shapes.Should().ContainSingle();
        UndoCount(1);

        await m.Undo();
        screen.Shapes.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_undo_redo_collection_changes_in_explicit_batch()
    {
        var screen = new Screen();
        m.BeginBatch();
        try
        {
            var circle = new Circle();
            screen.Add(new Circle { Radius = 10 });
            screen.Add(circle);
            screen.MoveToFront(1);
            screen.Remove(circle);
        }
        finally
        {
            m.EndBatch();
        }
        screen.Shapes.Should().ContainSingle();
        UndoCount(1);

        await m.Undo();
        screen.Shapes.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_fire_events()
    {
        int count = 0;
        m.Changed += (_, args) => count++;

        var circle = new Circle { Radius = 5 };
        count.Should().Be(1);

        circle.Center = new Point(5, 5);
        count.Should().Be(2);

        m.Batch(() => new Circle { Radius = 5, Center = new Point(5, 5) });
        count.Should().Be(3);

        await m.Undo();
        count.Should().Be(4);

        await m.Redo();
        count.Should().Be(5);

        m.IsTrackingEnabled = false;
        new Circle { Radius = 5 };
        count.Should().Be(5);
        m.IsTrackingEnabled = true;

        m.ExecuteNoTrack(() => new Circle { Radius = 5, Center = new Point() });
        count.Should().Be(5);

        m.Reset();
        count.Should().Be(6);
    }

    [Fact]
    public async Task Should_fire_property_change_event()
    {
        new Circle { Radius = 10 };
        m.Changed += (_, args) =>
        {
            args.Event.Should().BeOfType<PropertyChangeEvent>();
            ((PropertyChangeEvent)args.Event!).PropertyValue.Should().Be(0);
        };
        await m.Undo();
    }

    [Fact]
    public async Task Should_fire_collection_addition_event()
    {
        var screen = new Screen();
        var circle = new Circle();

        int count = 0;
        m.Changed += (_, args) =>
        {
            args.Event.Should().BeOfType<ElementAdditionEvent<Circle>>();
            ((ElementAdditionEvent<Circle>)args.Event!).Collection.Should().BeSameAs(screen.Shapes);
            ((ElementAdditionEvent<Circle>)args.Event).Element.Should().Be(circle);
            count++;
        };
        screen.Add(circle);
        await m.Undo();
        count.Should().Be(2);
    }

    [Fact]
    public async Task Should_fire_collection_removal_event()
    {
        var screen = new Screen();
        var circle = new Circle();
        screen.Add(circle);

        int count = 0;
        m.Changed += (_, args) =>
        {
            args.Event.Should().BeOfType<ElementRemovalEvent<Circle>>();
            var removalEvent = (ElementRemovalEvent<Circle>)args.Event!;
            removalEvent.Collection.Should().BeSameAs(screen.Shapes);
            removalEvent.Element.Should().BeSameAs(circle);
            removalEvent.Index.Should().Be(0);
            count++;
        };
        screen.Remove(circle);
        await m.Undo();
        count.Should().Be(2);
    }

    [Fact]
    public async Task Should_fire_collection_element_position_change_event()
    {
        var screen = new Screen();
        var circle = new Circle();
        screen.Add(new Circle());
        screen.Add(circle);

        int count = 0;
        m.Changed += (_, args) =>
        {
            args.Event.Should().BeOfType<ElementIndexChangeEvent<Circle>>();
            var indexChangeEvent = (ElementIndexChangeEvent<Circle>)args.Event!;
            indexChangeEvent.Collection.Should().BeSameAs(screen.Shapes);
            indexChangeEvent.Element.Should().BeSameAs(circle);
            indexChangeEvent.Index.Should().Be(1);
            count++;
        };
        screen.MoveToFront(1);
        await m.Undo();
        count.Should().Be(2);
    }

    [Fact]
    public async Task Should_fire_batch_event()
    {
        int count = 0;
        m.Changed += (_, args) =>
        {
            args.Event.Should().BeOfType<BatchEvent>();
            var batchEvent = (BatchEvent)args.Event!;
            batchEvent.Count.Should().Be(2);
            var events = batchEvent.ToArray();
            events[0].Should().BeOfType<PropertyChangeEvent>();
            events[1].Should().BeOfType<PropertyChangeEvent>();
            count++;
        };
        m.Batch(() => new Circle { Center = new Point(5, 5), Radius = 5 });
        await m.Undo();
        count.Should().Be(2);
    }

    [Fact]
    public async Task Should_handle_custom_event()
    {
        var reverseEvent = new CustomEvent();
        var @event = new CustomEvent(reverseEvent);
        m.MarkEvent(@event);
        UndoCount(1);

        await m.Undo();
        @event.IsRolledback.Should().BeTrue();

        m.Changed += (_, args) => args.Event.Should().BeSameAs(reverseEvent);
        await m.Redo();
    }

    [Fact]
    public async Task Should_throw_if_invalid_rollback_return_type()
    {
        m.Batch(() =>
        {
            new Circle() { Radius = 10, Center = new Point(10, 10) };
        });

        BatchEvent? batchEvent = null;
        MementorChanged changed = (_, args) =>
        {
            batchEvent = (BatchEvent?)args.Event;
        };
        m.Changed += changed;
        await m.Undo();
        batchEvent.Should().NotBeNull();

        m.Changed -= changed;
        var customEvent = new InvalidCustomEvent(batchEvent!);
        m.MarkEvent(customEvent);

        Func<Task> undoAction = async () => await m.Undo();
        await undoAction.Should().ThrowExactlyAsync<InvalidOperationException>();

        m.Reset();
        m.Batch(() =>
        {
            m.MarkEvent(customEvent);
            m.MarkEvent(customEvent);
        });
        await undoAction.Should().ThrowExactlyAsync<InvalidOperationException>();
    }

    #region Helper

    private Features UndoCount(int c)
    {
        m.UndoCount.Should().Be(c);
        m.CanUndo.Should().Be(c > 0);
        return this;
    }

    private Features RedoCount(int c)
    {
        m.RedoCount.Should().Be(c);
        m.CanRedo.Should().Be(c > 0);
        return this;
    }

    #endregion
}