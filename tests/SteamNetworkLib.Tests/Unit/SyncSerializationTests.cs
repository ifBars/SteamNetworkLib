using System;
using System.Collections.Generic;
using FluentAssertions;
using SteamNetworkLib.Sync;
using Xunit;

namespace SteamNetworkLib.Tests.Unit
{
    /// <summary>
    /// Unit tests for sync var serialization without requiring Steam.
    /// Tests the JsonSyncSerializer's ability to serialize and deserialize various types.
    /// </summary>
    public class SyncSerializationTests
    {
        private readonly JsonSyncSerializer _serializer;

        public SyncSerializationTests()
        {
            _serializer = new JsonSyncSerializer();
        }

        [Fact]
        public void JsonSyncSerializer_Primitives_SerializeAndDeserialize()
        {
            // Integer
            var intValue = 42;
            var intSerialized = _serializer.Serialize(intValue);
            var intDeserialized = _serializer.Deserialize<int>(intSerialized);
            intDeserialized.Should().Be(42);

            // String
            var stringValue = "Hello, World!";
            var stringSerialized = _serializer.Serialize(stringValue);
            var stringDeserialized = _serializer.Deserialize<string>(stringSerialized);
            stringDeserialized.Should().Be("Hello, World!");

            // Boolean
            var boolValue = true;
            var boolSerialized = _serializer.Serialize(boolValue);
            var boolDeserialized = _serializer.Deserialize<bool>(boolSerialized);
            boolDeserialized.Should().BeTrue();

            // Float
            var floatValue = 3.14159f;
            var floatSerialized = _serializer.Serialize(floatValue);
            var floatDeserialized = _serializer.Deserialize<float>(floatSerialized);
            floatDeserialized.Should().BeApproximately(3.14159f, 0.0001f);

            // Double
            var doubleValue = 2.71828;
            var doubleSerialized = _serializer.Serialize(doubleValue);
            var doubleDeserialized = _serializer.Deserialize<double>(doubleSerialized);
            doubleDeserialized.Should().BeApproximately(2.71828, 0.00001);
        }

        [Fact]
        public void JsonSyncSerializer_ComplexObject_SerializeAndDeserialize()
        {
            // Arrange
            var original = new TestGameData
            {
                PlayerId = 12345,
                PlayerName = "TestPlayer",
                Score = 9999,
                IsAlive = true,
                Position = new Vector3 { X = 1.5f, Y = 2.5f, Z = 3.5f }
            };

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<TestGameData>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.PlayerId.Should().Be(12345);
            deserialized.PlayerName.Should().Be("TestPlayer");
            deserialized.Score.Should().Be(9999);
            deserialized.IsAlive.Should().BeTrue();
            deserialized.Position.Should().NotBeNull();
            deserialized.Position.X.Should().BeApproximately(1.5f, 0.001f);
            deserialized.Position.Y.Should().BeApproximately(2.5f, 0.001f);
            deserialized.Position.Z.Should().BeApproximately(3.5f, 0.001f);
        }

        [Fact]
        public void JsonSyncSerializer_List_SerializeAndDeserialize()
        {
            // Arrange
            var original = new List<int> { 1, 2, 3, 4, 5 };

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<List<int>>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Should().HaveCount(5);
            deserialized.Should().ContainInOrder(1, 2, 3, 4, 5);
        }

        [Fact]
        public void JsonSyncSerializer_Dictionary_SerializeAndDeserialize()
        {
            // Arrange
            var original = new Dictionary<string, int>
            {
                { "kills", 10 },
                { "deaths", 3 },
                { "assists", 7 }
            };

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<Dictionary<string, int>>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Should().HaveCount(3);
            deserialized["kills"].Should().Be(10);
            deserialized["deaths"].Should().Be(3);
            deserialized["assists"].Should().Be(7);
        }

