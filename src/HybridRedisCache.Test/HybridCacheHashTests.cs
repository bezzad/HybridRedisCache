using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace HybridRedisCache.Test;

public class HybridCacheHashTests(ITestOutputHelper testOutputHelper) : BaseCacheTest(testOutputHelper)
{
    [Fact]
    public async Task HashSetAsync_WithDictionary_ShouldStoreAllFields()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" },
            { "field3", "value3" }
        };

        // Act
        await Cache.HashSetAsync(key, fields);
        var result = await Cache.HashGetAsync(key);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("value1", result["field1"]);
        Assert.Equal("value2", result["field2"]);
        Assert.Equal("value3", result["field3"]);
    }

    [Fact]
    public async Task HashSetAsync_WithDictionaryAndExpiry_ShouldSetExpiration()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" }
        };
        var expiry = TimeSpan.FromMilliseconds(500);

        // Act
        await Cache.HashSetAsync(key, fields, expiry);
        await Task.Delay(TimeSpan.FromSeconds(1));
        var result = await Cache.HashGetAsync(key);

        // Assert - fields should have expired
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task HashSetAsync_WithSingleField_ShouldStoreField()
    {
        // Arrange
        var key = UniqueKey;
        var hashField = "testField";
        var value = "testValue";

        // Act
        await Cache.HashSetAsync(key, hashField, value);
        var result = await Cache.HashGetAsync(key, hashField);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(value, result);
    }

    [Fact]
    public async Task HashSetAsync_WithConditionNotExists_ShouldOnlySetIfNotExists()
    {
        // Arrange
        var key = UniqueKey;
        var hashField = "field1";
        var value1 = "value1";
        var value2 = "value2";

        // Act
        await Cache.HashSetAsync(key, hashField, value1, Condition.NotExists);
        await Cache.HashSetAsync(key, hashField, value2, Condition.NotExists);
        var result = await Cache.HashGetAsync(key, hashField);

        // Assert - should still have the first value
        Assert.Equal(value1, result);
    }

    [Fact]
    public async Task HashSetAsync_WithConditionAlways_ShouldOverwriteExistingValue()
    {
        // Arrange
        var key = UniqueKey;
        var hashField = "field1";
        var value1 = "value1";
        var value2 = "value2";

        // Act
        await Cache.HashSetAsync(key, hashField, value1);
        await Cache.HashSetAsync(key, hashField, value2);
        var result = await Cache.HashGetAsync(key, hashField);

        // Assert - should have the second value
        Assert.Equal(value2, result);
    }

    [Fact]
    public async Task HashGetAsync_WithNonExistentKey_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var key = UniqueKey;

        // Act
        var result = await Cache.HashGetAsync(key);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task HashGetAsync_WithSingleField_ShouldReturnFieldValue()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" },
            { "field3", "value3" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var result = await Cache.HashGetAsync(key, "field2");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("value2", result);
    }

    [Fact]
    public async Task HashGetAsync_WithNonExistentField_ShouldReturnNull()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var result = await Cache.HashGetAsync(key, "nonExistentField");

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Fact]
    public async Task HashGetAsync_WithMultipleFields_ShouldReturnAllFieldValues()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" },
            { "field3", "value3" },
            { "field4", "value4" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var requestedFields = new[] { "field1", "field3", "field4" };
        var result = await Cache.HashGetAsync(key, requestedFields);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.Equal("value1", result[0]);
        Assert.Equal("value3", result[1]);
        Assert.Equal("value4", result[2]);
    }

    [Fact]
    public async Task HashGetAsync_WithMixedExistentAndNonExistentFields_ShouldReturnPartialResults()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var requestedFields = new[] { "field1", "nonExistent", "field2" };
        var result = await Cache.HashGetAsync(key, requestedFields);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.Equal("value1", result[0]);
        Assert.True(string.IsNullOrEmpty(result[1]));
        Assert.Equal("value2", result[2]);
    }

    [Fact]
    public async Task HashExistsAsync_WithExistingField_ShouldReturnTrue()
    {
        // Arrange
        var key = UniqueKey;
        var hashField = "testField";
        var value = "testValue";
        await Cache.HashSetAsync(key, hashField, value);

        // Act
        var result = await Cache.HashExistsAsync(key, hashField);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HashExistsAsync_WithNonExistentField_ShouldReturnFalse()
    {
        // Arrange
        var key = UniqueKey;
        var hashField = "testField";
        var value = "testValue";
        await Cache.HashSetAsync(key, hashField, value);

        // Act
        var result = await Cache.HashExistsAsync(key, "nonExistentField");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HashExistsAsync_WithNonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        var key = UniqueKey;

        // Act
        var result = await Cache.HashExistsAsync(key, "anyField");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HashDeleteAsync_WithSingleField_ShouldDeleteFieldAndReturnTrue()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" },
            { "field3", "value3" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var deleted = await Cache.HashDeleteAsync(key, "field2");
        var remainingFields = await Cache.HashGetAsync(key);

        // Assert
        Assert.True(deleted);
        Assert.Equal(2, remainingFields.Count);
        Assert.True(remainingFields.ContainsKey("field1"));
        Assert.False(remainingFields.ContainsKey("field2"));
        Assert.True(remainingFields.ContainsKey("field3"));
    }

    [Fact]
    public async Task HashDeleteAsync_WithNonExistentField_ShouldReturnFalse()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var deleted = await Cache.HashDeleteAsync(key, "nonExistentField");

        // Assert
        Assert.False(deleted);
    }

    [Fact]
    public async Task HashDeleteAsync_WithMultipleFields_ShouldDeleteAllFieldsAndReturnCount()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" },
            { "field3", "value3" },
            { "field4", "value4" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var fieldsToDelete = new[] { "field1", "field3", "field4" };
        var deletedCount = await Cache.HashDeleteAsync(key, fieldsToDelete);
        var remainingFields = await Cache.HashGetAsync(key);

        // Assert
        Assert.Equal(3, deletedCount);
        Assert.Single(remainingFields);
        Assert.True(remainingFields.ContainsKey("field2"));
    }

    [Fact]
    public async Task HashDeleteAsync_WithMixedExistentAndNonExistentFields_ShouldDeleteOnlyExistentFields()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var fieldsToDelete = new[] { "field1", "nonExistent", "field2" };
        var deletedCount = await Cache.HashDeleteAsync(key, fieldsToDelete);
        var remainingFields = await Cache.HashGetAsync(key);

        // Assert
        Assert.Equal(2, deletedCount); // Only 2 existing fields deleted
        Assert.Empty(remainingFields);
    }

    [Fact]
    public async Task HashSetAsync_WithEmptyDictionary_ShouldNotThrowException()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>();

        // Act & Assert
        await Cache.HashSetAsync(key, fields);
        var result = await Cache.HashGetAsync(key);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task HashOperations_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field:with:colons", "value:with:colons" },
            { "field-with-dashes", "value-with-dashes" },
            { "field_with_underscores", "value_with_underscores" },
            { "field.with.dots", "value.with.dots" }
        };

        // Act
        await Cache.HashSetAsync(key, fields);
        var result = await Cache.HashGetAsync(key);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal("value:with:colons", result["field:with:colons"]);
        Assert.Equal("value-with-dashes", result["field-with-dashes"]);
        Assert.Equal("value_with_underscores", result["field_with_underscores"]);
        Assert.Equal("value.with.dots", result["field.with.dots"]);
    }

    [Fact]
    public async Task HashOperations_WithUnicodeValues_ShouldHandleCorrectly()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "english", "Hello World" },
            { "chinese", "你好世界" },
            { "arabic", "مرحبا بالعالم" },
            { "emoji", "👋🌍" }
        };

        // Act
        await Cache.HashSetAsync(key, fields);
        var result = await Cache.HashGetAsync(key);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal("Hello World", result["english"]);
        Assert.Equal("你好世界", result["chinese"]);
        Assert.Equal("مرحبا بالعالم", result["arabic"]);
        Assert.Equal("👋🌍", result["emoji"]);
    }

    [Fact]
    public async Task HashOperations_WithLargeNumberOfFields_ShouldHandleCorrectly()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>();
        for (int i = 0; i < 1000; i++)
        {
            fields[$"field{i}"] = $"value{i}";
        }

        // Act
        await Cache.HashSetAsync(key, fields);
        var result = await Cache.HashGetAsync(key);

        // Assert
        Assert.Equal(1000, result.Count);
        Assert.Equal("value0", result["field0"]);
        Assert.Equal("value500", result["field500"]);
        Assert.Equal("value999", result["field999"]);
    }

    [Fact]
    public async Task HashSetAsync_UpdateExistingField_ShouldOverwriteValue()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        await Cache.HashSetAsync(key, "field1", "updatedValue1");
        var result = await Cache.HashGetAsync(key);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("updatedValue1", result["field1"]);
        Assert.Equal("value2", result["field2"]);
    }

    [Fact]
    public async Task HashOperations_ConcurrentAccess_ShouldHandleCorrectly()
    {
        // Arrange
        var key = UniqueKey;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Cache.HashSetAsync(key, $"field{index}", $"value{index}"));
        }
        await Task.WhenAll(tasks);
        var result = await Cache.HashGetAsync(key);

        // Assert
        Assert.Equal(10, result.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal($"value{i}", result[$"field{i}"]);
        }
    }

    [Fact]
    public async Task HashDeleteAsync_AllFields_ShouldLeaveEmptyHash()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        await Cache.HashDeleteAsync(key, new[] { "field1", "field2" });
        var result = await Cache.HashGetAsync(key);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task HashOperations_WithPreferMasterFlag_ShouldSucceed()
    {
        // Arrange
        var key = UniqueKey;
        var hashField = "testField";
        var value = "testValue";

        // Act
        await Cache.HashSetAsync(key, hashField, value);
        var exists = await Cache.HashExistsAsync(key, hashField);
        var deleted = await Cache.HashDeleteAsync(key, hashField);

        // Assert
        Assert.True(exists);
        Assert.True(deleted);
    }

    [Fact]
    public async Task HashValuesAsync_WithMultipleFields_ShouldReturnAllValues()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" },
            { "field3", "value3" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var values = await Cache.HashValuesAsync(key);

        // Assert
        Assert.NotNull(values);
        Assert.Equal(3, values.Length);
        Assert.Contains("value1", values);
        Assert.Contains("value2", values);
        Assert.Contains("value3", values);
    }

    [Fact]
    public async Task HashValuesAsync_WithEmptyHash_ShouldReturnEmptyArray()
    {
        // Arrange
        var key = UniqueKey;

        // Act
        var values = await Cache.HashValuesAsync(key);

        // Assert
        Assert.NotNull(values);
        Assert.Empty(values);
    }

    [Fact]
    public async Task HashKeysAsync_WithMultipleFields_ShouldReturnAllKeys()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" },
            { "field3", "value3" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var keys = await Cache.HashKeysAsync(key);

        // Assert
        Assert.NotNull(keys);
        Assert.Equal(3, keys.Length);
        Assert.Contains("field1", keys);
        Assert.Contains("field2", keys);
        Assert.Contains("field3", keys);
    }

    [Fact]
    public async Task HashKeysAsync_WithEmptyHash_ShouldReturnEmptyArray()
    {
        // Arrange
        var key = UniqueKey;

        // Act
        var keys = await Cache.HashKeysAsync(key);

        // Assert
        Assert.NotNull(keys);
        Assert.Empty(keys);
    }

    [Fact]
    public async Task HashLengthAsync_WithMultipleFields_ShouldReturnCorrectCount()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" },
            { "field3", "value3" },
            { "field4", "value4" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var length = await Cache.HashLengthAsync(key);

        // Assert
        Assert.Equal(4, length);
    }

    [Fact]
    public async Task HashLengthAsync_WithEmptyHash_ShouldReturnZero()
    {
        // Arrange
        var key = UniqueKey;

        // Act
        var length = await Cache.HashLengthAsync(key);

        // Assert
        Assert.Equal(0, length);
    }

    [Fact]
    public async Task HashLengthAsync_AfterAddingAndDeletingFields_ShouldReturnCorrectCount()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" },
            { "field3", "value3" }
        };
        await Cache.HashSetAsync(key, fields);
        await Cache.HashDeleteAsync(key, "field2");

        // Act
        var length = await Cache.HashLengthAsync(key);

        // Assert
        Assert.Equal(2, length);
    }

    [Fact]
    public async Task HashFieldGetAndDeleteAsync_WithExistingField_ShouldReturnValueAndDelete()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" },
            { "field3", "value3" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var value = await Cache.HashFieldGetAndDeleteAsync(key, "field2");
        var remainingFields = await Cache.HashGetAsync(key);

        // Assert
        Assert.Equal("value2", value);
        Assert.Equal(2, remainingFields.Count);
        Assert.False(remainingFields.ContainsKey("field2"));
        Assert.True(remainingFields.ContainsKey("field1"));
        Assert.True(remainingFields.ContainsKey("field3"));
    }

    [Fact]
    public async Task HashFieldGetAndDeleteAsync_WithNonExistentField_ShouldReturnEmpty()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var value = await Cache.HashFieldGetAndDeleteAsync(key, "nonExistentField");

        // Assert
        Assert.True(string.IsNullOrEmpty(value));
    }

    [Fact]
    public async Task HashFieldGetAndDeleteAsync_WithNonExistentKey_ShouldReturnEmpty()
    {
        // Arrange
        var key = UniqueKey;

        // Act
        var value = await Cache.HashFieldGetAndDeleteAsync(key, "anyField");

        // Assert
        Assert.True(string.IsNullOrEmpty(value));
    }

    [Fact]
    public async Task HashScanAsync_WithMatchingPattern_ShouldReturnMatchingFields()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "user:1:name", "John" },
            { "user:1:email", "john@example.com" },
            { "user:2:name", "Jane" },
            { "user:2:email", "jane@example.com" },
            { "admin:1:name", "Admin" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var result = new List<KeyValuePair<string, string>>();
        await foreach (var entry in Cache.HashScanAsync(key, "user:1:*"))
        {
            result.Add(entry);
        }

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Key == "user:1:name" && e.Value == "John");
        Assert.Contains(result, e => e.Key == "user:1:email" && e.Value == "john@example.com");
    }

    [Fact]
    public async Task HashScanAsync_WithWildcardPattern_ShouldReturnAllFields()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" },
            { "field3", "value3" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var result = new List<KeyValuePair<string, string>>();
        await foreach (var entry in Cache.HashScanAsync(key, "*"))
        {
            result.Add(entry);
        }

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task HashScanAsync_WithNoMatchingPattern_ShouldReturnEmpty()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var result = new List<KeyValuePair<string, string>>();
        await foreach (var entry in Cache.HashScanAsync(key, "nonexistent:*"))
        {
            result.Add(entry);
        }

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task HashScanAsync_WithNonExistentKey_ShouldReturnEmpty()
    {
        // Arrange
        var key = UniqueKey;

        // Act
        var result = new List<KeyValuePair<string, string>>();
        await foreach (var entry in Cache.HashScanAsync(key, "*"))
        {
            result.Add(entry);
        }

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task HashScanNoValuesAsync_WithMatchingPattern_ShouldReturnMatchingKeys()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "user:1:name", "John" },
            { "user:1:email", "john@example.com" },
            { "user:2:name", "Jane" },
            { "admin:1:name", "Admin" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var result = new List<string>();
        await foreach (var fieldName in Cache.HashScanNoValuesAsync(key, "user:1:*"))
        {
            result.Add(fieldName);
        }

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("user:1:name", result);
        Assert.Contains("user:1:email", result);
    }

    [Fact]
    public async Task HashScanNoValuesAsync_WithWildcardPattern_ShouldReturnAllKeys()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" },
            { "field3", "value3" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var result = new List<string>();
        await foreach (var fieldName in Cache.HashScanNoValuesAsync(key, "*"))
        {
            result.Add(fieldName);
        }

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("field1", result);
        Assert.Contains("field2", result);
        Assert.Contains("field3", result);
    }

    [Fact]
    public async Task HashScanNoValuesAsync_WithNoMatchingPattern_ShouldReturnEmpty()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "field1", "value1" },
            { "field2", "value2" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var result = new List<string>();
        await foreach (var fieldName in Cache.HashScanNoValuesAsync(key, "nonexistent:*"))
        {
            result.Add(fieldName);
        }

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task HashScanNoValuesAsync_WithNonExistentKey_ShouldReturnEmpty()
    {
        // Arrange
        var key = UniqueKey;

        // Act
        var result = new List<string>();
        await foreach (var fieldName in Cache.HashScanNoValuesAsync(key, "*"))
        {
            result.Add(fieldName);
        }

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task HashScanAsync_WithComplexPattern_ShouldReturnMatchingFields()
    {
        // Arrange
        var key = UniqueKey;
        var fields = new Dictionary<string, string>
        {
            { "product:123:name", "Product A" },
            { "product:123:price", "99.99" },
            { "product:456:name", "Product B" },
            { "order:789:total", "199.99" }
        };
        await Cache.HashSetAsync(key, fields);

        // Act
        var result = new List<KeyValuePair<string, string>>();
        await foreach (var entry in Cache.HashScanAsync(key, "product:*:name"))
        {
            result.Add(entry);
        }

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Key == "product:123:name");
        Assert.Contains(result, e => e.Key == "product:456:name");
    }

    [Fact]
    public async Task HashOperations_Integration_CompleteWorkflow()
    {
        // Arrange
        var key = UniqueKey;

        // Act & Assert - Set initial data
        var initialFields = new Dictionary<string, string>
        {
            { "user:id", "12345" },
            { "user:name", "John Doe" },
            { "user:email", "john@example.com" },
            { "user:age", "30" }
        };
        await Cache.HashSetAsync(key, initialFields);

        // Verify length
        var length = await Cache.HashLengthAsync(key);
        Assert.Equal(4, length);

        // Get all keys
        var keys = await Cache.HashKeysAsync(key);
        Assert.Equal(4, keys.Length);

        // Get all values
        var values = await Cache.HashValuesAsync(key);
        Assert.Equal(4, values.Length);

        // Check if field exists
        var exists = await Cache.HashExistsAsync(key, "user:name");
        Assert.True(exists);

        // Update a field
        await Cache.HashSetAsync(key, "user:age", "31");
        var updatedAge = await Cache.HashGetAsync(key, "user:age");
        Assert.Equal("31", updatedAge);

        // Scan with pattern
        var userFields = new List<KeyValuePair<string, string>>();
        await foreach (var entry in Cache.HashScanAsync(key, "user:*"))
        {
            userFields.Add(entry);
        }
        Assert.Equal(4, userFields.Count);

        // Get and delete a field
        var deletedValue = await Cache.HashFieldGetAndDeleteAsync(key, "user:email");
        Assert.Equal("john@example.com", deletedValue);

        // Verify deletion
        var lengthAfterDelete = await Cache.HashLengthAsync(key);
        Assert.Equal(3, lengthAfterDelete);

        // Delete remaining fields
        var deletedCount = await Cache.HashDeleteAsync(key, new[] { "user:id", "user:name", "user:age" });
        Assert.Equal(3, deletedCount);

        // Verify hash is empty
        var finalLength = await Cache.HashLengthAsync(key);
        Assert.Equal(0, finalLength);
    }
}
