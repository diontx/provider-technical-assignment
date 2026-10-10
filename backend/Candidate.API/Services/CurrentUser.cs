namespace Candidate.API.Services;

/// <summary>Who is making the change, for the CreatedBy / ModifiedBy / DeletedBy audit columns.</summary>
public interface ICurrentUser
{
    string Name { get; }
}

/// <summary>
/// Placeholder: the assignment has no authentication. Replace with an implementation that
/// reads the signed-in user from HttpContext once authentication is added.
/// </summary>
public sealed class AnonymousCurrentUser : ICurrentUser
{
    public const string Placeholder = "anonymous";

    public string Name => Placeholder;
}
