using System.ComponentModel;

public enum AuthorizationReason
{
    [Description("The operation was authorized.")]
    Allowed,
    [Description("No matching grant found for the requested authorization.")]
    NoMatchingGrant,
    [Description("The grant has expired.")]
    GrantExpired,
    [Description("The grant has been revoked.")]
    GrantRevoked
}