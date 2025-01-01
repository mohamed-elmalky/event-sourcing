using MediatR;
using Persistence;

namespace PersonEvents;

public class ParticipantUniqueByNameAndEmailBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : AddParticipantCommand
{
    private readonly IUniquenessDataStore _uniquenessDataStore;

    public ParticipantUniqueByNameAndEmailBehavior(IUniquenessDataStore uniquenessDataStore) => _uniquenessDataStore = uniquenessDataStore;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request.Participant is null || string.IsNullOrEmpty(request.Participant.Name) || string.IsNullOrEmpty(request.Participant.Email))
        {
            Console.WriteLine("\u2705 Name and email");
            return await next();
        }

        var participantId = await _uniquenessDataStore.ByNameAndEmail($"{request.Participant.Name}:{request.Participant.Email}");
        if (participantId is null)
            Console.WriteLine("\u2705 Name and email");
        else
        {
            Console.WriteLine("\u274C Exists with this name and email");
            throw new ParticipantAlreadyExistsException("Participant already exists with this name and email") { ParticipantId = participantId };
        }

        return await next();
    }
}

/// <summary>
/// Ensures that a participant is unique by name and phone number. This is the second highest priority uniqueness constraint.
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public class ParticipantUniqeByNameAndHomePhoneNumberBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : AddParticipantCommand
{
    private readonly IUniquenessDataStore _uniquenessDataStore;

    public ParticipantUniqeByNameAndHomePhoneNumberBehavior(IUniquenessDataStore uniquenessDataStore) => _uniquenessDataStore = uniquenessDataStore;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request.Participant is null || string.IsNullOrEmpty(request.Participant.Name) || string.IsNullOrEmpty(request.Participant.HomePhone))
        {
            Console.WriteLine("\u2705 Name and home phone number");
            return await next();
        }

        var participantId = await _uniquenessDataStore.ByNameAndHomePhoneNumber($"{request.Participant.Name}:{request.Participant.HomePhone}");
        if (participantId is null)
            Console.WriteLine("\u2705 Name and home phone number");
        else
        {
            Console.WriteLine("\u274C Exists with this name and home phone number");
            throw new ParticipantAlreadyExistsException("Participant already exists with this name and number") { ParticipantId = participantId };
        }

        return await next();
    }
}

public class ParticipantUniqueByNameAndMobileNumberBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : AddParticipantCommand
{
    private readonly IUniquenessDataStore _uniquenessDataStore;

    public ParticipantUniqueByNameAndMobileNumberBehavior(IUniquenessDataStore uniquenessDataStore) => _uniquenessDataStore = uniquenessDataStore;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request.Participant is null || string.IsNullOrEmpty(request.Participant.Name) || string.IsNullOrEmpty(request.Participant.MobilePhone))
        {
            Console.WriteLine("\u2705 Name and mobile phone number");
            return await next();
        }

        var participantId = await _uniquenessDataStore.ByNameAndMobilePhoneNumber($"{request.Participant.Name}:{request.Participant.MobilePhone}");
        if (participantId is null)
            Console.WriteLine("\u2705 Name and mobile phone number");
        else
        {
            Console.WriteLine("\u274C Exists with this name and mobile phone number");
            throw new ParticipantAlreadyExistsException("Participant already exists with this name and number") { ParticipantId = participantId };
        }

        return await next();
    }
}

/// <summary>
/// Ensures that a participant is unique by SSN. This is the highest priority uniqueness constraint. It short-circuits the pipeline if the participant is unique.
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public class ParticipantUniqueBySSNBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : AddParticipantCommand where TResponse : ParticipantAcquired
{
    private readonly IUniquenessDataStore _uniquenessDataStore;

    public ParticipantUniqueBySSNBehavior(IUniquenessDataStore uniquenessDataStore) => _uniquenessDataStore = uniquenessDataStore;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request.Participant is null || string.IsNullOrEmpty(request.Participant.SSN))
        {
            Console.WriteLine("\u2705 SSN");
            return await next();
        }

        var participantId = await _uniquenessDataStore.BySSN(request.Participant.SSN);
        if (participantId is null)
        {
            Console.WriteLine("\u2705 SSN");
            return (TResponse)request.ToEvent(); // Not really happy with creating the event here, but it's the only way I can short-circuit the pipeline for this specific behavior.
        }
        else
        {
            Console.WriteLine("\u274C Exists with this SSN");
            throw new ParticipantAlreadyExistsException("Participant already exists with this SSN") { ParticipantId = participantId };
        }

    }
}

public class ParticipantUniqueByNameAndAddressBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : AddParticipantCommand
{
    private readonly IUniquenessDataStore _uniquenessDataStore;

    public ParticipantUniqueByNameAndAddressBehavior(IUniquenessDataStore uniquenessDataStore) => _uniquenessDataStore = uniquenessDataStore;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request.Participant is null || string.IsNullOrEmpty(request.Participant.Name) || request.Participant.Address is null)
        {
            Console.WriteLine("\u2705 Name and address");
            return await next();
        }

        var participantId = await _uniquenessDataStore.ByNameAndAddress($"{request.Participant.Name}:{request.Participant.Address}");
        if (participantId is null)
            Console.WriteLine("\u2705 Name and address");
        else
        {
            Console.WriteLine("\u274C Exists with this name and address");
            throw new ParticipantAlreadyExistsException("Participant already exists with this name and address") { ParticipantId = participantId };
        }

        return await next();
    }
}
