using System.Collections.Concurrent;
using PersonEvents;

namespace Persistence;

public interface IUniquenessDataStore
{
    public Task Add(Person participant);
    public Task<Person?> ById(string key);
    public Task<string?> BySSN(string key);
    public Task<string?> ByNameAndHomePhoneNumber(string key);
    public Task<string?> ByNameAndMobilePhoneNumber(string key);
    public Task<string?> ByNameAndAddress(string key);
    public Task<string?> ByNameAndEmail(string key);

}
public class UniquenessMemoryDataStore : IUniquenessDataStore
{
    private readonly ConcurrentDictionary<string, Person> ById = new();
    private readonly ConcurrentDictionary<string, string> BySSN = new();
    private readonly ConcurrentDictionary<string, string> ByNameAndHomePhoneNumber = new();
    private readonly ConcurrentDictionary<string, string> ByNameAndMobilePhoneNumber = new();
    private readonly ConcurrentDictionary<string, string> ByNameAndAddress = new();
    private readonly ConcurrentDictionary<string, string> ByNameAndEmail = new();

    public Task Add(Person participant)
    {
        if (!string.IsNullOrEmpty(participant.SSN))
            BySSN.TryAdd(participant.SSN, participant.Id);

        if (participant.Name is not null && participant.HomePhone is not null)
            ByNameAndHomePhoneNumber.TryAdd($"{participant.Name}:{participant.HomePhone}", participant.Id);

        if (participant.Name is not null && participant.MobilePhone is not null)
            ByNameAndMobilePhoneNumber.TryAdd($"{participant.Name}:{participant.MobilePhone}", participant.Id);

        if (participant.Name is not null && participant.Email is not null)
            ByNameAndEmail.TryAdd($"{participant.Name}:{participant.Email}", participant.Id);

        if (participant.Name is not null && participant.Address is not null)
            ByNameAndAddress.TryAdd($"{participant.Name}:{participant.Address}", participant.Id);
        
        ById.TryAdd(participant.Id, participant);

        return Task.CompletedTask;
    }

    Task<Person?> IUniquenessDataStore.ById(string key)
    {
        return Task.FromResult(ById.TryGetValue(key, out var participant) ? participant : null);
    }

    Task<string?> IUniquenessDataStore.ByNameAndAddress(string key)
    {
        return Task.FromResult(ByNameAndAddress.TryGetValue(key, out var participantId) ? participantId : null);
    }

    Task<string?> IUniquenessDataStore.ByNameAndEmail(string key)
    {
        return Task.FromResult(ByNameAndEmail.TryGetValue(key, out var participantId) ? participantId : null);
    }

    Task<string?> IUniquenessDataStore.ByNameAndHomePhoneNumber(string key)
    {
        return Task.FromResult(ByNameAndHomePhoneNumber.TryGetValue(key, out var participantId) ? participantId : null);
    }

    Task<string?> IUniquenessDataStore.ByNameAndMobilePhoneNumber(string key)
    {
        return Task.FromResult(ByNameAndMobilePhoneNumber.TryGetValue(key, out var participantId) ? participantId : null);
    }

    Task<string?> IUniquenessDataStore.BySSN(string key)
    {
        return Task.FromResult(BySSN.TryGetValue(key, out var participantId) ? participantId : null);
    }
}
