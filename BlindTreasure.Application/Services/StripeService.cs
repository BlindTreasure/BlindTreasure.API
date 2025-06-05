using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Infrastructure.Interfaces;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Services
{
    public class StripeService : IStripeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly string redirectUrl = "";
        private readonly IStripeClient _stripeClient;
        private readonly IClaimsService _claimsService;

        public StripeService(IUnitOfWork unitOfWork, string redirectUrl, IStripeClient stripeClient, IClaimsService claimsService)
        {
            _unitOfWork = unitOfWork;
            this.redirectUrl = redirectUrl;
            _stripeClient = stripeClient;
            _claimsService = claimsService;
        }

        public async Task<string> GenerateExpressLoginLink()
        {

            var userId = _claimsService.CurrentUserId; // chỗ này là lấy user id của seller là người đang login
            var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(user => user.Id == userId) ?? throw ErrorHelper.Forbidden("Seller is not existing");
            // Create an instance of the LoginLinkService
            var loginLinkService = new Stripe.AccountLoginLinkService();

            // Create the login link for the connected account
            // Optionally, you can provide additional options (like redirect URL) if needed.
            var loginLink = await loginLinkService.CreateAsync(seller.StripeAccountId);
            return loginLink.Url;
        }


    }
}
