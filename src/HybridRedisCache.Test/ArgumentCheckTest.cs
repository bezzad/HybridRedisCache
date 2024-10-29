using StackExchange.Redis;
using System;
using System.Collections.Generic;
using Xunit;

namespace HybridRedisCache.Test;

public class ArgumentCheckTest
{
    [Fact]
    public void NotNull_Should_Throw_ArgumentNullException_When_Argument_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() => ArgumentCheck.NotNull(null, "name"));
    }

    [Theory]
    [InlineData(10)]
    [InlineData("10")]
    public void NotNull_Should_Not_Throw_ArgumentNullException_When_Argument_Is_Not_Null(object obj)
    {
        var ex = Record.Exception(() => ArgumentCheck.NotNull(obj, "name"));

        Assert.Null(ex);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void NotNullOrWhiteSpace_Should_Throw_ArgumentNullException_When_Argument_Is_NullOrWhiteSpace(string str)
    {
        Assert.Throws<ArgumentNullException>(() => ArgumentCheck.NotNullOrWhiteSpace(str, "name"));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("  1")]
    public void NotNullOrWhiteSpace_Should_Not_Throw_ArgumentNullException_When_Argument_Is_NotNullOrWhiteSpace(string str)
    {
        var ex = Record.Exception(() => ArgumentCheck.NotNullOrWhiteSpace(str, "name"));

        Assert.Null(ex);
    }

    [Fact]
    public void NotNegativeOrZero_Should_Throw_ArgumentOutOfRangeException_When_Argument_Is_Negative()
    {
        var ts = new TimeSpan(0, 0, -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgumentCheck.NotNegativeOrZero(ts, nameof(ts)));
    }

    [Fact]
    public void NotNegativeOrZero_Should_Throw_ArgumentOutOfRangeException_When_Argument_Is_Zero()
    {
        var ts = TimeSpan.Zero;
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgumentCheck.NotNegativeOrZero(ts, nameof(ts)));
    }

    [Fact]
    public void NotNegativeOrZero_Should_Not_Throw_ArgumentOutOfRangeException_When_Argument_Is_NotNegativeOrZero()
    {
        var ts = new TimeSpan(0, 0, 1);
        var ex = Record.Exception(() => ArgumentCheck.NotNegativeOrZero(ts, nameof(ts)));

        Assert.Null(ex);
    }

    [Fact]
    public void NotNullAndCountGTZero_List_Should_Throw_ArgumentNullException_When_Argument_Is_Null()
    {
        List<string> list = null;

        Assert.Throws<ArgumentNullException>(() => ArgumentCheck.NotNullAndCountGTZero(list, nameof(list)));
    }

    [Fact]
    public void NotNullAndCountGTZero_List_Should_Throw_ArgumentNullException_When_Argument_Is_Empty()
    {
        var list = new List<string>();

        Assert.Throws<ArgumentNullException>(() => ArgumentCheck.NotNullAndCountGTZero(list, nameof(list)));
    }

    [Fact]
    public void NotNullAndCountGTZero_List_Should_Not_Throw_ArgumentNullException_When_Argument_Is_NotNull_And_Empty()
    {
        var list = new List<string> { "abc" };

        var ex = Record.Exception(() => ArgumentCheck.NotNullAndCountGTZero(list, nameof(list)));

        Assert.Null(ex);
    }

    [Fact]
    public void NotNullAndCountGTZero_Dict_Should_Throw_ArgumentNullException_When_Argument_Is_Null()
    {
        Dictionary<string, string> dict = null;

        Assert.Throws<ArgumentNullException>(() => ArgumentCheck.NotNullAndCountGTZero(dict, nameof(dict)));
    }

    [Fact]
    public void NotNullAndCountGTZero_Dict_Should_Throw_ArgumentNullException_When_Argument_Is_Empty()
    {
        var dict = new Dictionary<string, string>();

        Assert.Throws<ArgumentNullException>(() => ArgumentCheck.NotNullAndCountGTZero(dict, nameof(dict)));
    }

    [Fact]
    public void NotNullAndCountGTZero_Dict_Should_Not_Throw_ArgumentNullException_When_Argument_Is_NotNull_And_Empty()
    {
        var dict = new Dictionary<string, string> { { "abc", "123" } };

        var ex = Record.Exception(() => ArgumentCheck.NotNullAndCountGTZero(dict, nameof(dict)));

        Assert.Null(ex);
    }

    [Theory]
    [InlineData("00:49:00")]
    [InlineData("01:29:19")]
    [InlineData("02:01:00")]
    [InlineData("03:59:59")]
    [InlineData("04:55:55")]
    [InlineData("05:44:00")]
    [InlineData("06:33:33")]
    [InlineData("07:22:00")]
    [InlineData("08:11:11")]
    [InlineData("09:00:00")]
    [InlineData("10:11:00")]
    [InlineData("11:15")]
    [InlineData("12:22:59")]
    [InlineData("13:32:22")]
    [InlineData("14:42:11")]
    [InlineData("15:52:01")]
    [InlineData("16:02:00")]
    [InlineData("17:00:50")]
    [InlineData("18:20:40")]
    [InlineData("19:39:30")]
    [InlineData("20:20:20")]
    [InlineData("21:14:10")]
    [InlineData("22:22:00")]
    [InlineData("23:59:59")]
    public void TestGetNextUtcDateTime(string time)
    {
        // arrange 
        var now = DateTime.UtcNow;
        var fromDateTime = new DateTime(now.Year, now.Month, now.Day, 12, 0, 0, DateTimeKind.Utc);
        var timeObj = TimeOnly.Parse(time);
        if (timeObj.ToTimeSpan() <= fromDateTime.TimeOfDay)
            now = now.AddDays(1);

        // act
        var nextDateTime = time.GetNextUtcDateTime(fromDateTime);

        // assert
        Assert.Equal(timeObj.Second, nextDateTime.Second);
        Assert.Equal(timeObj.Minute, nextDateTime.Minute);
        Assert.Equal(timeObj.Second, nextDateTime.Second);
        Assert.Equal(now.Day, nextDateTime.Day);
        Assert.Equal(now.Month, nextDateTime.Month);
        Assert.Equal(now.Year, nextDateTime.Year);
        Assert.Equal(now.Year, nextDateTime.Year);
    }

    [Theory]
    [InlineData("00:49:00")]
    [InlineData("01:29:19")]
    [InlineData("02:01:00")]
    [InlineData("03:59:59")]
    [InlineData("04:55:55")]
    [InlineData("05:44:00")]
    [InlineData("06:33:33")]
    [InlineData("07:22:00")]
    [InlineData("08:11:11")]
    [InlineData("09:00:00")]
    [InlineData("10:11:00")]
    [InlineData("11:15")]
    [InlineData("12:22:59")]
    [InlineData("13:32:22")]
    [InlineData("14:42:11")]
    [InlineData("15:52:01")]
    [InlineData("16:02:00")]
    [InlineData("17:00:50")]
    [InlineData("18:20:40")]
    [InlineData("19:39:30")]
    [InlineData("20:20:20")]
    [InlineData("21:14:10")]
    [InlineData("22:22:00")]
    [InlineData("23:59:59")]
    public void TestGetNonZeroDurationFromNow(string time)
    {
        // arrange 
        var now = DateTime.UtcNow;
        var fromDateTime = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var timeObj = TimeOnly.Parse(time);
        var expectedSeconds = timeObj.ToTimeSpan().TotalSeconds;

        // act
        var duration = time.GetNextUtcDateTime(fromDateTime).GetNonZeroDurationFromNow(fromDateTime);
        var seconds = duration.TotalSeconds;

        // assert
        Assert.Equal(expectedSeconds, seconds);
    }

    [Theory]
    [InlineData(Flags.NoRedirect)]
    [InlineData(Flags.PreferReplica)]
    [InlineData(Flags.DemandReplica)]
    [InlineData(Flags.NoScriptCache)]
    [InlineData(Flags.DemandMaster)]
    [InlineData(Flags.FireAndForget)]
    public void TestCastRedisCommandFlagsEnum(Flags flags)
    {
        // act
        var result = (CommandFlags)flags;

        // assert
        Assert.Equal((int)flags, (int)result);
    }

    [Theory]
    [InlineData(Condition.Always)]
    [InlineData(Condition.Exists)]
    [InlineData(Condition.NotExists)]
    public void TestCastRedisWhenEnum(Condition when)
    {
        // act
        var result = (When)when;

        // assert
        Assert.Equal((int)when, (int)result);
        Assert.Equal(when.ToString(), result.ToString());
    }
}
