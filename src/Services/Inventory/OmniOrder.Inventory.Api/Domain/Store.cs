namespace OmniOrder.Inventory.Api.Domain;

public sealed class Store
{
    private Store()
    {
    }

    public Store(Guid id, string code, string name, string city, string address)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Code = code;
        Name = name;
        City = city;
        Address = address;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public string Address { get; private set; } = string.Empty;
}
