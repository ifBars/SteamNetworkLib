using System;

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// Interface for validating sync var values before they are synchronized.
    /// </summary>
    /// <typeparam name="T">The type of value to validate.</typeparam>
    public interface ISyncValidator<T>
    {
        /// <summary>
        /// Validates a value before it is synchronized.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <returns>True if the value is valid, false otherwise.</returns>
        bool IsValid(T value);

        /// <summary>
        /// Gets a human-readable error message describing why validation failed.
        /// </summary>
        /// <param name="value">The invalid value.</param>
        /// <returns>An error message, or null if no specific message is available.</returns>
        string? GetErrorMessage(T value);
    }

    /// <summary>
    /// Base class for simple validators using a predicate function.
    /// </summary>
    /// <typeparam name="T">The type of value to validate.</typeparam>
    public class PredicateValidator<T> : ISyncValidator<T>
    {
        private readonly Func<T, bool> _predicate;
        private readonly string _errorMessage;

        /// <summary>
        /// Creates a new predicate-based validator.
        /// </summary>
        /// <param name="predicate">Function that returns true if the value is valid.</param>
        /// <param name="errorMessage">Error message to return when validation fails.</param>
        public PredicateValidator(Func<T, bool> predicate, string errorMessage)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _errorMessage = errorMessage ?? "Validation failed";
        }

        /// <inheritdoc/>
        public bool IsValid(T value) => _predicate(value);

        /// <inheritdoc/>
        public string? GetErrorMessage(T value) => _errorMessage;
    }

    /// <summary>
    /// Validator for numeric ranges.
    /// </summary>
    public class RangeValidator<T> : ISyncValidator<T> where T : IComparable<T>
    {
        private readonly T _min;
        private readonly T _max;
        private readonly bool _inclusive;

        /// <summary>
        /// Creates a new range validator.
        /// </summary>
        /// <param name="min">Minimum value (inclusive).</param>
        /// <param name="max">Maximum value (inclusive).</param>
        /// <param name="inclusive">If true, min and max are included in the valid range.</param>
        public RangeValidator(T min, T max, bool inclusive = true)
        {
            _min = min;
            _max = max;
            _inclusive = inclusive;
        }

        /// <inheritdoc/>
        public bool IsValid(T value)
        {
            if (_inclusive)
            {
                return value.CompareTo(_min) >= 0 && value.CompareTo(_max) <= 0;
            }
            else
            {
                return value.CompareTo(_min) > 0 && value.CompareTo(_max) < 0;
            }
        }

        /// <inheritdoc/>
        public string? GetErrorMessage(T value)
        {
            var op = _inclusive ? "[]" : "()";
            return $"Value {value} must be in range {op[0]}{_min}, {_max}{op[1]}";
        }
    }

    /// <summary>
    /// Validator that combines multiple validators with AND logic.
    /// </summary>
    public class CompositeValidator<T> : ISyncValidator<T>
    {
        private readonly ISyncValidator<T>[] _validators;

        /// <summary>
        /// Creates a composite validator that requires all validators to pass.
        /// </summary>
        /// <param name="validators">Array of validators to check.</param>
        public CompositeValidator(params ISyncValidator<T>[] validators)
        {
            _validators = validators ?? throw new ArgumentNullException(nameof(validators));
        }

        /// <inheritdoc/>
        public bool IsValid(T value)
        {
            foreach (var validator in _validators)
            {
                if (!validator.IsValid(value))
                {
                    return false;
                }
            }
            return true;
        }

        /// <inheritdoc/>
        public string? GetErrorMessage(T value)
        {
            foreach (var validator in _validators)
            {
                if (!validator.IsValid(value))
                {
                    return validator.GetErrorMessage(value);
                }
            }
            return null;
        }
    }
}
