using AtlasOfAlchemy.Functions.VideoAnalysis;
using NUnit.Framework;

namespace AtlasOfAlchemy.Tests.VideoAnalysis;

[TestFixture]
public class VersionedDocumentIdTests
{
    [Test]
    public void Value_ReturnsCorrectFormat()
    {
        // Arrange
        var videoId = "dQw4w9WgXcQ";
        var timestamp = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

        // Act
        var documentId = VersionedDocumentId.Create(videoId, timestamp);

        // Assert
        Assert.That(documentId.Value, Is.EqualTo("dQw4w9WgXcQ_20260401T120000Z"));
    }

    [Test]
    public void Value_ConvertsToUtc()
    {
        // Arrange
        var videoId = "abc123";
        var timestamp = new DateTimeOffset(2026, 4, 1, 14, 0, 0, TimeSpan.FromHours(2)); // UTC+2

        // Act
        var documentId = VersionedDocumentId.Create(videoId, timestamp);

        // Assert
        Assert.That(documentId.Value, Is.EqualTo("abc123_20260401T120000Z"));
    }

    [Test]
    public void Create_WithValidInputs_ReturnsVersionedDocumentId()
    {
        // Arrange
        var videoId = "test-video";
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var documentId = VersionedDocumentId.Create(videoId, timestamp);

        // Assert
        Assert.That(documentId.VideoId, Is.EqualTo(videoId));
        Assert.That(documentId.Timestamp, Is.EqualTo(timestamp));
    }

    [Test]
    public void Create_WithNullVideoId_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => VersionedDocumentId.Create(null!, DateTimeOffset.UtcNow));
    }

    [Test]
    public void Create_WithEmptyVideoId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => VersionedDocumentId.Create("", DateTimeOffset.UtcNow));
    }

    [Test]
    public void Create_WithWhitespaceVideoId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => VersionedDocumentId.Create("   ", DateTimeOffset.UtcNow));
    }

    [Test]
    public void TryParse_WithValidVersionedFormat_ReturnsTrue()
    {
        // Arrange
        var documentId = "dQw4w9WgXcQ_20260401T120000Z";

        // Act
        var success = VersionedDocumentId.TryParse(documentId, out var result);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(result.VideoId, Is.EqualTo("dQw4w9WgXcQ"));
        Assert.That(result.Timestamp.UtcDateTime, Is.EqualTo(new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public void TryParse_WithVideoIdContainingUnderscore_ParsesCorrectly()
    {
        // Arrange - video ID with underscore, should use last underscore as separator
        var documentId = "video_with_underscore_20260401T120000Z";

        // Act
        var success = VersionedDocumentId.TryParse(documentId, out var result);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(result.VideoId, Is.EqualTo("video_with_underscore"));
    }

    [Test]
    public void TryParse_WithLegacyFormat_ReturnsFalse()
    {
        // Arrange - plain video ID without timestamp
        var documentId = "dQw4w9WgXcQ";

        // Act
        var success = VersionedDocumentId.TryParse(documentId, out _);

        // Assert
        Assert.That(success, Is.False);
    }

    [Test]
    public void TryParse_WithInvalidTimestamp_ReturnsFalse()
    {
        // Arrange
        var documentId = "dQw4w9WgXcQ_notadate";

        // Act
        var success = VersionedDocumentId.TryParse(documentId, out _);

        // Assert
        Assert.That(success, Is.False);
    }

    [Test]
    public void TryParse_WithNullOrEmpty_ReturnsFalse()
    {
        // Arrange & Act & Assert
        Assert.That(VersionedDocumentId.TryParse(null!, out _), Is.False);
        Assert.That(VersionedDocumentId.TryParse("", out _), Is.False);
        Assert.That(VersionedDocumentId.TryParse("   ", out _), Is.False);
    }

    [Test]
    public void TryParse_WithOnlySeparator_ReturnsFalse()
    {
        // Arrange
        var documentId = "_";

        // Act
        var success = VersionedDocumentId.TryParse(documentId, out _);

        // Assert
        Assert.That(success, Is.False);
    }

    [Test]
    public void TryParse_WithSeparatorAtStart_ReturnsFalse()
    {
        // Arrange
        var documentId = "_20260401T120000Z";

        // Act
        var success = VersionedDocumentId.TryParse(documentId, out _);

        // Assert
        Assert.That(success, Is.False);
    }

    [Test]
    public void ExtractVideoId_WithVersionedFormat_ReturnsVideoId()
    {
        // Arrange
        var documentId = "dQw4w9WgXcQ_20260401T120000Z";

        // Act
        var videoId = VersionedDocumentId.ExtractVideoId(documentId);

        // Assert
        Assert.That(videoId, Is.EqualTo("dQw4w9WgXcQ"));
    }

    [Test]
    public void ExtractVideoId_WithLegacyFormat_ReturnsDocumentIdAsIs()
    {
        // Arrange
        var documentId = "dQw4w9WgXcQ";

        // Act
        var videoId = VersionedDocumentId.ExtractVideoId(documentId);

        // Assert
        Assert.That(videoId, Is.EqualTo("dQw4w9WgXcQ"));
    }

    [Test]
    public void ExtractVideoId_WithNullOrEmpty_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => VersionedDocumentId.ExtractVideoId(null!));
        Assert.Throws<ArgumentException>(() => VersionedDocumentId.ExtractVideoId(""));
        Assert.Throws<ArgumentException>(() => VersionedDocumentId.ExtractVideoId("   "));
    }

    [Test]
    public void Roundtrip_CreateAndParse_PreservesData()
    {
        // Arrange
        var videoId = "test-video-123";
        var timestamp = new DateTimeOffset(2026, 6, 15, 9, 30, 45, TimeSpan.Zero);

        // Act
        var original = VersionedDocumentId.Create(videoId, timestamp);
        var success = VersionedDocumentId.TryParse(original.Value, out var parsed);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(parsed.VideoId, Is.EqualTo(videoId));
        Assert.That(parsed.Timestamp.UtcDateTime, Is.EqualTo(timestamp.UtcDateTime));
    }
}
