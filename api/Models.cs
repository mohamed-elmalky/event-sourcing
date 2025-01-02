namespace Models;

public abstract record Participant(string? Id);

public record Person(string? Id) : Participant(Id)
{
    public bool IsActive { get; set; }
    public string? Name { get; set; }
    public string? SSN { get; set; }
    public string? HomePhone { get; set; }
    public string? MobilePhone { get; set; }
    public Address? Address { get; set; }
    public string? Email { get; set; }
}

public record PersonRequest
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? SSN { get; set; }
    public string? HomePhone { get; set; }
    public string? MobilePhone { get; set; }
    public Address? Address { get; set; }
    public string? Email { get; set; }

    public Person ToModel() => new(Id)
    {
        Name = Name,
        SSN = SSN,
        HomePhone = HomePhone,
        MobilePhone = MobilePhone,
        Address = Address,
        Email = Email
    };
}

public record Address(string Address1, string Address2, string City, string State, string Country, string ZipCode)
{
    public override string ToString() => $"{Address1}, {Address2}, {City}, {State}, {Country}, {ZipCode}";
}

public class ParticipantAlreadyExistsException(string message) : Exception(message)
{
    public string? ParticipantId { get; set; } 
}
