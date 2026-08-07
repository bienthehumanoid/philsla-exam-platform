namespace PhilSLA.ExamPlatform.Core.Incidents;

public sealed record IncidentCategory(
    Guid Id,
    string Name,
    bool IsActive,
    int DisplayOrder)
{
    private Guid _id = RequireId(Id, nameof(Id));
    private string _name = RequireText(Name, nameof(Name));
    private int _displayOrder = RequireDisplayOrder(DisplayOrder);

    public Guid Id
    {
        get => _id;
        init => _id = RequireId(value, nameof(Id));
    }

    public string Name
    {
        get => _name;
        init => _name = RequireText(value, nameof(Name));
    }

    public int DisplayOrder
    {
        get => _displayOrder;
        init => _displayOrder = RequireDisplayOrder(value);
    }

    private static Guid RequireId(Guid value, string name) =>
        value == Guid.Empty ? throw new ArgumentException("An ID is required.", name) : value;

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A name is required.", name)
            : value.Trim();

    private static int RequireDisplayOrder(int value) =>
        value < 0 ? throw new ArgumentOutOfRangeException(nameof(DisplayOrder)) : value;
}
