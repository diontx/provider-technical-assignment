namespace Candidate.API.Domain;

public class License : AuditableEntity
{
    public const int NumberMaxLength = 50;

    public int LicenseId { get; set; }
    public int ProviderId { get; set; }
    public Provider Provider { get; set; } = null!;
    public string LicenseNumber { get; set; } = string.Empty;
    public LicenseStatus LicenseStatus { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
}
