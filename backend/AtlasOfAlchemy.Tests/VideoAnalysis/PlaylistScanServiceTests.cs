using AutoFixture;
using AtlasOfAlchemy.Functions.Interfaces;
using AtlasOfAlchemy.Functions.VideoAnalysis;
using AtlasOfAlchemy.Functions.VideoAnalysis.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace AtlasOfAlchemy.Tests.VideoAnalysis
{
    [TestFixture]
    public class PlaylistScanServiceTests
    {
        private Mock<IPlaylistService> _playlistServiceMock;
        private Mock<IQueueService> _queueServiceMock;
        private List<IVideoFilter> _filters;
        private Mock<ILogger<PlaylistScanService>> _loggerMock;
        private PlaylistScanService _testSubject;
        private Fixture _fixture;

        [SetUp]
        public void Setup()
        {
            _fixture = new Fixture();
            _playlistServiceMock = new Mock<IPlaylistService>();
            _queueServiceMock = new Mock<IQueueService>();
            _filters = new List<IVideoFilter>();
            _loggerMock = new Mock<ILogger<PlaylistScanService>>();
            _testSubject = new PlaylistScanService(_playlistServiceMock.Object, _queueServiceMock.Object, _filters, _loggerMock.Object);
        }

        [Test]
        public async Task ScanPlaylistAsync_HappyPath_ReturnsSuccessWithEnqueuedCount()
        {
            // Arrange
            var request = _fixture.Create<PlaylistScanRequest>() with { Filters = VideoFilters.Empty };
            var videos = _fixture.CreateMany<VideoInfo>(5).ToList();

            _playlistServiceMock.Setup(x => x.GetPlaylistVideosAsync(request.PlaylistId, request.Limit, It.IsAny<Func<VideoInfo, bool>>()))
                .Returns(ToAsyncEnumerable(videos));

            _queueServiceMock.Setup(x => x.EnqueueVideoAsync(It.IsAny<VideoInfo>()))
                .Returns(Task.CompletedTask);

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

        [Test]
        public async Task ScanPlaylistAsync_WithFilter_FiltersVideos()
        {
            // Arrange
            var request = _fixture.Create<PlaylistScanRequest>() with { Filters = VideoFilters.Parse("test-filter") };
            var videos = _fixture.CreateMany<VideoInfo>(3).ToList();

            // Second video should be filtered out
            var filterMock = new Mock<IVideoFilter>();
            filterMock.Setup(x => x.ShouldProcessAsync(It.Is<VideoInfo>(v => v.VideoId == videos[0].VideoId), request)).ReturnsAsync(true);
            filterMock.Setup(x => x.ShouldProcessAsync(It.Is<VideoInfo>(v => v.VideoId == videos[1].VideoId), request)).ReturnsAsync(false);
            filterMock.Setup(x => x.ShouldProcessAsync(It.Is<VideoInfo>(v => v.VideoId == videos[2].VideoId), request)).ReturnsAsync(true);
            _filters.Add(filterMock.Object);

            _playlistServiceMock.Setup(x => x.GetPlaylistVideosAsync(request.PlaylistId, request.Limit, It.IsAny<Func<VideoInfo, bool>>()))
                .Returns(ToAsyncEnumerable(videos));

            _queueServiceMock.Setup(x => x.EnqueueVideoAsync(It.IsAny<VideoInfo>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await InvokeScanPlaylistAsync(request);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data, Is.EqualTo(2));
            _queueServiceMock.Verify(x => x.EnqueueVideoAsync(It.Is<VideoInfo>(v => v.VideoId == videos[0].VideoId)), Times.Once);
            _queueServiceMock.Verify(x => x.EnqueueVideoAsync(It.Is<VideoInfo>(v => v.VideoId == videos[1].VideoId)), Times.Never);
            _queueServiceMock.Verify(x => x.EnqueueVideoAsync(It.Is<VideoInfo>(v => v.VideoId == videos[2].VideoId)), Times.Once);
        }

        [Test]
        public async Task ScanPlaylistAsync_FilterThrows_SkipsVideoAndContinues()
        {
            // Arrange
            var request = _fixture.Create<PlaylistScanRequest>();
            var videos = _fixture.CreateMany<VideoInfo>(2).ToList();

            var filterMock = new Mock<IVideoFilter>();
            filterMock.Setup(x => x.ShouldProcessAsync(It.Is<VideoInfo>(v => v.VideoId == videos[0].VideoId), request)).ThrowsAsync(new Exception("Filter error"));
            filterMock.Setup(x => x.ShouldProcessAsync(It.Is<VideoInfo>(v => v.VideoId == videos[1].VideoId), request)).ReturnsAsync(true);
            _filters.Add(filterMock.Object);

            _playlistServiceMock.Setup(x => x.GetPlaylistVideosAsync(request.PlaylistId, request.Limit, It.IsAny<Func<VideoInfo, bool>>()))
                .Returns(ToAsyncEnumerable(videos));

            _queueServiceMock.Setup(x => x.EnqueueVideoAsync(It.IsAny<VideoInfo>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await InvokeScanPlaylistAsync(request);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data, Is.EqualTo(1)); // Only the second video should be enqueued
            _queueServiceMock.Verify(x => x.EnqueueVideoAsync(It.Is<VideoInfo>(v => v.VideoId == videos[0].VideoId)), Times.Never);
            _queueServiceMock.Verify(x => x.EnqueueVideoAsync(It.Is<VideoInfo>(v => v.VideoId == videos[1].VideoId)), Times.Once);
        }

        [Test]
        public async Task ScanPlaylistAsync_PartialQueueFailure_ContinuesAndReturnsPartialCount()
        {
            // Arrange
            var request = _fixture.Create<PlaylistScanRequest>() with { Filters = VideoFilters.Empty };
            var videos = _fixture.CreateMany<VideoInfo>(3).ToList();

            _playlistServiceMock.Setup(x => x.GetPlaylistVideosAsync(request.PlaylistId, request.Limit, It.IsAny<Func<VideoInfo, bool>>()))
                .Returns(ToAsyncEnumerable(videos));

            // Fail the second video with a transient exception
            _queueServiceMock.SetupSequence(x => x.EnqueueVideoAsync(It.IsAny<VideoInfo>()))
                .Returns(Task.CompletedTask)
                .Throws(new QueueTransientException("Transient error"))
                .Returns(Task.CompletedTask);

            // Act
            var result = await InvokeScanPlaylistAsync(request);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Data, Is.EqualTo(2)); // 1st and 3rd succeeded
            _queueServiceMock.Verify(x => x.EnqueueVideoAsync(It.IsAny<VideoInfo>()), Times.Exactly(3));
        }

        [Test]
        public void ScanPlaylistAsync_FatalQueueFailure_ThrowsException()
        {
            // Arrange
            var request = _fixture.Create<PlaylistScanRequest>() with { Filters = VideoFilters.Empty };
            var videos = _fixture.CreateMany<VideoInfo>(1).ToList();

            _playlistServiceMock.Setup(x => x.GetPlaylistVideosAsync(request.PlaylistId, request.Limit, It.IsAny<Func<VideoInfo, bool>>()))
                .Returns(ToAsyncEnumerable(videos));

            _queueServiceMock.Setup(x => x.EnqueueVideoAsync(It.IsAny<VideoInfo>()))
                .ThrowsAsync(new QueueConfigurationException("Fatal error"));

            // Act & Assert
            Assert.ThrowsAsync<QueueConfigurationException>(async () => await InvokeScanPlaylistAsync(request));
        }

        [Test]
        public async Task ScanPlaylistAsync_WithVersionLabel_PropagatesVersionLabelToEnqueuedVideos()
        {
            // Arrange
            var versionLabel = "v2.0-taxonomy";
            var request = _fixture.Create<PlaylistScanRequest>() with
            {
                Filters = VideoFilters.Empty,
                VersionLabel = versionLabel
            };
            var videos = _fixture.CreateMany<VideoInfo>(2).Select(v => v with { VersionLabel = null }).ToList();
            var enqueuedVideos = new List<VideoInfo>();

            _playlistServiceMock.Setup(x => x.GetPlaylistVideosAsync(request.PlaylistId, request.Limit, It.IsAny<Func<VideoInfo, bool>>()))
                .Returns(ToAsyncEnumerable(videos));

            _queueServiceMock.Setup(x => x.EnqueueVideoAsync(It.IsAny<VideoInfo>()))
                .Callback<VideoInfo>(v => enqueuedVideos.Add(v))
                .Returns(Task.CompletedTask);

            // Act
            var result = await InvokeScanPlaylistAsync(request);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(enqueuedVideos, Has.Count.EqualTo(2));
            Assert.That(enqueuedVideos, Has.All.Property(nameof(VideoInfo.VersionLabel)).EqualTo(versionLabel));
        }

        [Test]
        public async Task ScanPlaylistAsync_WithoutVersionLabel_EnqueuesVideosWithNullVersionLabel()
        {
            // Arrange
            var request = _fixture.Create<PlaylistScanRequest>() with
            {
                Filters = VideoFilters.Empty,
                VersionLabel = null
            };
            var videos = _fixture.CreateMany<VideoInfo>(2).Select(v => v with { VersionLabel = null }).ToList();
            var enqueuedVideos = new List<VideoInfo>();

            _playlistServiceMock.Setup(x => x.GetPlaylistVideosAsync(request.PlaylistId, request.Limit, It.IsAny<Func<VideoInfo, bool>>()))
                .Returns(ToAsyncEnumerable(videos));

            _queueServiceMock.Setup(x => x.EnqueueVideoAsync(It.IsAny<VideoInfo>()))
                .Callback<VideoInfo>(v => enqueuedVideos.Add(v))
                .Returns(Task.CompletedTask);

            // Act
            var result = await InvokeScanPlaylistAsync(request);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(enqueuedVideos, Has.Count.EqualTo(2));
            Assert.That(enqueuedVideos, Has.All.Property(nameof(VideoInfo.VersionLabel)).Null);
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
