using AgentAuthorization.Core.Domain.Authorization;

namespace AgentAuthorization.Core.Tests.Domain.Authorization;

public sealed class AuthorizationDecisionTests
{
    [Fact]
    public void Allow_ShouldReturnAllowedDecision()
    {
        //Act
        var decision = AuthorizationDecision.Allow();

        //Assert
        Assert.True(decision.IsAllowed);
        Assert.Equal(AuthorizationReason.Allowed, decision.Reason);
    }

    [Theory]
    [InlineData(AuthorizationReason.NoMatchingGrant)]
    [InlineData(AuthorizationReason.AgentMismatch)]
    [InlineData(AuthorizationReason.WorkflowMismatch)]
    [InlineData(AuthorizationReason.ActionNotAllowed)]
    [InlineData(AuthorizationReason.ResourceOutOfScope)]
    [InlineData(AuthorizationReason.GrantNotYetValid)]
    [InlineData(AuthorizationReason.GrantExpired)]
    [InlineData(AuthorizationReason.GrantRevoked)]
    public void Deny_WithValidReason_ShouldReturnDeniedDecision(AuthorizationReason reason)
    {
        //Act
        var decision = AuthorizationDecision.Deny(reason);

        //Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(reason, decision.Reason);
    }

    [Fact]
    public void Deny_WithAllowedReason_ShouldThrowArgumentOutOfRangeException()
    {
        //Act
        Action act = () => AuthorizationDecision.Deny(AuthorizationReason.Allowed);

        //Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Deny_WithUndefinedReason_ShouldThrowArgumentOutOfRangeException()
    {
        //Arrange
        var invalidReason = (AuthorizationReason)999;

        //Act
        Action act = () => AuthorizationDecision.Deny(invalidReason);

        //Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }
}
