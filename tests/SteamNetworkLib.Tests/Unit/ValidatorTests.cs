using System;
using FluentAssertions;
using SteamNetworkLib.Sync;
using Xunit;

namespace SteamNetworkLib.Tests.Unit
{
    /// <summary>
    /// Unit tests for sync var validators (PredicateValidator, RangeValidator, CompositeValidator).
    /// </summary>
    public class ValidatorTests
    {
        #region PredicateValidator Tests

        [Fact]
        public void PredicateValidator_PredicatePasses_ReturnsTrue()
        {
            // Arrange
            var validator = new PredicateValidator<int>(x => x > 0, "Value must be positive");

            // Act
            var isValid = validator.IsValid(5);

            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public void PredicateValidator_PredicateFails_ReturnsFalse()
        {
            // Arrange
            var validator = new PredicateValidator<int>(x => x > 0, "Value must be positive");

            // Act
            var isValid = validator.IsValid(-5);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void PredicateValidator_GetErrorMessage_ReturnsMessage()
        {
            // Arrange
            var expectedMessage = "Custom error message";
            var validator = new PredicateValidator<int>(x => x > 0, expectedMessage);

            // Act
            var errorMessage = validator.GetErrorMessage(-5);

            // Assert
            errorMessage.Should().Be(expectedMessage);
        }

        [Fact]
        public void PredicateValidator_NullPredicate_ThrowsArgumentNullException()
        {
            // Act & Assert
            Action act = () => new PredicateValidator<int>(null!, "Error");
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void PredicateValidator_StringValidation_Works()
        {
            // Arrange
            var validator = new PredicateValidator<string>(s => !string.IsNullOrEmpty(s), "String cannot be empty");

            // Act & Assert
            validator.IsValid("Hello").Should().BeTrue();
            validator.IsValid("").Should().BeFalse();
            validator.IsValid(null!).Should().BeFalse();
        }

        #endregion

        #region RangeValidator Tests

        [Fact]
        public void RangeValidator_InRange_ReturnsTrue()
        {
            // Arrange
            var validator = new RangeValidator<int>(0, 100);

            // Act
            var isValid = validator.IsValid(50);

            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public void RangeValidator_AtMinBoundaryInclusive_ReturnsTrue()
        {
            // Arrange
            var validator = new RangeValidator<int>(0, 100, inclusive: true);

            // Act
            var isValid = validator.IsValid(0);

            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public void RangeValidator_AtMaxBoundaryInclusive_ReturnsTrue()
        {
            // Arrange
            var validator = new RangeValidator<int>(0, 100, inclusive: true);

            // Act
            var isValid = validator.IsValid(100);

            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public void RangeValidator_BelowMin_ReturnsFalse()
        {
            // Arrange
            var validator = new RangeValidator<int>(0, 100);

            // Act
            var isValid = validator.IsValid(-1);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void RangeValidator_AboveMax_ReturnsFalse()
        {
            // Arrange
            var validator = new RangeValidator<int>(0, 100);

            // Act
            var isValid = validator.IsValid(101);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void RangeValidator_AtMinBoundaryExclusive_ReturnsFalse()
        {
            // Arrange
            var validator = new RangeValidator<int>(0, 100, inclusive: false);

            // Act
            var isValid = validator.IsValid(0);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void RangeValidator_AtMaxBoundaryExclusive_ReturnsFalse()
        {
            // Arrange
            var validator = new RangeValidator<int>(0, 100, inclusive: false);

            // Act
            var isValid = validator.IsValid(100);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void RangeValidator_GetErrorMessage_IncludesRangeInfo()
        {
            // Arrange
            var validator = new RangeValidator<int>(0, 100);

            // Act
            var errorMessage = validator.GetErrorMessage(150);

            // Assert
            errorMessage.Should().Contain("150");
            errorMessage.Should().Contain("0");
            errorMessage.Should().Contain("100");
        }

        [Fact]
        public void RangeValidator_FloatRange_Works()
        {
            // Arrange
            var validator = new RangeValidator<float>(0.0f, 1.0f);

            // Act & Assert
            validator.IsValid(0.5f).Should().BeTrue();
            validator.IsValid(0.0f).Should().BeTrue();
            validator.IsValid(1.0f).Should().BeTrue();
            validator.IsValid(-0.1f).Should().BeFalse();
            validator.IsValid(1.1f).Should().BeFalse();
        }

        #endregion

        #region CompositeValidator Tests

        [Fact]
        public void CompositeValidator_AllPass_ReturnsTrue()
        {
            // Arrange
            var validator1 = new PredicateValidator<int>(x => x > 0, "Must be positive");
            var validator2 = new PredicateValidator<int>(x => x < 100, "Must be less than 100");
            var composite = new CompositeValidator<int>(validator1, validator2);

            // Act
            var isValid = composite.IsValid(50);

            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public void CompositeValidator_FirstFails_ReturnsFalse()
        {
            // Arrange
            var validator1 = new PredicateValidator<int>(x => x > 0, "Must be positive");
            var validator2 = new PredicateValidator<int>(x => x < 100, "Must be less than 100");
            var composite = new CompositeValidator<int>(validator1, validator2);

            // Act
            var isValid = composite.IsValid(-5);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void CompositeValidator_SecondFails_ReturnsFalse()
        {
            // Arrange
            var validator1 = new PredicateValidator<int>(x => x > 0, "Must be positive");
            var validator2 = new PredicateValidator<int>(x => x < 100, "Must be less than 100");
            var composite = new CompositeValidator<int>(validator1, validator2);

            // Act
            var isValid = composite.IsValid(150);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void CompositeValidator_GetErrorMessage_ReturnsFirstFailedMessage()
        {
            // Arrange
            var validator1 = new PredicateValidator<int>(x => x > 0, "Must be positive");
            var validator2 = new PredicateValidator<int>(x => x < 100, "Must be less than 100");
            var composite = new CompositeValidator<int>(validator1, validator2);

            // Act
            var errorMessage = composite.GetErrorMessage(-5);

            // Assert
            errorMessage.Should().Be("Must be positive");
        }

        [Fact]
        public void CompositeValidator_AllPass_GetErrorMessageReturnsNull()
        {
            // Arrange
            var validator1 = new PredicateValidator<int>(x => x > 0, "Must be positive");
            var validator2 = new PredicateValidator<int>(x => x < 100, "Must be less than 100");
            var composite = new CompositeValidator<int>(validator1, validator2);

            // Act
            var errorMessage = composite.GetErrorMessage(50);

            // Assert
            errorMessage.Should().BeNull();
        }

        [Fact]
        public void CompositeValidator_EmptyValidators_ReturnsTrue()
        {
            // Arrange
            var composite = new CompositeValidator<int>();

            // Act
            var isValid = composite.IsValid(999);

            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public void CompositeValidator_WithRangeValidator_Works()
        {
            // Arrange
            var rangeValidator = new RangeValidator<int>(0, 100);
            var evenValidator = new PredicateValidator<int>(x => x % 2 == 0, "Must be even");
            var composite = new CompositeValidator<int>(rangeValidator, evenValidator);

            // Act & Assert
            composite.IsValid(50).Should().BeTrue();  // In range and even
            composite.IsValid(51).Should().BeFalse(); // In range but odd
            composite.IsValid(150).Should().BeFalse(); // Out of range
        }

        #endregion
    }
}
