using System;

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// Interface for validating sync var values before they are synchronized.
    /// </summary>
    /// <typeparam name="T">The type of value to validate.</typeparam>
    /// <remarks>
    /// <para><strong>Validation Flow:</strong> Validators are invoked before a value is synchronized.
    /// If validation fails, the sync operation is blocked and an error is reported.</para>
    /// 
    /// <para><strong>Error Handling:</strong> Validation errors can either throw <see cref="SyncValidationException"/>
    /// or be reported via the <see cref="HostSyncVar{T}.OnSyncError"/>/<see cref="ClientSyncVar{T}.OnSyncError"/> event,
    /// depending on the <see cref="NetworkSyncOptions.ThrowOnValidationError"/> setting.</para>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Enforcing numeric ranges (e.g., health must be 0-100)</description></item>
    /// <item><description>Validating string formats (e.g., usernames must be alphanumeric)</description></item>
    /// <item><description>Checking enum values are within valid ranges</description></item>
    /// <item><description>Complex business logic validation</description></item>
    /// </list>
    /// 
    /// <para><strong>Built-in Validators:</strong></para>
    /// <list type="bullet">
    /// <item><description><see cref="PredicateValidator{T}"/> - Custom validation using a lambda</description></item>
    /// <item><description><see cref="RangeValidator{T}"/> - Numeric range validation</description></item>
    /// <item><description><see cref="CompositeValidator{T}"/> - Combine multiple validators</description></item>
    /// </list>
    /// 
    /// <example>
    /// <code>
    /// // Create a validator that ensures health stays within 0-100
    /// var healthValidator = new RangeValidator&lt;int&gt;(0, 100);
    /// 
    /// // Create a custom validator using predicate
    /// var nameValidator = new PredicateValidator&lt;string&gt;(
    ///     name => !string.IsNullOrEmpty(name) &amp;&amp; name.Length &lt;= 20,
    ///     "Name must be 1-20 characters"
    /// );
    /// 
    /// // Use with a sync var
    /// var health = client.CreateHostSyncVar("Health", 100, validator: healthValidator);
    /// </code>
    /// </example>
    /// </remarks>
    public interface ISyncValidator<T>
    {
        /// <summary>
        /// Validates a value before it is synchronized.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <returns>True if the value is valid and should be synchronized; false otherwise.</returns>
        /// <remarks>
        /// <para>This method is called synchronously before each sync operation.</para>
        /// <para>Keep validation logic fast and non-blocking as it runs on the main thread.</para>
        /// </remarks>
        bool IsValid(T value);

        /// <summary>
        /// Gets a human-readable error message describing why validation failed.
        /// </summary>
        /// <param name="value">The invalid value that failed validation.</param>
        /// <returns>An error message describing why the value is invalid, or null if no specific message is available.</returns>
        /// <remarks>
        /// <para>This message is used in <see cref="SyncValidationException"/> and logged when validation fails.</para>
        /// <para>Include the invalid value and expected constraints in the message for easier debugging.</para>
        /// </remarks>
        string? GetErrorMessage(T value);
    }

    /// <summary>
    /// Base class for simple validators using a predicate function.
    /// </summary>
    /// <typeparam name="T">The type of value to validate.</typeparam>
    /// <remarks>
    /// <para><strong>Best For:</strong> One-off custom validation logic without creating a dedicated validator class.</para>
    /// 
    /// <para><strong>Performance:</strong> The predicate is invoked directly without additional overhead.</para>
    /// 
    /// <example>
    /// <code>
    /// // Validate that a string is not empty and has valid length
    /// var usernameValidator = new PredicateValidator&lt;string&gt;(
    ///     username => !string.IsNullOrWhiteSpace(username) &amp;&amp; username.Length >= 3 &amp;&amp; username.Length &lt;= 20,
    ///     "Username must be 3-20 characters and not whitespace"
    /// );
    /// 
    /// // Validate enum is defined
    /// var gameModeValidator = new PredicateValidator&lt;GameMode&gt;(
    ///     mode => Enum.IsDefined(typeof(GameMode), mode),
    ///     "Invalid game mode selected"
    /// );
    /// </code>
    /// </example>
    /// </remarks>
    public class PredicateValidator<T> : ISyncValidator<T>
    {
        private readonly Func<T, bool> _predicate;
        private readonly string _errorMessage;

        /// <summary>
        /// Creates a new predicate-based validator.
        /// </summary>
        /// <param name="predicate">Function that returns true if the value is valid.</param>
        /// <param name="errorMessage">Error message to return when validation fails.</param>
        /// <exception cref="ArgumentNullException">Thrown when predicate is null.</exception>
        /// <remarks>
        /// <para>The predicate should be pure (no side effects) as it may be called multiple times.</para>
        /// <para>The error message should clearly explain what validation failed for debugging purposes.</para>
        /// </remarks>
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
    /// Validator for numeric ranges with inclusive or exclusive bounds.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <remarks>
    /// <para><strong>Supported Types:</strong> Any type implementing <see cref="IComparable{T}"/> including
    /// <see cref="int"/>, <see cref="float"/>, <see cref="double"/>, <see cref="decimal"/>, <see cref="long"/>, etc.</para>
    /// 
    /// <para><strong>Bounds:</strong> By default, the range is inclusive (min and max are valid values).
    /// Use <c>inclusive: false</c> for exclusive bounds.</para>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Health/energy values (0-100)</description></item>
    /// <item><description>Position coordinates within map bounds</description></item>
    /// <item><description>Percentage values (0.0-1.0)</description></item>
    /// <item><description>Count limits (0-maxItems)</description></item>
    /// </list>
    /// 
    /// <example>
    /// <code>
    /// // Health must be between 0 and 100 (inclusive)
    /// var healthValidator = new RangeValidator&lt;int&gt;(0, 100);
    /// 
    /// // Percentage must be 0.0 to 1.0 (inclusive)
    /// var percentValidator = new RangeValidator&lt;float&gt;(0f, 1f);
    /// 
    /// // Exclusive range - value must be strictly between min and max
    /// var exclusiveValidator = new RangeValidator&lt;int&gt;(0, 100, inclusive: false);
    /// </code>
    /// </example>
    /// </remarks>
    public class RangeValidator<T> : ISyncValidator<T> where T : IComparable<T>
    {
        private readonly T _min;
        private readonly T _max;
        private readonly bool _inclusive;

        /// <summary>
        /// Creates a new range validator.
        /// </summary>
        /// <param name="min">Minimum allowed value.</param>
        /// <param name="max">Maximum allowed value.</param>
        /// <param name="inclusive">If true (default), min and max are valid values. If false, values must be strictly between min and max.</param>
        /// <exception cref="ArgumentException">Thrown when min is greater than max.</exception>
        /// <remarks>
        /// <para>When <paramref name="inclusive"/> is true, the valid range is [min, max].</para>
        /// <para>When <paramref name="inclusive"/> is false, the valid range is (min, max).</para>
        /// </remarks>
        public RangeValidator(T min, T max, bool inclusive = true)
        {
            if (min.CompareTo(max) > 0)
            {
                throw new ArgumentException("Minimum value must be less than or equal to maximum value", nameof(min));
            }

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
    /// <typeparam name="T">The type of value to validate.</typeparam>
    /// <remarks>
    /// <para><strong>Validation Logic:</strong> All validators must pass for the value to be considered valid.
    /// Validators are checked in order, and the first failure determines the error message.</para>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Combining range validation with custom business rules</description></item>
    /// <item><description>Validating multiple independent constraints</description></item>
    /// <item><description>Reusing existing validators in complex validation scenarios</description></item>
    /// </list>
    /// 
    /// <para><strong>Performance:</strong> Validators are evaluated sequentially. Order validators from fastest
    /// to slowest for optimal performance (fail fast on cheap checks).</para>
    /// 
    /// <example>
    /// <code>
    /// // Combine range and custom validation
    /// var healthValidator = new CompositeValidator&lt;int&gt;(
    ///     new RangeValidator&lt;int&gt;(0, 100),
    ///     new PredicateValidator&lt;int&gt;(
    ///         health => health % 10 == 0,
    ///         "Health must be a multiple of 10"
    ///     )
    /// );
    /// 
    /// // Multiple string constraints
    /// var usernameValidator = new CompositeValidator&lt;string&gt;(
    ///     new PredicateValidator&lt;string&gt;(
    ///         s => !string.IsNullOrWhiteSpace(s),
    ///         "Username cannot be empty"
    ///     ),
    ///     new PredicateValidator&lt;string&gt;(
    ///         s => s.Length >= 3 &amp;&amp; s.Length &lt;= 20,
    ///         "Username must be 3-20 characters"
    ///     ),
    ///     new PredicateValidator&lt;string&gt;(
    ///         s => s.All(char.IsLetterOrDigit),
    ///         "Username must be alphanumeric"
    ///     )
    /// );
    /// </code>
    /// </example>
    /// </remarks>
    public class CompositeValidator<T> : ISyncValidator<T>
    {
        private readonly ISyncValidator<T>[] _validators;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeValidator{T}"/> class.
        /// </summary>
        /// <param name="validators">Array of validators to check. All must pass for validation to succeed.</param>
        /// <exception cref="ArgumentNullException">Thrown when validators array is null.</exception>
        /// <remarks>
        /// <para>Validators are evaluated in the order provided.</para>
        /// <para>The error message from the first failing validator is returned.</para>
        /// <para>If no validators are provided, all values pass validation.</para>
        /// </remarks>
        public CompositeValidator(params ISyncValidator<T>[] validators)
        {
            if (validators == null)
            {
                throw new ArgumentNullException(nameof(validators));
            }

            _validators = validators;
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
