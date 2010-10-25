using System;

using DomainDrivenDelivery.Utilities;

namespace DomainDrivenDelivery.Domain.Patterns.ValueObject
{
    /// <summary>
    /// Supporting base class for value objects.
    /// </summary>
    /// <remarks>
    /// While the <see cref="ValueObject{T}"/> interface makes the pattern properties explicit,
    /// this class is less general and is suited for this particular application.
    /// <para />
    /// For example, the private id field is meant for autogenerated, surrogate primary keys.
    /// Also, you may want more flexibility in selecting significant fields for comparision,
    /// or you may need to be able to calculate equals millions of times every second,
    /// in which case reflection might not be fast enough.
    /// </remarks>
    /// <typeparam name="T">The value object type.</typeparam>
    public abstract class ValueObjectSupport<T> : ValueObject<T> where T : class, ValueObject<T>
    {
        private readonly long _primaryKey;
        [NonSerialized]
        private int _cachedHashCode;
        private static readonly string[] EXCLUDED_FIELDS = { "_primaryKey", "_cachedHashCode" };

        public bool sameValueAs(T other)
        {
            return other != null && EqualsBuilder.reflectionEquals(this, other, EXCLUDED_FIELDS);
        }

        public override int GetHashCode()
        {
            // Using a local variable to ensure that we only do a single read
            // of the _cachedHashCode field, to avoid race conditions.
            // It doesn't matter if several threads compute the hash code and overwrite
            // each other, but it's important that we never return 0, which could happen
            // with multiple reads of the _cachedHashCode field.
            //
            // See java.lang.String.hashCode()
            int h = _cachedHashCode;
            if(h == 0)
            {
                // Lazy initialization of hash code.
                // Value objects are immutable, so the hash code never changes.
                h = HashCodeBuilder.reflectionHashCode(this, false);
                _cachedHashCode = h;
            }

            return h;
        }

        public override bool Equals(object obj)
        {
            if(obj == null) return false;
            if(this == obj) return true;
            if(GetType() != obj.GetType()) return false;

            return sameValueAs((T)obj);
        }
    }
}