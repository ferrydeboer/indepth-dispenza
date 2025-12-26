using AutoFixture;
using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace IndepthDispenza.Tests.VideoAnalysis
{
    [TestFixture]
    public class PlaylistScanServiceTests
    {
        private Mock<IPlaylistService> _playlistServiceMock;
        private Mock<IQueueService> _queueServiceMock;
        private Mock<ILogger<PlaylistScanService>> _loggerMock;
        private PlaylistScanService _testSubject;
        private Fixture _fixture;

        [SetUp]
        public void Setup()
        {
            _fixture = new Fixture();
            _playlistServiceMock = new Mock<IPlaylistService>();
            _queueServiceMock = new Mock<IQueueService>();
            _loggerMock = new Mock<ILogger<PlaylistScanService>>();
            _testSubject = new PlaylistScanService(_playlistServiceMock.Object, _queueServiceMock.Object, _loggerMock.Object);
        }

        [Test]
        public async Task ScanPlaylistAsync_HappyPath_ReturnsSuccessWithEnqueuedCount()
        {
            // Arrange
            var request = _fixture.Create<PlaylistScanRequest>();
            var videos = _fixture.CreateMany<VideoInfo>(5).ToList();

            _playlistServiceMock.Setup(x => x.GetPlaylistVideosAsync(request.PlaylistId, request.Limit, It.IsAny<Func<VideoInfo, bool>>()))
                .Returns(ToAsyncEnumerable(videos));

            _queueServiceMock.Setup(x => x.EnqueueVideoAsync(It.IsAny<VideoInfo>()))
                .ReturnsAsync(ServiceResult.Success());

            // Act
            var result = await InvokeScanPlaylistAsync(request);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Data, Is.EqualTo(videos.Count));
            }

            _playlistServiceMock.Verify(x => x.GetPlaylistVideosAsync(request.PlaylistId, request.Limit, It.IsAny<Func<VideoInfo, bool>>()), Times.Once);
            _queueServiceMock.Verify(x => x.EnqueueVideoAsync(It.IsAny<VideoInfo>()), Times.Exactly(videos.Count));
        }

        private async Task<ServiceResult<int>> InvokeScanPlaylistAsync(PlaylistScanRequest request)
        {
            return await _testSubject.ScanPlaylistAsync(request);
        }

        private static async IAsyncEnumerable<VideoInfo> ToAsyncEnumerable(IEnumerable<VideoInfo> items)
        {
            foreach (var item in items)
            {
                yield return item;
                await Task.Yield();
            }
        }
    }
}
