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
}
