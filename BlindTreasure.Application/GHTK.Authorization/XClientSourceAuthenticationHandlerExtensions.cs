﻿using Microsoft.AspNetCore.Authentication;

namespace BlindTreasure.Application.GHTK.Authorization;

public static class XClientSourceAuthenticationHandlerExtensions
{
    public static AuthenticationBuilder AddXClientSource(this AuthenticationBuilder builder,
        Action<XClientSourceAuthenticationHandlerOptions> configureOptions)
    {
        return builder
            .AddScheme<XClientSourceAuthenticationHandlerOptions, XClientSourceAuthenticationHandler>("X-Client-Source",
                configureOptions);
    }
}