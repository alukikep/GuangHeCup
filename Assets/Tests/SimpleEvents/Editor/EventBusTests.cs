using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ModuleFactory.SimpleEvents.Tests
{
    public sealed class EventBusTests
    {
        [Test]
        public void PublishRoutesByMessageTypeAndBusInstance()
        {
            var bus = new EventBus();
            var otherBus = new EventBus();
            var received = new List<int>();
            using var numbers = bus.Subscribe<int>(received.Add);
            using var flags = bus.Subscribe<bool>(_ => Assert.Fail("Wrong event type"));
            using var otherNumbers = otherBus.Subscribe<int>(_ => Assert.Fail("Wrong bus"));

            bus.Publish(42);
            bus.Publish(1.5f);

            Assert.That(received, Is.EqualTo(new[] { 42 }));
        }

        [Test]
        public void DuplicateRegistrationsHaveIndependentHandlesAndPreserveOrder()
        {
            var bus = new EventBus();
            var calls = new List<string>();
            Action<int> duplicate = _ => calls.Add("duplicate");
            using var first = bus.Subscribe(duplicate);
            using var middle = bus.Subscribe<int>(_ => calls.Add("middle"));
            using var last = bus.Subscribe(duplicate);

            bus.Publish(0);
            Assert.That(calls, Is.EqualTo(new[] { "duplicate", "middle", "duplicate" }));

            calls.Clear();
            first.Dispose();
            first.Dispose();
            bus.Publish(0);
            Assert.That(calls, Is.EqualTo(new[] { "middle", "duplicate" }));
        }

        [Test]
        public void ChangesDuringPublishSkipRemovedAndDeferAddedListeners()
        {
            var bus = new EventBus();
            var calls = new List<string>();
            IDisposable second = null;
            IDisposable added = null;
            using var first = bus.Subscribe<int>(_ =>
            {
                calls.Add("first");
                second.Dispose();
                added ??= bus.Subscribe<int>(_ => calls.Add("added"));
            });
            second = bus.Subscribe<int>(_ => calls.Add("second"));

            bus.Publish(0);
            Assert.That(calls, Is.EqualTo(new[] { "first" }));

            calls.Clear();
            bus.Publish(0);
            Assert.That(calls, Is.EqualTo(new[] { "first", "added" }));
            added.Dispose();
        }

        [Test]
        public void NestedPublishUsesLatestSubscriptionsAndCompletesBeforeOuterPublishResumes()
        {
            var bus = new EventBus();
            var calls = new List<string>();
            IDisposable added = null;
            using var first = bus.Subscribe<int>(value =>
            {
                calls.Add($"first:{value}");
                if (value == 1)
                {
                    added = bus.Subscribe<int>(nested => calls.Add($"added:{nested}"));
                    bus.Publish(2);
                }
            });
            using var second = bus.Subscribe<int>(value => calls.Add($"second:{value}"));

            bus.Publish(1);

            Assert.That(calls, Is.EqualTo(new[]
            {
                "first:1", "first:2", "second:2", "added:2", "second:1"
            }));
            added.Dispose();
        }

        [Test]
        public void ListenerExceptionStopsDispatchAndBusCanPublishAgain()
        {
            var bus = new EventBus();
            var error = new InvalidOperationException("Listener failed");
            var calls = 0;
            using var failing = bus.Subscribe<int>(_ => throw error);
            using var remaining = bus.Subscribe<int>(_ => calls++);

            Assert.That(Assert.Throws<InvalidOperationException>(() => bus.Publish(0)), Is.SameAs(error));
            Assert.That(calls, Is.Zero);

            failing.Dispose();
            bus.Publish(0);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void DisposingLastSubscriptionAllowsNewRegistration()
        {
            var bus = new EventBus();
            var calls = 0;
            var original = bus.Subscribe<int>(_ => Assert.Fail("Disposed listener called"));
            original.Dispose();
            bus.Publish(0);

            using var replacement = bus.Subscribe<int>(_ => calls++);
            original.Dispose();
            bus.Publish(0);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void SubscribeRejectsNullHandler()
        {
            var bus = new EventBus();

            Assert.Throws<ArgumentNullException>(() => bus.Subscribe<int>(null));
        }
    }
}
