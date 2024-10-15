using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting;

[assembly: FabricTransportActorRemotingProvider(RemotingListenerVersion = RemotingListenerVersion.V2_1, RemotingClientVersion = RemotingClientVersion.V2_1)]
namespace User.Interfaces
{
    /// <summary>
    /// This interface defines the methods exposed by an actor.
    /// Clients use this interface to interact with the actor that implements it.
    /// </summary>
    public interface IUser : IActor
    {
        Task<bool> CreateUserAsync(string firstName, string lastName, string password); // Can add more user data here
        Task<string?> LoginUserAsync(string password);
        Task LogoutUserAsync();
        Task<bool> IsUserAuthenticatedAsync(string token);
        Task SetPixelPlacementTimeAsync();
        Task<int> GetRemainingCooldownTimeAsync();
        Task<(string?, string?)> GetUserInfo();
    }
}
