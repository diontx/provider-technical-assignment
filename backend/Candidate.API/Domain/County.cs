namespace Candidate.API.Domain;

/// <summary>Lookup of Georgia counties. Reference data, seeded by migration.</summary>
public class County
{
    public int CountyId { get; set; }
    public string Name { get; set; } = string.Empty;
}
