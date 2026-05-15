using System.Text.Json;
using AtlasOfAlchemy.Functions.Interfaces;
using NUnit.Framework;

namespace AtlasOfAlchemy.Tests.Interfaces;

public class VideoInfoTests
{
    [Test]
    public void SystemTextJson_Roundtrip_WithVersionLabel_Works()
    {
        var videoInfo = new VideoInfo(
            VideoId: "abc123",
            Title: "Test Video",
            Description: "A test description",
            ChannelTitle: "Test Channel",
            PublishedAt: new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero),
            ThumbnailUrl: "https://example.com/thumb.jpg",
            Duration: TimeSpan.FromMinutes(10),
            ViewCount: 1000,
            VersionLabel: "v2.0");

        var json = JsonSerializer.Serialize(videoInfo);
        var deserialized = JsonSerializer.Deserialize<VideoInfo>(json);

        Assert.That(deserialized, Is.EqualTo(videoInfo));
        Assert.That(deserialized!.VersionLabel, Is.EqualTo("v2.0"));
    }

    [Test]
    public void SystemTextJson_Roundtrip_WithoutVersionLabel_Works()
    {
        var videoInfo = new VideoInfo(
            VideoId: "abc123",
            Title: "Test Video",
            Description: "A test description",
            ChannelTitle: "Test Channel",
            PublishedAt: new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero),
            ThumbnailUrl: "https://example.com/thumb.jpg");

        var json = JsonSerializer.Serialize(videoInfo);
        var deserialized = JsonSerializer.Deserialize<VideoInfo>(json);

        Assert.That(deserialized, Is.EqualTo(videoInfo));
        Assert.That(deserialized!.VersionLabel, Is.Null);
    }

    [Test]
    public void BackwardCompatibility_OldJsonWithoutVersionLabel_DeserializesCorrectly()
    {
        // Simulates a message serialized before VersionLabel was added
        var oldJson = """
            {
                "VideoId": "xyz789",
                "Title": "Old Video",
                "Description": "Old description",
                "ChannelTitle": "Old Channel",
                "PublishedAt": "2023-06-01T08:00:00+00:00",
                "ThumbnailUrl": "https://example.com/old.jpg",
                "Duration": "00:05:30",
                "ViewCount": 500
            }
            """;

        var deserialized = JsonSerializer.Deserialize<VideoInfo>(oldJson);

        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.VideoId, Is.EqualTo("xyz789"));
        Assert.That(deserialized.Title, Is.EqualTo("Old Video"));
        Assert.That(deserialized.VersionLabel, Is.Null);
    }

    [Test]
    public void BackwardCompatibility_MinimalOldJson_DeserializesCorrectly()
    {
        // Simulates the minimal required fields from an old message
        var minimalJson = """
            {
                "VideoId": "min123",
                "Title": "Minimal",
                "Description": "",
                "ChannelTitle": "Channel",
                "PublishedAt": "2023-01-01T00:00:00+00:00",
                "ThumbnailUrl": "https://example.com/min.jpg"
            }
            """;

        var deserialized = JsonSerializer.Deserialize<VideoInfo>(minimalJson);

        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized!.VideoId, Is.EqualTo("min123"));
        Assert.That(deserialized.Duration, Is.Null);
        Assert.That(deserialized.ViewCount, Is.Null);
        Assert.That(deserialized.VersionLabel, Is.Null);
    }
}
