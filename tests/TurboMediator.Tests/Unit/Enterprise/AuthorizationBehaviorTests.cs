using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TurboMediator.Enterprise.Authorization;
using Xunit;

namespace TurboMediator.Tests.Enterprise;

public class AuthorizationBehaviorTests
{
    #region Test Messages

    [Authorize]
    public record AuthenticatedCommand : IRequest<string>;

    [Authorize(Policy = "AdminPolicy")]
    public record AdminPolicyCommand : IRequest<string>;

    [Authorize(Roles = "Admin,Manager")]
    public record RoleBasedCommand : IRequest<string>;

    [Authorize]
    [AllowAnonymous]
    public record AnonymousAllowedCommand : IRequest<string>;

    public record NoAuthCommand : IRequest<string>;

    [Authorize(AuthenticationSchemes = "Bearer")]
    public record BearerOnlyCommand : IRequest<string>;

    [Authorize(AuthenticationSchemes = "Bearer,Cookie")]
    public record MultipleSchemesCommand : IRequest<string>;

    [Authorize]
    public record NoSchemeCommand : IRequest<string>;

    [Authorize(AuthenticationSchemes = "Bearer", Roles = "Admin")]
    public record BearerAndRoleCommand : IRequest<string>;

    #endregion

    #region Test User Context

    private class TestUserContext : IUserContext
    {
        public ClaimsPrincipal? User { get; set; }
        public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
    }

