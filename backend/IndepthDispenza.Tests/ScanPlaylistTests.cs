using Moq;
using NUnit.Framework;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using InDepthDispenza.Functions;

namespace IndepthDispenza.Tests;

[TestFixture]
public class ScanPlaylistTests
{
    [Test]
    public void NoLimit_ReturnsZeroOrchestrationCount()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ScanPlaylist>>();
        var mockReq = new Mock<HttpRequest>().Object;
        var sut = new ScanPlaylist(mockLogger.Object);

        // Act
        var result = sut.Run(mockReq, null);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result;
        Assert.That(okResult.Value, Is.InstanceOf<ScanPlaylistResult>());
        Assert.That(((ScanPlaylistResult)okResult.Value!).OrchestrationCount, Is.EqualTo(0));
        mockLogger.Verify(l => l.LogInformation(It.IsAny<string>()), Times.Once);
    }

    [Test]
    public void WithLimit_ReturnsProvidedLimit()
    {
        // Arrange
        const int testLimit = 42;
        var mockLogger = new Mock<ILogger<ScanPlaylist>>();
        var mockReq = new Mock<HttpRequest>().Object;
        var sut = new ScanPlaylist(mockLogger.Object);

        // Act
        var result = sut.Run(mockReq, testLimit);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result;
        Assert.That(okResult.Value, Is.InstanceOf<ScanPlaylistResult>());
        Assert.That(((ScanPlaylistResult)okResult.Value!).OrchestrationCount, Is.EqualTo(testLimit));
        mockLogger.Verify(l => l.LogInformation(It.IsAny<string>()), Times.Once);
    }
}