        [Fact]
        public void JsonSyncSerializer_CanSerialize_ReturnsTrueForSupportedTypes()
        {
            _serializer.CanSerialize(typeof(int)).Should().BeTrue();
            _serializer.CanSerialize(typeof(string)).Should().BeTrue();
            _serializer.CanSerialize(typeof(bool)).Should().BeTrue();
            _serializer.CanSerialize(typeof(float)).Should().BeTrue();
            _serializer.CanSerialize(typeof(double)).Should().BeTrue();
            _serializer.CanSerialize(typeof(List<int>)).Should().BeTrue();
            _serializer.CanSerialize(typeof(Dictionary<string, int>)).Should().BeTrue();
            _serializer.CanSerialize(typeof(TestGameData)).Should().BeTrue();
        }

        [Fact]
        public void JsonSyncSerializer_NullValue_SerializeAndDeserialize()
        {
            // Arrange
            string? nullString = null;

            // Act
            var serialized = _serializer.Serialize(nullString);
            var deserialized = _serializer.Deserialize<string?>(serialized);

            // Assert - should handle null gracefully
            serialized.Should().NotBeNull(); // JSON "null" or empty
        }

        [Fact]
        public void JsonSyncSerializer_EmptyString_SerializeAndDeserialize()
        {
            // Arrange
            var emptyString = "";

            // Act
            var serialized = _serializer.Serialize(emptyString);
            var deserialized = _serializer.Deserialize<string>(serialized);

            // Assert
            deserialized.Should().Be("");
        }

        [Fact]
        public void JsonSyncSerializer_LargeObject_SerializeAndDeserialize()
        {
            // Arrange - create a complex nested structure
            var original = new TestComplexData
            {
                Id = 999,
                Name = "Complex Test",
                Players = new List<string> { "Player1", "Player2", "Player3" },
                Scores = new Dictionary<string, int>
                {
                    { "Player1", 100 },
                    { "Player2", 200 },
                    { "Player3", 300 }
                },
                Metadata = new TestGameData
                {
                    PlayerId = 1,
                    PlayerName = "Host",
                    Score = 1000,
                    IsAlive = true,
                    Position = new Vector3 { X = 0, Y = 0, Z = 0 }
                }
            };

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<TestComplexData>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Id.Should().Be(999);
            deserialized.Name.Should().Be("Complex Test");
            deserialized.Players.Should().HaveCount(3);
            deserialized.Scores.Should().HaveCount(3);
            deserialized.Metadata.Should().NotBeNull();
            deserialized.Metadata.PlayerName.Should().Be("Host");
        }

        [Fact]
        public void JsonSyncSerializer_Enum_SerializeAndDeserialize()
        {
            // Arrange
            var original = TestEnum.ValueTwo;

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<TestEnum>(serialized);

            // Assert
            deserialized.Should().Be(TestEnum.ValueTwo);
        }

        [Fact]
        public void JsonSyncSerializer_EnumInObject_SerializeAndDeserialize()
        {
            // Arrange
            var original = new TestObjectWithEnum
            {
                Id = 123,
                Status = TestEnum.ValueThree,
                Name = "Test"
            };

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<TestObjectWithEnum>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Id.Should().Be(123);
            deserialized.Status.Should().Be(TestEnum.ValueThree);
            deserialized.Name.Should().Be("Test");
        }

        [Fact]
        public void JsonSyncSerializer_Array_SerializeAndDeserialize()
        {
            // Arrange
            var original = new int[] { 10, 20, 30, 40, 50 };

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<int[]>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Should().HaveCount(5);
            deserialized.Should().ContainInOrder(10, 20, 30, 40, 50);
        }

        [Fact]
        public void JsonSyncSerializer_StringArray_SerializeAndDeserialize()
        {
            // Arrange
            var original = new string[] { "alpha", "beta", "gamma" };

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<string[]>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Should().HaveCount(3);
            deserialized.Should().ContainInOrder("alpha", "beta", "gamma");
        }

