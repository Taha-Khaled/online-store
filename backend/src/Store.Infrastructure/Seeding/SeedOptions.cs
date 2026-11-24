namespace Store.Infrastructure.Seeding;

/// <summary>Settings of the "Seed" configuration section.</summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>When false, the seeder does nothing at all.</summary>
    public bool Enabled { get; set; } = true;

    public string? AdminPassword { get; set; }

    public string? CustomerPassword { get; set; }

    /// <summary>Folder that holds the seed images; a missing file only logs a warning.</summary>
    public string? ImagesPath { get; set; }
}
