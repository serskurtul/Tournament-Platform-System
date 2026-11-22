using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TournamentPlatformSystemWebApi.API.Controllers;
using TournamentPlatformSystemWebApi.Application.Interfaces;
using TournamentPlatformSystemWebApi.Common.Exceptions;
using TournamentPlatformSystemWebApi.Common.Models;
using TournamentPlatformSystemWebApi.Infrastructure.Services;
using Xunit;

namespace Tests
{
    /// <summary>CAPA CA-1 (INC-001): a storage outage is a 503 with a neutral message and an error log.</summary>
    public class BannerStorageFailureTests
    {
        [Fact]
        public async Task StorageService_WithoutConfiguration_ThrowsStorageUnavailable()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            var service = new GoogleDriveStorageService(config);

            await Assert.ThrowsAsync<StorageUnavailableException>(() => service.UploadAsync(new MemoryStream(new byte[] { 1 }), "a.png", "image/png"));
        }

        [Fact]
        public async Task UploadImage_WhenStorageUnavailable_Returns503WithoutInternalDetailsAndLogsError()
        {
            var organizerId = Guid.NewGuid();
            var management = new Mock<ITournamentManagementService>();
            management.Setup(m => m.UploadImageAsync(It.IsAny<Guid>(), organizerId, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>()))
                .ThrowsAsync(new StorageUnavailableException("Google Cloud Storage rejected the upload", new Exception("invalid_grant: account not found")));
            var logger = new Mock<ILogger<TournamentsController>>();
            var controller = new TournamentsController(management.Object, Mock.Of<IParticipantService>(), Mock.Of<IMatchService>(),
                Mock.Of<ITournamentDetailsService>(), Mock.Of<ITournamentCatalogService>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, organizerId.ToString()), new Claim(ClaimTypes.Role, "organizer") }, "test"))
                    }
                }
            };
            var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
            var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "banner.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };

            var result = await controller.UploadImage(Guid.NewGuid(), file, logger.Object);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(503, objectResult.StatusCode);
            var body = Assert.IsType<ErrorResponseDto>(objectResult.Value);
            Assert.Equal("StorageUnavailable", body.Error.Type);
            Assert.DoesNotContain("invalid_grant", body.Error.Message);
            logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }
    }
}