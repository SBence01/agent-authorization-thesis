public sealed record AuthorizationDecision
{
    public bool IsAllowed { get; }
    public AuthorizationReason Reason { get; }

    private AuthorizationDecision(bool isAllowed, AuthorizationReason reason)
    {
        IsAllowed = isAllowed;
        Reason = reason;
    }

    public static AuthorizationDecision Allow()
    {
        return new AuthorizationDecision(true, AuthorizationReason.Allowed);
    }

    public static AuthorizationDecision Deny(AuthorizationReason reason)
    {
        if (reason == AuthorizationReason.Allowed || !Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(reason),
                reason,
                "A denial must have a valid authorization reason.");
        }

        return new AuthorizationDecision(false, reason);
    }
}