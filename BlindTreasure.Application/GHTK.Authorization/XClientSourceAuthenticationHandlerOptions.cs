using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace BlindTreasure.Application.GHTK.Authorization;

public class XClientSourceAuthenticationHandlerOptions : AuthenticationSchemeOptions
{
    // This class can be extended with properties specific to the XClientSourceAuthenticationHandler
    // For example, you might want to add properties for client ID, secret, or other configuration options.
    // Currently, it is empty, but you can add properties as needed in the future.

    public string IssuerSigningKey { get; set; } = string.Empty;

    public Func<string, SecurityToken, ClaimsPrincipal, Task<bool>> ClientValidator { get; set; } =
        (clientSource, token, principal) => Task.FromResult(false);
}