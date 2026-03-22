namespace SpireArena;

/// <summary>
/// Represents a single card entry from the tier list.
/// </summary>
public class CardTierEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Cost { get; set; }
    public string Type { get; set; } = "";
    public string Rarity { get; set; } = "";
    public string Character { get; set; } = "";
    public int BaseRating { get; set; }
    public string[] Tags { get; set; } = [];
}
