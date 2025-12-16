using Moq;
using NUnit.Framework;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using InDepthDispenza.Functions;
using InDepthDispenza.Functions.Interfaces;

namespace IndepthDispenza.Tests;

[TestFixture]
public class ScanPlaylistTests
{
    private Mock<ILogger<ScanPlaylist>> _mockLogger;
    private Mock<IPlaylistScanService> _mockPlaylistScanService;
    private ScanPlaylist _scanPlaylist;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<ScanPlaylist>>();
        _mockPlaylistScanService = new Mock<IPlaylistScanService>();
        _scanPlaylist = new ScanPlaylist(_mockLogger.Object, _mockPlaylistScanService.Object);
    }

    [Test]
    public async Task Run_ValidRequest_ServiceSuccess_ReturnsOkWithResult()
    {
        // Arrange
        var req = new Mock<HttpRequest>().Object;
        string playlistId = "validId";
        int? limit = 5;
        var serviceResult = ServiceResult<int>.Success(10);
        _mockPlaylistScanService.Setup(s => s.ScanPlaylistAsync(It.Is<PlaylistScanRequest>(r => r.PlaylistId == playlistId && r.Limit == limit)))
                                 .ReturnsAsync(serviceResult);

        // Act
        var result = await _scanPlaylist.Run(req, playlistId, limit);

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

        // Act
        var result = await _scanPlaylist.Run(req, playlistId, limit);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Run_ValidRequest_ServiceFailure_ReturnsBadRequestWithError()
    {
        // Arrange
        var req = new Mock<HttpRequest>().Object;
        string playlistId = "validId";
        int? limit = null;
        var serviceResult = ServiceResult<int>.Failure("Service error");
        _mockPlaylistScanService.Setup(s => s.ScanPlaylistAsync(It.IsAny<PlaylistScanRequest>()))
                                 .ReturnsAsync(serviceResult);

        // Act
        var result = await _scanPlaylist.Run(req, playlistId, limit);

        // Assert
        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Run_UnhandledException_ReturnsInternalServerError()
    {
        // Arrange
        var req = new Mock<HttpRequest>().Object;
        string playlistId = "validId";
        int? limit = null;
        _mockPlaylistScanService.Setup(s => s.ScanPlaylistAsync(It.IsAny<PlaylistScanRequest>()))
                                 .ThrowsAsync(new Exception("Unexpected error"));

        // Act
        var result = await _scanPlaylist.Run(req, playlistId, limit);

        // Assert
        Assert.That(result, Is.InstanceOf<StatusCodeResult>());
        var statusResult = (StatusCodeResult)result;
        Assert.That(statusResult.StatusCode, Is.EqualTo(500));
    }
}
