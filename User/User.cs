using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Actors.Client;
using User.Interfaces;
using System.Runtime.Serialization;

namespace User
{
    /// <remarks>
    /// This class represents an actor.
    /// Every ActorID maps to an instance of this class.
    /// The StatePersistence attribute determines persistence and replication of actor state:
    ///  - Persisted: State is written to disk and replicated.
    ///  - Volatile: State is kept in memory only and replicated.
    ///  - None: State is kept in memory only and not replicated.
    /// </remarks>
    [StatePersistence(StatePersistence.Persisted)]
    internal class User : Actor, IUser
    {
        /// <summary>
        /// Initializes a new instance of User
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        public User(ActorService actorService, ActorId actorId) 
            : base(actorService, actorId)
        {
        }

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected override Task OnActivateAsync()
        {
            ActorEventSource.Current.ActorMessage(this, "Actor activated.");
            return StateManager.TryAddStateAsync("count", 0);
        }

        // Create user data as actor state
        public async Task<bool> CreateUserAsync(string firstName, string lastName, string password)
        {
            var existingUser = await StateManager.TryGetStateAsync<UserInfo>("userInfo");
            if (existingUser.HasValue)
            {
                return false;  // User already exists
            }

            var user = new UserInfo { FirstName = firstName, LastName = lastName, Password = password };
            await StateManager.AddStateAsync("userInfo", user);
            return true;
        }

        // Login method with token management
        public async Task<string?> LoginUserAsync(string password)
        {
            var user = await StateManager.TryGetStateAsync<UserInfo>("userInfo");
            if (!user.HasValue || user.Value.Password != password)
            {
                return null;  // Invalid password or user does not exist
            }

            var existingToken = await StateManager.TryGetStateAsync<UserToken>("userToken");
            if (existingToken.HasValue && existingToken.Value.Expiration > DateTimeOffset.UtcNow)
            {
                // Extend token expiration
                var userTokenExtended = new UserToken
                {
                    Token = existingToken.Value.Token,
                    Expiration = DateTimeOffset.UtcNow.Add(tokenDuration)
                };
                await StateManager.SetStateAsync("userToken", userTokenExtended);
                return existingToken.Value.Token;
            }

            // Generate new token
            var token = Guid.NewGuid().ToString();
            var newUserToken = new UserToken
            {
                Token = token,
                Expiration = DateTimeOffset.UtcNow.Add(tokenDuration)
            };
            await StateManager.SetStateAsync("userToken", newUserToken);
            return token;
        }

        // Logout method, removing token
        public async Task LogoutUserAsync()
        {
            await StateManager.RemoveStateAsync("userToken");
        }

        // Check if user is authenticated and update token expiration
        public async Task<bool> IsUserAuthenticatedAsync(string token)
        {
            var existingToken = await StateManager.TryGetStateAsync<UserToken>("userToken");
            if (!existingToken.HasValue || existingToken.Value.Token != token)
            {
                return false;  // Token invalid
            }

            if (existingToken.Value.Expiration < DateTimeOffset.UtcNow)
            {
                await StateManager.RemoveStateAsync("userToken");  // Token expired
                return false;
            }

            // Extend token expiration
            var userTokenExtended = new UserToken
            {
                Token = token,
                Expiration = DateTimeOffset.UtcNow.Add(tokenDuration)
            };
            await StateManager.SetStateAsync("userToken", userTokenExtended);
            return true;
        }

        // Pixel placement time management
        public async Task SetPixelPlacementTimeAsync()
        {
            await StateManager.SetStateAsync("lastPlacementTime", DateTimeOffset.UtcNow);
        }

        public async Task<int> GetRemainingCooldownTimeAsync()
        {
            if (Id.ToString() == "admin")
            {
                return 0;  // Admin user can place pixels without cooldown
            }
            var lastPlacementTime = await StateManager.TryGetStateAsync<DateTimeOffset>("lastPlacementTime");
            if (lastPlacementTime.HasValue)
            {
                TimeSpan remainingCooldown = PixelCooldown - (DateTimeOffset.UtcNow - lastPlacementTime.Value);
                return remainingCooldown > TimeSpan.FromSeconds(1) // Tollerate 1 second difference
                    ? (int)Math.Ceiling(remainingCooldown.TotalSeconds)
                    : 0;  // No cooldown remaining
            }

            return 0;  // No placement history, user can place a pixel
        }

        public async Task<(string?, string?)> GetUserInfo()
        {
            var user = await StateManager.TryGetStateAsync<UserInfo>("userInfo");
            if (!user.HasValue)
            {
                return (null, null);
            }
            return (user.Value.FirstName, user.Value.LastName);
        }

        private static readonly TimeSpan tokenDuration = TimeSpan.FromMinutes(5);  // Token duration
        private static readonly TimeSpan PixelCooldown = TimeSpan.FromSeconds(5);  // Cooldown time
    }

    // User information and token storage classes
    [DataContract]
    public class UserInfo
    {
        [DataMember]
        public required string FirstName { get; set; }
        [DataMember]
        public required string LastName { get; set; }
        [DataMember]
        public required string Password { get; set; } // should be hashed in the future
    }

    [DataContract]
    public class UserToken
    {
        [DataMember]
        public required string Token { get; set; }

        [DataMember]
        public required DateTimeOffset Expiration { get; set; }
    }
}
