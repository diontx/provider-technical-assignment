namespace Candidate.API.Domain;

/// <summary>
/// Audit, soft-delete, and optimistic concurrency columns shared by Provider and License.
/// These are stamped by <see cref="Data.AuditSoftDeleteInterceptor"/>; application code does not set them.
/// </summary>
public abstract class AuditableEntity
{
    public DateTime CreatedUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedUtc { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>Replaced on every update so two editors cannot silently overwrite each other.</summary>
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
