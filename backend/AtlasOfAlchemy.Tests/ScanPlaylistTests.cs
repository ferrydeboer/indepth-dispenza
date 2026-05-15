using Moq;
using NUnit.Framework;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AtlasOfAlchemy.Functions;
using AtlasOfAlchemy.Functions.Interfaces;

namespace AtlasOfAlchemy.Tests;

[TestFixture]
public class ScanPlaylistTests
{
    private Mock<ILogger<ScanPlaylist>> _mockLogger;
    private Mock<IPlaylistScanService> _mockPlaylistScanService;
    private Mock<IVideoAnalysisRepository> _mockRepository;
    private ScanPlaylist _scanPlaylist;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<ScanPlaylist>>();
        _mockPlaylistScanService = new Mock<IPlaylistScanService>();
        _mockRepository = new Mock<IVideoAnalysisRepository>();
        _scanPlaylist = new ScanPlaylist(_mockLogger.Object, _mockPlaylistScanService.Object, _mockRepository.Object);
    }

    [Test]
    public async Task Run_ValidRequest_ServiceSuccess_ReturnsOkWithResult()
    {
        // Arrange
        var req = new Mock<HttpRequest>().Object;
        string playlistId = "validId";
        int? limit = 5;
        string? filters = "skip-existing";
        var serviceResult = ServiceResult<int>.Success(10);
        _mockPlaylistScanService.Setup(s => s.ScanPlaylistAsync(It.Is<PlaylistScanRequest>(r => r.PlaylistId == playlistId && r.Limit == limit && r.Filters != null && r.Filters.RawFilters == filters)))
                                 .ReturnsAsync(serviceResult);

        // Act
        var result = await _scanPlaylist.Run(req, playlistId, limit, filters);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result;
        Assert.That(okResult.Value, Is.InstanceOf<ScanPlaylistResult>());
        var scanResult = (ScanPlaylistResult)okResult.Value;
        Assert.That(scanResult.VideosProcessed, Is.EqualTo(10));
    }

    [Test]
    public async Task Run_MissingPlaylistId_ReturnsBadRequest()
    {
        // Arrange
        var req = new Mock<HttpRequest>().Object;
        string? playlistId = null;
        int? limit = null;
        string? filters = null;

        // Act
        var result = await _scanPlaylist.Run(req, playlistId, limit, filters);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }


    [Test]
    public void Run_UnhandledException_BubblesUp()
    {
        // Arrange
        var req = new Mock<HttpRequest>().Object;
        string playlistId = "validId";
        int? limit = null;
        string? filters = null;
        _mockPlaylistScanService.Setup(s => s.ScanPlaylistAsync(It.IsAny<PlaylistScanRequest>()))
                                 .ThrowsAsync(new Exception("Unexpected error"));

        // Act & Assert
        Assert.ThrowsAsync<Exception>(async () => await _scanPlaylist.Run(req, playlistId, limit, filters));
    }

    [Test]
    public async Task Run_WithVersionParameter_PassesVersionToRequest()
    {
        // Arrange
        var req = new Mock<HttpRequest>().Object;
        string playlistId = "validId";
        var serviceResult = ServiceResult<int>.Success(5);
        _mockPlaylistScanService.Setup(s => s.ScanPlaylistAsync(It.Is<PlaylistScanRequest>(r =>
            r.PlaylistId == playlistId &&
            r.VersionLabel == "v2.0")))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _scanPlaylist.Run(req, playlistId, limit: null, filters: null, version: "v2.0");

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        _mockPlaylistScanService.Verify(s => s.ScanPlaylistAsync(It.Is<PlaylistScanRequest>(r =>
            r.VersionLabel == "v2.0")), Times.Once);
    }

    [Test]
    public async Task ScheduledRun_TriggersScanWithCorrectParameters()
    {
        // Arrange
        int analyzedCount = 10;
        _mockRepository.Setup(r => r.GetAnalyzedVideoCountAsync()).ReturnsAsync(analyzedCount);
        _mockPlaylistScanService.Setup(s => s.ScanPlaylistAsync(It.IsAny<PlaylistScanRequest>()))
            .ReturnsAsync(ServiceResult<int>.Success(5));

        // Act
        await _scanPlaylist.ScheduledRun(null!);

        // Assert
        _mockRepository.Verify(r => r.GetAnalyzedVideoCountAsync(), Times.Once);
        _mockPlaylistScanService.Verify(s => s.ScanPlaylistAsync(It.Is<PlaylistScanRequest>(r =>
            r.PlaylistId == "PLD4EAA8F8C9148A1B" &&
            r.Limit == analyzedCount + 50 &&
            r.Filters != null &&
            r.Filters.SkipExisting)), Times.Once);
    }
}
