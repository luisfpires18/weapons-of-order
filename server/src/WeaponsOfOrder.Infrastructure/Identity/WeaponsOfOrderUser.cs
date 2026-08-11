using Microsoft.AspNetCore.Identity;

namespace WeaponsOfOrder.Infrastructure.Identity;

/// <summary>
/// A Weapons of Order account. Its <see cref="IdentityUser{TKey}.Id"/> is the stable
/// internal User ID that every future player-owned record points at.
/// </summary>
/// <remarks>
/// Deliberately carries no gameplay or player-profile fields. Those belong to the tasks
/// that own those systems; adding them here merely because Identity now exists would make
/// the account table the place everything accumulates.
/// <para>
/// External sign-in methods (Steam, later) attach to this account through Identity's
/// user-login table rather than becoming the account identity, so the internal Id never
/// depends on an external provider.
/// </para>
/// </remarks>
public sealed class WeaponsOfOrderUser : IdentityUser<Guid>
{
    public WeaponsOfOrderUser()
    {
        // Version 7 GUIDs are time-ordered, so inserts append to the end of the primary
        // key index instead of scattering across it the way random v4 values do.
        Id = Guid.CreateVersion7();
    }
}
