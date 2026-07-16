using System.Text;
using FluentAssertions;
using Moq;
using SmartNest.PlatformService.Handlers.Media;
using SmartNest.PlatformService.Infrastructure;

namespace SmartNest.PlatformService.Tests.Handlers.Media;

public class UploadMediaHandlerTests
{
    [Fact]
    public async Task HandleAsync_Throws_WhenContentTypeIsUnsupported()
    {
        var factory = new Mock<IBlobStorageClientFactory>();
        var handler = new UploadMediaHandler(factory.Object, "media-uploads");
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("data"));

        var act = () => handler.HandleAsync("device-1", "text/plain", content.Length, content);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenPayloadExceedsSizeLimit()
    {
        var factory = new Mock<IBlobStorageClientFactory>();
        var handler = new UploadMediaHandler(factory.Object, "media-uploads");
        using var content = new MemoryStream(new byte[10]);

        var act = () => handler.HandleAsync("device-1", "image/jpeg", 11 * 1024 * 1024, content);

        await act.Should().ThrowAsync<UploadMediaHandler.PayloadTooLargeException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenDeviceIdIsMissing()
    {
        var factory = new Mock<IBlobStorageClientFactory>();
        var handler = new UploadMediaHandler(factory.Object, "media-uploads");
        using var content = new MemoryStream(new byte[10]);

        var act = () => handler.HandleAsync("", "image/jpeg", 10, content);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenDeviceIdContainsSlash()
    {
        var factory = new Mock<IBlobStorageClientFactory>();
        var handler = new UploadMediaHandler(factory.Object, "media-uploads");
        using var content = new MemoryStream(new byte[10]);

        var act = () => handler.HandleAsync("device/../other", "image/jpeg", 10, content);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenContentLengthIsMissing()
    {
        var factory = new Mock<IBlobStorageClientFactory>();
        var handler = new UploadMediaHandler(factory.Object, "media-uploads");
        using var content = new MemoryStream(new byte[10]);

        var act = () => handler.HandleAsync("device-1", "image/jpeg", null, content);

        await act.Should().ThrowAsync<UploadMediaHandler.LengthRequiredException>();
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenContentLengthIsZeroOrNegative()
    {
        var factory = new Mock<IBlobStorageClientFactory>();
        var handler = new UploadMediaHandler(factory.Object, "media-uploads");
        using var content = new MemoryStream(new byte[10]);

        var act = () => handler.HandleAsync("device-1", "image/jpeg", 0, content);

        await act.Should().ThrowAsync<UploadMediaHandler.LengthRequiredException>();
    }

    [Fact]
    public async Task HandleAsync_Accepts_ContentTypeWithMimeParameters()
    {
        var containerClient = new Mock<Azure.Storage.Blobs.BlobContainerClient>();
        var blobClient = new Mock<Azure.Storage.Blobs.BlobClient>();
        containerClient.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(blobClient.Object);
        blobClient
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Azure.Response<Azure.Storage.Blobs.Models.BlobContentInfo>)null!);
        var factory = new Mock<IBlobStorageClientFactory>();
        factory.Setup(f => f.GetContainerClient("media-uploads")).Returns(containerClient.Object);
        var handler = new UploadMediaHandler(factory.Object, "media-uploads");
        using var content = new MemoryStream(new byte[10]);

        var result = await handler.HandleAsync("device-1", "image/jpeg; charset=binary", 10, content);

        result.ContentType.Should().Be("image/jpeg");
    }
}