    private static ClaimsPrincipal CreateUser(string name, string authScheme, params Claim[] additionalClaims)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, name) };
        claims.AddRange(additionalClaims);
        var identity = new ClaimsIdentity(claims, authScheme);
        return new ClaimsPrincipal(identity);
    }

    #endregion

    [Fact]
    public async Task Handle_NoAuthorizeAttribute_ExecutesHandler()
    {
        // Arrange
        var userContext = new TestUserContext { User = null };
        var policyProvider = new DefaultAuthorizationPolicyProvider();
        var behavior = new AuthorizationBehavior<NoAuthCommand, string>(userContext, policyProvider);
        var message = new NoAuthCommand();
        var handlerCalled = false;

        MessageHandlerDelegate<NoAuthCommand, string> next = (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<string>("Success");
        };

        // Act
        var result = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        Assert.True(handlerCalled);
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task Handle_AllowAnonymous_ExecutesHandler()
    {
        // Arrange
        var userContext = new TestUserContext { User = null };
        var policyProvider = new DefaultAuthorizationPolicyProvider();
        var behavior = new AuthorizationBehavior<AnonymousAllowedCommand, string>(userContext, policyProvider);
        var message = new AnonymousAllowedCommand();
        var handlerCalled = false;

        MessageHandlerDelegate<AnonymousAllowedCommand, string> next = (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<string>("Success");
        };

        // Act
        var result = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        Assert.True(handlerCalled);
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task Handle_RequiresAuth_NotAuthenticated_ThrowsUnauthorized()
    {
        // Arrange
        var userContext = new TestUserContext { User = null };
        var policyProvider = new DefaultAuthorizationPolicyProvider();
        var behavior = new AuthorizationBehavior<AuthenticatedCommand, string>(userContext, policyProvider);
        var message = new AuthenticatedCommand();

        MessageHandlerDelegate<AuthenticatedCommand, string> next = (msg, ct) => new ValueTask<string>("Success");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            async () => await behavior.Handle(message, next, CancellationToken.None));

        Assert.Equal(typeof(AuthenticatedCommand), exception.MessageType);
    }

    [Fact]
    public async Task Handle_RequiresAuth_Authenticated_ExecutesHandler()
    {
        // Arrange
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "TestUser") }, "TestAuth");
        var user = new ClaimsPrincipal(identity);
        var userContext = new TestUserContext { User = user };
        var policyProvider = new DefaultAuthorizationPolicyProvider();
        var behavior = new AuthorizationBehavior<AuthenticatedCommand, string>(userContext, policyProvider);
        var message = new AuthenticatedCommand();
        var handlerCalled = false;

        MessageHandlerDelegate<AuthenticatedCommand, string> next = (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<string>("Success");
        };

        // Act
        var result = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        Assert.True(handlerCalled);
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task Handle_RequiresRole_UserHasRole_ExecutesHandler()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "TestUser"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);
        var userContext = new TestUserContext { User = user };
        var policyProvider = new DefaultAuthorizationPolicyProvider();
        var behavior = new AuthorizationBehavior<RoleBasedCommand, string>(userContext, policyProvider);
        var message = new RoleBasedCommand();
        var handlerCalled = false;

        MessageHandlerDelegate<RoleBasedCommand, string> next = (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<string>("Success");
        };

        // Act
        var result = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        Assert.True(handlerCalled);
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task Handle_RequiresRole_UserMissingRole_ThrowsUnauthorized()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "TestUser"),
            new Claim(ClaimTypes.Role, "User")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);
        var userContext = new TestUserContext { User = user };
        var policyProvider = new DefaultAuthorizationPolicyProvider();
        var behavior = new AuthorizationBehavior<RoleBasedCommand, string>(userContext, policyProvider);
        var message = new RoleBasedCommand();

        MessageHandlerDelegate<RoleBasedCommand, string> next = (msg, ct) => new ValueTask<string>("Success");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            async () => await behavior.Handle(message, next, CancellationToken.None));

        Assert.Equal(typeof(RoleBasedCommand), exception.MessageType);
        Assert.NotNull(exception.RequiredRoles);
        Assert.Contains("Admin", exception.RequiredRoles);
    }

    [Fact]
    public async Task Handle_RequiresPolicy_PolicyPasses_ExecutesHandler()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "TestAdmin"),
            new Claim("IsAdmin", "true")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);
        var userContext = new TestUserContext { User = user };
        var policyProvider = new DefaultAuthorizationPolicyProvider();
        policyProvider.AddPolicy("AdminPolicy", u => u.HasClaim(c => c.Type == "IsAdmin" && c.Value == "true"));
        var behavior = new AuthorizationBehavior<AdminPolicyCommand, string>(userContext, policyProvider);
        var message = new AdminPolicyCommand();
        var handlerCalled = false;

        MessageHandlerDelegate<AdminPolicyCommand, string> next = (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<string>("Success");
        };

        // Act
        var result = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        Assert.True(handlerCalled);
        Assert.Equal("Success", result);
    }

    [Fact]
    public async Task Handle_RequiresPolicy_PolicyFails_ThrowsUnauthorized()
    {
        // Arrange
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "TestUser") }, "TestAuth");
        var user = new ClaimsPrincipal(identity);
        var userContext = new TestUserContext { User = user };
        var policyProvider = new DefaultAuthorizationPolicyProvider();
        policyProvider.AddPolicy("AdminPolicy", u => u.HasClaim(c => c.Type == "IsAdmin" && c.Value == "true"));
        var behavior = new AuthorizationBehavior<AdminPolicyCommand, string>(userContext, policyProvider);
        var message = new AdminPolicyCommand();

        MessageHandlerDelegate<AdminPolicyCommand, string> next = (msg, ct) => new ValueTask<string>("Success");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            async () => await behavior.Handle(message, next, CancellationToken.None));

        Assert.Equal(typeof(AdminPolicyCommand), exception.MessageType);
        Assert.Equal("AdminPolicy", exception.Policy);
    }

    #region AuthenticationSchemes Tests

    [Fact]
    public async Task AuthScheme_MatchingScheme_ShouldSucceed()
    {
        // Arrange
        var user = CreateUser("TestUser", "Bearer");
        var userContext = new TestUserContext { User = user };
        var behavior = new AuthorizationBehavior<BearerOnlyCommand, string>(userContext);
        var message = new BearerOnlyCommand();
        var handlerCalled = false;

        MessageHandlerDelegate<BearerOnlyCommand, string> next = (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<string>("Success");
        };

        // Act
        var result = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeTrue();
        result.Should().Be("Success");
    }

    [Fact]
    public async Task AuthScheme_NonMatchingScheme_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var user = CreateUser("TestUser", "Cookie");
        var userContext = new TestUserContext { User = user };
        var behavior = new AuthorizationBehavior<BearerOnlyCommand, string>(userContext);
        var message = new BearerOnlyCommand();

        MessageHandlerDelegate<BearerOnlyCommand, string> next = (msg, ct) => new ValueTask<string>("Success");

        // Act
        var act = async () => await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<UnauthorizedException>();
        exception.Which.MessageType.Should().Be(typeof(BearerOnlyCommand));
        exception.Which.AuthenticationScheme.Should().Be("Bearer");
    }

    [Fact]
    public async Task AuthScheme_NotSet_ShouldNotCheckScheme_BackwardCompatible()
    {
        // Arrange — NoSchemeCommand has [Authorize] without AuthenticationSchemes
        var user = CreateUser("TestUser", "AnyScheme");
        var userContext = new TestUserContext { User = user };
        var behavior = new AuthorizationBehavior<NoSchemeCommand, string>(userContext);
        var message = new NoSchemeCommand();
        var handlerCalled = false;

        MessageHandlerDelegate<NoSchemeCommand, string> next = (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<string>("Success");
        };

        // Act
        var result = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeTrue();
        result.Should().Be("Success");
    }

    [Fact]
    public async Task AuthScheme_MultipleSchemesCommaSeparated_UserWithAnyMatchingScheme_ShouldSucceed()
    {
        // Arrange — MultipleSchemesCommand requires "Bearer,Cookie"
        var user = CreateUser("TestUser", "Cookie");
        var userContext = new TestUserContext { User = user };
        var behavior = new AuthorizationBehavior<MultipleSchemesCommand, string>(userContext);
        var message = new MultipleSchemesCommand();
        var handlerCalled = false;

        MessageHandlerDelegate<MultipleSchemesCommand, string> next = (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<string>("Success");
        };

        // Act
        var result = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeTrue();
        result.Should().Be("Success");
    }

    [Fact]
    public async Task AuthScheme_MultipleSchemesCommaSeparated_UserWithNoMatchingScheme_ShouldThrow()
    {
        // Arrange — MultipleSchemesCommand requires "Bearer,Cookie"; user has "ApiKey"
        var user = CreateUser("TestUser", "ApiKey");
        var userContext = new TestUserContext { User = user };
        var behavior = new AuthorizationBehavior<MultipleSchemesCommand, string>(userContext);
        var message = new MultipleSchemesCommand();

        MessageHandlerDelegate<MultipleSchemesCommand, string> next = (msg, ct) => new ValueTask<string>("Success");

        // Act
        var act = async () => await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<UnauthorizedException>();
        exception.Which.AuthenticationScheme.Should().Be("Bearer,Cookie");
    }

    [Fact]
    public async Task AuthScheme_CaseInsensitiveMatch_ShouldSucceed()
    {
        // Arrange — BearerOnlyCommand requires "Bearer"; user authenticated with "bearer" (lowercase)
        var user = CreateUser("TestUser", "bearer");
        var userContext = new TestUserContext { User = user };
        var behavior = new AuthorizationBehavior<BearerOnlyCommand, string>(userContext);
        var message = new BearerOnlyCommand();
        var handlerCalled = false;

        MessageHandlerDelegate<BearerOnlyCommand, string> next = (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<string>("Success");
        };

        // Act
        var result = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeTrue();
        result.Should().Be("Success");
    }

    [Fact]
    public async Task AuthScheme_WithRolesCheck_BothPass_ShouldSucceed()
    {
        // Arrange
        var user = CreateUser("TestUser", "Bearer", new Claim(ClaimTypes.Role, "Admin"));
        var userContext = new TestUserContext { User = user };
        var behavior = new AuthorizationBehavior<BearerAndRoleCommand, string>(userContext);
        var message = new BearerAndRoleCommand();
        var handlerCalled = false;

        MessageHandlerDelegate<BearerAndRoleCommand, string> next = (msg, ct) =>
        {
            handlerCalled = true;
            return new ValueTask<string>("Success");
        };

        // Act
        var result = await behavior.Handle(message, next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeTrue();
        result.Should().Be("Success");
    }

    [Fact]
    public async Task AuthScheme_WithRolesCheck_WrongScheme_ShouldThrow()
    {
        // Arrange — BearerAndRoleCommand requires scheme=Bearer and role=Admin; user has Cookie + Admin
        var user = CreateUser("TestUser", "Cookie", new Claim(ClaimTypes.Role, "Admin"));
        var userContext = new TestUserContext { User = user };
        var behavior = new AuthorizationBehavior<BearerAndRoleCommand, string>(userContext);
        var message = new BearerAndRoleCommand();

        MessageHandlerDelegate<BearerAndRoleCommand, string> next = (msg, ct) => new ValueTask<string>("Success");

        // Act
        var act = async () => await behavior.Handle(message, next, CancellationToken.None);

        // Assert — should fail on scheme check (roles pass but scheme doesn't)
        // Note: roles are checked first in the behavior, then schemes
        // Since roles pass but scheme fails, we expect UnauthorizedException with AuthenticationScheme set
        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    #endregion
}
