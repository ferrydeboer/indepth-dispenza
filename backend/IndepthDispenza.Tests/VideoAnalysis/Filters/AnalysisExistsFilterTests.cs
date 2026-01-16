using AutoFixture;
using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Filters;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace IndepthDispenza.Tests.VideoAnalysis.Filters;

[TestFixture]
public class AnalysisExistsFilterTests
{
    private Mock<IVideoAnalysisRepository> _repositoryMock;
    private Mock<ILogger<AnalysisExistsFilter>> _loggerMock;
    private AnalysisExistsFilter _testSubject;
    private Fixture _fixture;
    private VideoInfo _video;
    private PlaylistScanRequest _request;

    [SetUp]
    public void Setup()
    {
        _fixture = new Fixture();
        _repositoryMock = new Mock<IVideoAnalysisRepository>();
        _loggerMock = new Mock<ILogger<AnalysisExistsFilter>>();
        _testSubject = new AnalysisExistsFilter(_repositoryMock.Object, _loggerMock.Object);
        _video = _fixture.Create<VideoInfo>();
        _request = _fixture.Create<PlaylistScanRequest>() with { Filters = VideoFilters.Parse("skip-existing") };
    }

    [Test]
    public async Task ShouldProcessAsync_FilterNotActive_ReturnsTrue()
    {
        // Arrange
        var request = _request with { Filters = VideoFilters.Empty };

        // Act
        var result = await Act(_video, request);

        // Assert
        Assert.That(result, Is.True);
        _repositoryMock.Verify(x => x.GetAnalysisAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ShouldProcessAsync_AnalysisExists_ReturnsFalse()
    {
        // Arrange
        var analysis = _fixture.Create<VideoAnalysisDocument>();

        _repositoryMock.Setup(x => x.GetAnalysisAsync(_video.VideoId))
            .ReturnsAsync(ServiceResult<VideoAnalysisDocument?>.Success(analysis));

        // Act
        var result = await Act(_video, _request);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ShouldProcessAsync_AnalysisDoesNotExist_ReturnsTrue()
    {
        // Arrange
        _repositoryMock.Setup(x => x.GetAnalysisAsync(_video.VideoId))
            .ReturnsAsync(ServiceResult<VideoAnalysisDocument?>.Success(null));

        // Act
        var result = await Act(_video, _request);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task ShouldProcessAsync_RepositoryError_ReturnsFalseAndLogs()
    {
        // Arrange
        _repositoryMock.Setup(x => x.GetAnalysisAsync(_video.VideoId))
            .ThrowsAsync(new Exception("Cosmos DB error"));

        // Act
        var result = await Act(_video, _request);

        // Assert
        Assert.That(result, Is.False);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error checking existence")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private async Task<bool> Act(VideoInfo video, PlaylistScanRequest request)
    {
        return await _testSubject.ShouldProcessAsync(video, request);
    }
}
