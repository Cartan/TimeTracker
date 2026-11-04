using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TimeTracker.Data;
using TimeTracker.Models;
using Xunit;

namespace TimeTracker.Tests.Data;

public class TimeTrackerDbContextTests
{
    [Fact]
    public void TimeEntryTimestamps_UseDateTimeOffsetStorage()
    {
        var options = new DbContextOptionsBuilder<TimeTrackerDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=TimeTrackerTests")
            .Options;
        using var db = new TimeTrackerDbContext(options);
        var entity = db.Model.FindEntityType(typeof(TimeEntry))!;
        var startProperty = entity.FindProperty(nameof(TimeEntry.StartTime))!;
        var endProperty = entity.FindProperty(nameof(TimeEntry.EndTime))!;

        Assert.Equal("datetimeoffset", startProperty.GetColumnType());
        Assert.Equal("datetimeoffset", endProperty.GetColumnType());
        Assert.Equal(typeof(DateTimeOffset), startProperty.ClrType);
        Assert.Null(startProperty.GetTypeMapping().Converter);

    }
}
