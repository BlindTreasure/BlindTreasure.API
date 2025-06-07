namespace BlindTreasure.Application.Utils;

public static class ErrorMessages
{
    #region Category Error Message

    public const string CategoryNotFound = "Category not found.";

    #endregion

    #region Notificaiton Error Message

    public const string NotificaionNotFound = "Notificaiton not found.";

    #endregion

    #region Seed Data

    public const string Seed_UserHasNotBeenSeeded = "Please seed user accounts first.";

    #endregion

    #region Account Error Message

    public const string AccountNotFound = "User not found.";
    public const string AccountInvalidRole = "Invalid role.";
    public const string AccountSuspended = "Suspended account.";
    public const string AccountLocked = "Account is locked until {0}. Please try again later.";
    public const string AccountWrongPassword = "Wrong password.";
    public const string AccountVerificationCodeExpired = "Verification code expired.";
    public const string AccountInvalidVerificationCode = "Invalid code.";
    public const string AccountRecoveryEmailInUse = "Recovery email already in use.";

    public const string AccountTooManyRecoveryAttempts =
        "Too many failed recovery attempts. Please try again after {0}.";

    public const string AccountInvalidRecoveryCode = "Invalid or expired recovery code.";
    public const string AccountNotVerified = "Email is not verified.";
    public const string AccountEmailAlreadyRegistered = "This email is already registered.";

    public const string AccountEmailAlreadyRegistered_NoneVerified =
        "Email is already registered. Unverified accounts expire after 24 hours.";

    public const string Account_CityNotSupported =
        "City is not supported. Currently APIHub just support Ho Chi Minh, Binh Duong and Ha Noi";

    public const string AccountProfileIncomplete =
        "Profile is incomplete and you cannot submit api. Please complete your profile first.";

    public const string AccountInvalidEmailFormat = "Invalid email format. Please check and try again.";
    public const string AccountAlreadyVerified = "Email is already verified.";
    public const string AccountGitHubAccessTokenNotFound = "GitHub access token not found. Please login to GithubFirst";

    #endregion

    #region Oauth Error Message

    public const string Oauth_ClientIdMissing = "ClientID is missing.";
    public const string Oauth_PayloadNull = "Google payload is null.";
    public const string Oauth_InvalidToken = "Google token is invalid.";
    public const string Oauth_InvalidCredential = "Invalid credential.";

    #endregion

    #region Transaction Error Message

    public const string TransactionUpgradeCostNegative = "This node upgrades do not need additional fee.";
    public const string TransactionInvalidRefund = "Transaction state is not complete or cannot be found.";
    public const string Currency_APIFailed = "Currency API failed. Please try again later. Error: {0}";
    public const string Currency_RateNull = "Currency Rate is null, conversion fail. Please try again later.";
    public const string TransactionPermissionDenied = "You do not have permission to access this transaction.";
    public const string TransactionRefundOverdue = "You can not refund a transaction after 24 hours.";

    public const string Transacion_RevewActiveNodeDenied =
        "You can not renew an active node. APIHub are not supporting this feature for now!";

    #endregion

    #region Other Error Message

    public const string Jwt_InvalidToken = "Invalid or expired JWT Token.";
    public const string Jwt_RefreshTokenExpired = "Refresh token expired. Please re-login.";
    public const string AuthenticationFailed = "Authentication failed. Please try again.";
    public const string GeneratedSubdomainTroll = "Subdomain is not valid. Please try again.";

    #endregion

    #region Stripe

    public const string StripeTransctionFail_StripeAccountNotFound = "This account have not added Stripe Account yet.";
    public const string StripeSessionNotFound = "Stripe session not found, callback handler throwing exception!";

    #endregion
}