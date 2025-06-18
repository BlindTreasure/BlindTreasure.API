namespace BlindTreasure.Application.Utils;

public static class ErrorMessages
{
    #region Notificaiton Error Message

    public const string NotificaionNotFound = "Notificaiton not found.";

    #endregion

    #region Seed Data

    public const string Seed_UserHasNotBeenSeeded = "Please seed user accounts first.";

    #endregion

    #region Account Error Message

    public const string AccountNotFound = "User id/email is not found.";
    public const string AccountInvalidRole = "Invalid role.";
    public const string AccountSuspendedOrBan = "Suspended account or banned.";
    public const string AccountLocked = "Account is locked until {0}. Please try again later.";
    public const string AccountWrongPassword = "Wrong password.";
    public const string AccountVerificationCodeExpired = "Verification code expired.";
    public const string AccountInvalidVerificationCode = "Invalid code.";
    public const string AccountRecoveryEmailInUse = "Recovery email already in use.";
    public const string AccountAccesstokenInvalid = "The user has been log out or invalid token.";
    public const string ExpiredRefreshtokenInvalid = "Refresh token has expired.";
    public const string AccountInvalidRefreshToken = "The refresh token is invalid or expired.";

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
    public const string Oauth_InvalidToken = "Google token is invalid.";
    public const string Oauth_PayloadNull = "Google payload is null.";
    public const string Oauth_InvalidCredential = "Invalid credential.";
    public const string Oauth_InvalidOtp = "Invalid Otp code.";

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
    public const string GeneratedSubdomain = "Subdomain is not valid. Please try again.";

    #endregion

    #region Stripe

    public const string StripeTransctionFail_StripeAccountNotFound = "This account have not added Stripe Account yet.";
    public const string StripeSessionNotFound = "Stripe session not found, callback handler throwing exception!";

    #endregion

    #region Caching

    public const string VerifyOtpExistingCoolDown =
        "You are sending OTP too fast. Please try again after a few minutes.";

    public const string CacheUserNotFound = "User data not found in cache.";

    #endregion

    #region BlindBox Error Message

    public const string BlindBoxNotFound = "Blind box not found.";
    public const string BlindBoxDataRequired = "Blind box data is required.";
    public const string BlindBoxNameRequired = "Blind box name is required.";
    public const string BlindBoxPriceInvalid = "Blind box price must be greater than 0.";
    public const string BlindBoxTotalQuantityInvalid = "Blind box total quantity must be greater than 0.";
    public const string BlindBoxBrandRequired = "Blind box brand is required.";
    public const string BlindBoxReleaseDateInvalid = "Blind box release date is invalid.";
    public const string BlindBoxImageRequired = "Blind box image is required.";
    public const string BlindBoxSellerNotVerified = "You must be a verified seller to create a blind box.";
    public const string BlindBoxImageUrlError = "Error retrieving blind box image URL.";
    public const string BlindBoxNoUpdatePermission = "You do not have permission to update this blind box.";
    public const string BlindBoxImageUpdateError = "Error updating blind box image.";
    public const string BlindBoxNoEditPermission = "You do not have permission to edit this blind box.";
    public const string BlindBoxItemCountInvalid = "Blind box must contain exactly 6 or 12 items.";
    public const string BlindBoxAtLeastOneItem = "Blind box must have at least one item.";
    public const string BlindBoxDropRateMustBe100 = "Total drop rate must be 100%.";
    public const string BlindBoxNotFoundOrNotPending = "Blind box not found or not pending approval.";
    public const string BlindBoxNoItems = "Blind box contains no items.";
    public const string BlindBoxRejectReasonRequired = "Reject reason is required.";

    public const string BlindBoxNoDeleteItemPermission =
        "You do not have permission to delete items from this blind box.";

    public const string BlindBoxNoDeletePermission = "You do not have permission to delete this blind box.";
    public const string BlindBoxItemListRequired = "Item list cannot be empty.";
    public const string BlindBoxProductInvalidOrOutOfStock = "One or more products are invalid or out of stock.";
    public const string BlindBoxProductStockExceeded = "Product '{0}' exceeds available stock.";
    public const string BlindBoxNoSecretSupport = "Blind box does not support secret items.";
    public const string BlindBoxSecretItemRequired = "Blind box must have at least one secret item.";
    public const string BlindBoxDropRateExceeded = "Total drop rate (excluding secret) must be less than 100%.";

    #endregion


    #region Cart Error Message

    public const string CartItemQuantityMustBeGreaterThanZero = "Quantity must be greater than 0.";
    public const string CartItemProductOrBlindBoxRequired = "You must select a product or a blind box.";
    public const string CartItemProductNotFound = "Product not found.";
    public const string CartItemProductOutOfStock = "Product is out of stock.";
    public const string CartItemBlindBoxNotFoundOrRejected = "Blind box not found or has been rejected.";
    public const string CartItemNotFound = "Cart item not found.";
    public const string CartItemBlindBoxNotFound = "Blind box not found.";

    #endregion


    #region Category Error Message