        [Fact]
        public void JsonSyncSerializer_ObjectArray_SerializeAndDeserialize()
        {
            // Arrange
            var original = new Vector3[]
            {
                new Vector3 { X = 1, Y = 2, Z = 3 },
                new Vector3 { X = 4, Y = 5, Z = 6 }
            };

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<Vector3[]>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Should().HaveCount(2);
            deserialized[0].X.Should().Be(1);
            deserialized[1].Z.Should().Be(6);
        }

        [Fact]
        public void JsonSyncSerializer_NestedDictionary_SerializeAndDeserialize()
        {
            // Arrange
            var original = new Dictionary<string, Dictionary<string, int>>
            {
                { "player1", new Dictionary<string, int> { { "kills", 5 }, { "deaths", 2 } } },
                { "player2", new Dictionary<string, int> { { "kills", 3 }, { "deaths", 4 } } }
            };

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Should().HaveCount(2);
            deserialized["player1"]["kills"].Should().Be(5);
            deserialized["player2"]["deaths"].Should().Be(4);
        }

        [Fact]
        public void JsonSyncSerializer_EmptyList_SerializeAndDeserialize()
        {
            // Arrange
            var original = new List<string>();

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<List<string>>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Should().BeEmpty();
        }

        [Fact]
        public void JsonSyncSerializer_EmptyDictionary_SerializeAndDeserialize()
        {
            // Arrange
            var original = new Dictionary<string, int>();

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<Dictionary<string, int>>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Should().BeEmpty();
        }

        [Fact]
        public void JsonSyncSerializer_Long_SerializeAndDeserialize()
        {
            // Arrange
            var original = 9876543210L;

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<long>(serialized);

            // Assert
            deserialized.Should().Be(9876543210L);
        }

        [Fact]
        public void JsonSyncSerializer_NegativeNumbers_SerializeAndDeserialize()
        {
            // Arrange
            var negInt = -42;
            var negFloat = -3.14f;
            var negDouble = -2.71828;

            // Act & Assert
            var intSerialized = _serializer.Serialize(negInt);
            _serializer.Deserialize<int>(intSerialized).Should().Be(-42);

            var floatSerialized = _serializer.Serialize(negFloat);
            _serializer.Deserialize<float>(floatSerialized).Should().BeApproximately(-3.14f, 0.001f);

            var doubleSerialized = _serializer.Serialize(negDouble);
            _serializer.Deserialize<double>(doubleSerialized).Should().BeApproximately(-2.71828, 0.00001);
        }

        [Fact]
        public void JsonSyncSerializer_SpecialStringCharacters_SerializeAndDeserialize()
        {
            // Arrange
            var original = "Line1\nLine2\tTabbed\"Quoted\"";

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<string>(serialized);

            // Assert
            deserialized.Should().Be("Line1\nLine2\tTabbed\"Quoted\"");
        }

        [Fact]
        public void JsonSyncSerializer_ZeroValues_SerializeAndDeserialize()
        {
            // Arrange & Act & Assert
            _serializer.Deserialize<int>(_serializer.Serialize(0)).Should().Be(0);
            _serializer.Deserialize<float>(_serializer.Serialize(0.0f)).Should().Be(0.0f);
            _serializer.Deserialize<double>(_serializer.Serialize(0.0)).Should().Be(0.0);
        }

        [Fact]
        public void JsonSyncSerializer_BooleanFalse_SerializeAndDeserialize()
        {
            // Arrange
            var original = false;

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<bool>(serialized);

            // Assert
            deserialized.Should().BeFalse();
        }
    }

    #region Test Classes

    public class TestGameData
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public int Score { get; set; }
        public bool IsAlive { get; set; }
        public Vector3 Position { get; set; } = new Vector3();
    }

    public class Vector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public class TestComplexData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<string> Players { get; set; } = new List<string>();
        public Dictionary<string, int> Scores { get; set; } = new Dictionary<string, int>();
        public TestGameData Metadata { get; set; } = new TestGameData();
    }

    public enum TestEnum
    {
        ValueOne,
        ValueTwo,
        ValueThree
    }

    public class TestObjectWithEnum
    {
        public int Id { get; set; }
        public TestEnum Status { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    #endregion
}
