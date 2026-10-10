namespace Candidate.API.Domain;

/// <summary>Business lifecycle state. Separate from soft deletion, which is a record state.</summary>
public enum ProviderStatus
{
    Active,
    Inactive,
    Pending
}

/// <summary>Status as stored. Validity is decided together with the expiration date; see <see cref="LicenseRules"/>.</summary>
public enum LicenseStatus
{
    Active,
    Expired,
    Suspended
}