    public const string CategoryNotFound = "Category not found.";
    public const string CategoryNameRequired = "Category name is required.";
    public const string CategoryNameAlreadyExists = "Category name already exists.";
    public const string CategoryParentIdInvalid = "ParentId is invalid.";
    public const string CategoryImageOnlyForRoot = "Only root categories (no ParentId) can have an image.";
    public const string CategoryImageUploadError = "Error uploading category image.";
    public const string CategoryNoUpdatePermission = "You do not have permission to update categories.";
    public const string CategoryParentIdSelf = "ParentId cannot be the same as the category itself.";
    public const string CategoryHierarchyLoop = "Cannot create a category hierarchy loop.";
    public const string CategoryNoDeletePermission = "You do not have permission to delete categories.";

    public const string CategoryDeleteHasChildrenOrProducts =
        "Cannot delete category with related products or child categories.";

    #endregion


    #region Order Error Message

    public const string OrderCartEmpty = "Cart is empty.";
    public const string OrderCartEmptyLog = "[CheckoutAsync] Cart is empty.";
    public const string OrderCheckoutStartLog = "[CheckoutAsync] Start processing checkout from system cart.";
    public const string OrderClientCartInvalid = "Client cart is invalid or empty.";
    public const string OrderClientCartInvalidLog = "[CheckoutFromClientCartAsync] Client cart is invalid or empty.";

    public const string OrderCheckoutFromClientStartLog =
        "[CheckoutFromClientCartAsync] Start processing checkout from client cart.";

    public const string OrderCartEmptyOrInvalid = "Cart is empty or invalid.";
    public const string OrderCartEmptyOrInvalidLog = "[CheckoutCore] Cart is empty or invalid.";
    public const string OrderShippingAddressInvalid = "Shipping address is invalid or does not belong to user.";

    public const string OrderShippingAddressInvalidLog =
        "[CheckoutCore] Shipping address is invalid or does not belong to user.";

    public const string OrderProductNotFound = "Product {0} not found.";
    public const string OrderProductOutOfStock = "Product {0} is out of stock.";
    public const string OrderProductNotForSale = "Product {0} is not for sale.";
    public const string OrderBlindBoxNotFound = "Blind box {0} not found.";
    public const string OrderBlindBoxNotApproved = "Blind box {0} is not approved.";
    public const string OrderBlindBoxOutOfStock = "Blind box {0} is out of stock.";
    public const string OrderCartClearedAfterCheckoutLog = "[CheckoutCore] System cart cleared after checkout.";

    public const string OrderCacheClearedAfterCheckoutLog =
        "[CheckoutCore] Order cache cleared for user {0} after checkout.";

    public const string OrderCheckoutSuccessLog = "[CheckoutCore] Checkout successful for user {0}.";
    public const string OrderCacheHitLog = "[GetOrderByIdAsync] Cache hit for order {0}";
    public const string OrderNotFoundLog = "[GetOrderByIdAsync] Order {0} not found.";
    public const string OrderNotFound = "Order not found.";
    public const string OrderLoadedAndCachedLog = "[GetOrderByIdAsync] Order {0} loaded from DB and cached.";
    public const string OrderListLoadedLog = "[GetMyOrdersAsync] User orders loaded from DB.";

    public const string OrderNotFoundOrNotBelongToUserLog =
        "[CancelOrderAsync] Order {0} not found or does not belong to user.";

    public const string OrderNotPendingLog = "[CancelOrderAsync] Order {0} is not in PENDING status.";
    public const string OrderCancelOnlyPending = "Only orders in PENDING status can be cancelled.";

    public const string OrderCacheClearedAfterCancelLog =
        "[CancelOrderAsync] Order cache cleared for user {0} after cancellation.";

    public const string OrderCancelSuccessLog = "[CancelOrderAsync] Order {0} cancelled successfully.";

    public const string OrderCacheClearedAfterDeleteLog =
        "[DeleteOrderAsync] Order cache cleared for user {0} after deletion.";

    public const string OrderDeleteSuccessLog = "[DeleteOrderAsync] Order {0} deleted successfully.";

    #endregion

    #region Product Error Message

    public const string ProductNotFound = "Product not found.";
    public const string ProductNotFoundOrDeleted = "Product does not exist or has been deleted.";
    public const string ProductSellerNotFound = "Seller does not exist.";
    public const string ProductSellerNotVerified = "Seller is not verified.";
    public const string ProductCreatedLog = "[CreateAsync] Product {0} created with {1} images.";
    public const string ProductUpdateNotFoundLog = "[UpdateAsync] Product {0} not found or deleted.";
    public const string ProductUpdateLog = "[UpdateAsync] User {0} updates product {1}";
    public const string ProductUpdateSuccessLog = "[UpdateAsync] Product {0} updated by user {1}";
    public const string ProductDeleteNotFoundLog = "[DeleteAsync] Product {0} not found or deleted.";
    public const string ProductDeleteSuccessLog = "[DeleteAsync] Product {0} soft deleted by user {1}";
    public const string ProductImageFileInvalidLog = "[UploadProductImageAsync] Image file is invalid or empty.";
    public const string ProductImageFileInvalid = "Image file is invalid or empty.";
    public const string ProductImageNotFoundLog = "[UploadProductImageAsync] Product {0} not found or deleted.";
    public const string ProductImageUrlErrorLog = "[UploadProductImageAsync] Cannot get URL for file {0}";
    public const string ProductImageUrlError = "Cannot create image URL.";
    public const string ProductImageUploadingLog = "[UploadProductImageAsync] Uploading file: {0}";
    public const string ProductImageUpdateSuccessLog = "[UploadProductImageAsync] Updated image for product {0}: {1}";

    #endregion
}