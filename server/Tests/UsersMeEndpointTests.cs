using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TournamentPlatformSystemWebApi.API.Controllers;
using TournamentPlatformSystemWebApi.Application.DTOs.Auth;
using TournamentPlatformSystemWebApi.Application.Interfaces;
using TournamentPlatformSystemWebApi.Common.Models;
using TournamentPlatformSystemWebApi.Core.Entities;
using Xunit;

namespace Tests
{
    /// <summary>RFC-001: GET /api/v1/users/me returns the token owner's profile instead of a stub.</summary>
    public class UsersMeEndpointTests
    {
        private static AuthController ControllerFor(string? sub)
        {
            var identity = sub == null ? new ClaimsIdentity() : new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, sub) }, "test");
            return new AuthController(Mock.Of<IAuthenticationService>())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } }
            };
        }

        [Fact]
        public async Task Me_ReturnsProfileOfTokenOwner()
        {
            var id = Guid.NewGuid();
            var repo = new Mock<IUserRepository>();
            repo.Setup(r => r.GetUserWithDetails(id)).ReturnsAsync(new User
            {
                Id = id,
                FullName = "Olena Organizer",
                IsOrganizer = true,
                UserDetail = new UserDetail { Email = "olena@example.com" }
            });

            var result = await ControllerFor(id.ToString()).Me(repo.Object);

            var dto = Assert.IsType<UserDto>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.Equal(id, dto.Id);
            Assert.Equal("olena@example.com", dto.Email);
            Assert.Equal("Olena Organizer", dto.FullName);
            Assert.Equal("organizer", dto.Role);
        }

        [Fact]
        public async Task Me_WithoutSubjectClaim_Returns401()
        {
            var repo = new Mock<IUserRepository>();

            var result = await ControllerFor(null).Me(repo.Object);

            var body = Assert.IsType<ErrorResponseDto>(Assert.IsType<UnauthorizedObjectResult>(result).Value);
            Assert.Equal(401, body.Error.Code);
            repo.Verify(r => r.GetUserWithDetails(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task Me_ForDeletedUser_Returns404()
        {
            var repo = new Mock<IUserRepository>();
            repo.Setup(r => r.GetUserWithDetails(It.IsAny<Guid>())).ReturnsAsync((User)null!);

            var result = await ControllerFor(Guid.NewGuid().ToString()).Me(repo.Object);

            var body = Assert.IsType<ErrorResponseDto>(Assert.IsType<NotFoundObjectResult>(result).Value);
            Assert.Equal(404, body.Error.Code);
        }
    }
}