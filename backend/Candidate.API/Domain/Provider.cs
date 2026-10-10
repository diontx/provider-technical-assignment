namespace Candidate.API.Domain;

public class Provider : AuditableEntity
{
    public const int NameMaxLength = 200;

    public int ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public int CountyId { get; set; }
    public County County { get; set; } = null!;
    public ProviderStatus Status { get; set; }

    public List<License> Licenses { get; set; } = [];
}
