using System;
using System.Threading;

using DomainDrivenDelivery.Domain.Patterns.ValueObject;

namespace DomainDrivenDelivery.Domain.Model.Handling
{
    public class EventSequenceNumber : ValueObjectSupport<EventSequenceNumber>
    {
        private static long SEQUENCE = DateTime.Now.Millisecond;
        private readonly long value;

        private EventSequenceNumber(long value)
        {
            this.value = value;
        }

        public static EventSequenceNumber next()
        {
            return new EventSequenceNumber(Interlocked.Increment(ref SEQUENCE));
        }

        public static EventSequenceNumber valueOf(long value)
        {
            return new EventSequenceNumber(value);
        }

        public long longValue()
        {
            return value;
        }

        public override string ToString()
        {
            return value.ToString();
        }

        EventSequenceNumber()
        {
            // Needed by Hibernate
            value = -1L;
        }
    }
}