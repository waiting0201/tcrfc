namespace Tcrfc.Api.Common;

/// <summary>
/// 錯誤代碼（<c>code</c>）對應的英文使用者訊息，供統一錯誤結構的 <c>messageEn</c> 使用（行動 App 規劃書 §9.5「雙語訊息」）。
/// 規則：與繁中 <c>detail</c> 同義、不含內部實作細節；繁中訊息裡的動態值（件數、訂單編號、商品名稱）在英文版不重複，
/// 改用不含數字的句子——要顯示數字時用戶端依 <c>messageZh</c>／<c>detail</c> 或自己的字串表處理。
/// 新增 <c>code</c> 時要同步補在這裡（<c>ApiErrorMessagesTests</c> 掃描原始碼中所有 <c>ICodedApiException</c> 代碼，缺漏會失敗）；
/// 沒登記的代碼退回該 HTTP 狀態的通用英文（<see cref="GenericEnglish"/>）。
/// 🔵 <c>shared/scripts/gen-error-codes.mjs</c> 會解析本檔，把每個代碼的英文訊息寫進 <c>shared/error-codes.json</c>，格式請勿改動：
/// 每筆一行 <c>["code"] = "English message",</c>。
/// </summary>
public static class ApiErrorMessages
{
    /// <summary>屬於「永久狀態、重試無益」的 503 代碼（外部服務或憑證尚未設定）。這些 <c>retryable</c> 為 false。</summary>
    public static readonly IReadOnlySet<string> NonRetryableServerCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "not_configured", "collecting_subject_missing", "geocoder_not_configured",
        "internal_activation_disabled", "line_not_configured", "payment_not_configured",
    };

    public static string English(string code, int status)
        => Catalog.TryGetValue(code, out var message) ? message : GenericEnglish(status);

    public static string GenericEnglish(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "The request is invalid. Please check your input and try again.",
        StatusCodes.Status401Unauthorized => "Please sign in first.",
        StatusCodes.Status403Forbidden => "You do not have permission to do this.",
        StatusCodes.Status404NotFound => "The requested item could not be found.",
        StatusCodes.Status405MethodNotAllowed => "This request method is not allowed here.",
        StatusCodes.Status409Conflict => "This action conflicts with the current state. Please refresh and try again.",
        StatusCodes.Status413PayloadTooLarge => "The upload is too large. Please reduce its size and try again.",
        StatusCodes.Status415UnsupportedMediaType => "This content format is not supported.",
        StatusCodes.Status423Locked => "The account is temporarily locked. Please try again later.",
        StatusCodes.Status429TooManyRequests => "Too many requests. Please try again later.",
        StatusCodes.Status503ServiceUnavailable => "The service is temporarily unavailable. Please try again later.",
        >= 500 => "Something went wrong on our side. Please try again later.",
        _ => "The request could not be processed.",
    };

    private static readonly Dictionary<string, string> Catalog = new(StringComparer.Ordinal)
    {
        ["account_locked"] = "Too many failed sign-in attempts. The account is temporarily locked. Please try again later or reset your password.",
        ["account_suspended"] = "This account has been suspended. Please contact support.",
        ["activation_failed"] = "The membership could not be activated. Please contact support.",
        ["activation_in_progress"] = "This order is being activated. Please refresh in a moment.",
        ["already_fan_club"] = "You are already a fan club member this season.",
        ["already_registered"] = "You have already registered for this event.",
        ["card_revoked"] = "This membership card has been deactivated, so its QR code cannot be regenerated.",
        ["cart_empty"] = "Your cart could not be found. Please add items to the cart first.",
        ["cart_full"] = "Your cart is full. Please remove an item before adding another.",
        ["cart_item_not_found"] = "This item is not in your cart.",
        ["collecting_subject_missing"] = "The shop is not open for checkout yet.",
        ["confirmation_required"] = "Please type DELETE to confirm deleting your account.",
        ["captcha_failed"] = "The human verification did not pass. Please refresh the page and try again.",
        ["conflict"] = "This action conflicts with the current state. Please refresh and try again.",
        ["device_not_registered"] = "This device is not registered. Please reopen the app and try again.",
        ["email_not_verified"] = "Please verify your email first using the link we sent, then sign in.",
        ["email_taken"] = "This email is already registered. Please sign in, or use Forgot password to reset it.",
        ["fan_club_required"] = "This event is for paid fan club members only, and you do not have an active membership.",
        ["forbidden"] = "You do not have permission to do this.",
        ["geocoder_not_configured"] = "Address lookup is not enabled. Please enter the latitude and longitude directly.",
        ["geocoder_unavailable"] = "Address lookup is temporarily unavailable. Please try again later.",
        ["idempotency_key_required"] = "Please send an Idempotency-Key header (8 to 64 letters, digits, or - _ : .).",
        ["idempotency_key_reused"] = "This idempotency key was already used for a different order. Please generate a new one.",
        ["insufficient_stock"] = "There is not enough stock for this item. Please adjust the quantity.",
        ["internal_activation_disabled"] = "The internal activation endpoint is not enabled.",
        ["invalid_address"] = "Please enter a complete delivery address (up to 500 characters).",
        ["birth_on_required"] = "Please enter your date of birth (we need to confirm your age to register).",
        ["guardian_consent_required"] = "Members under 18 need a guardian's consent to register. Please have your guardian complete the consent.",
        ["guardian_name_required"] = "Please enter the guardian's name (up to 64 characters).",
        ["invalid_guardian_relationship"] = "Please choose the guardian's relationship to you (parent or legal guardian).",
        ["invalid_guardian_consent_version"] = "The consent text version is not valid.",
        ["invalid_birth_on"] = "The date of birth is not valid.",
        ["invalid_carrier"] = "The mobile barcode carrier is not valid (a slash followed by 7 characters, for example /ABC+123).",
        ["invalid_credential"] = "The credential is not valid.",
        ["invalid_credentials"] = "The email or password is incorrect.",
        ["invalid_delivery_method"] = "Please choose a delivery method: home delivery, convenience store pickup, or on-site pickup.",
        ["invalid_donation_code"] = "Please choose an organization from the list (the donation code is not valid).",
        ["invalid_email"] = "Please enter a valid email address (the order confirmation is sent there).",
        ["invalid_locale"] = "The language must be zh or en.",
        ["invalid_mode"] = "The mode must be login or bind.",
        ["invalid_name"] = "Please enter a name (up to 64 characters).",
        ["invalid_note"] = "The note can be at most 500 characters.",
        ["invalid_phone"] = "Please enter a valid contact phone number.",
        ["invalid_pickup_store"] = "Please enter the pickup store name or code for convenience store pickup.",
        ["invalid_quantity"] = "The quantity is out of the allowed range.",
        ["invalid_recipient_name"] = "Please enter the recipient name (up to 64 characters).",
        ["invalid_redirect_uri"] = "The callback address is not allowed.",
        ["invalid_tax_id"] = "The tax ID is not valid. Please check that it has 8 digits.",
        ["invoice_required"] = "Please choose how to receive the e-invoice (carrier, tax ID, or donation code).",
        ["item_unavailable"] = "An item is no longer available. Please remove it from your cart before checking out.",
        ["jersey_locked"] = "This jersey has already been processed (shipped or collected) and can no longer be changed. Please contact support.",
        ["jersey_quota_reached"] = "You have registered all the jerseys included in this membership plan.",
        ["line_already_bound"] = "Your account is already linked to LINE.",
        ["line_exchange_failed"] = "The LINE authorization is no longer valid. Please try again.",
        ["line_in_use"] = "This LINE account is already linked to another member account.",
        ["line_not_configured"] = "LINE sign-in is not enabled. Please sign in with your email first.",
        ["line_unreachable"] = "LINE cannot be reached right now. Please try again later.",
        ["login_required"] = "This event is for paid fan club members only. Please sign in first.",
        ["lookup_fields_required"] = "Please enter the order number and the email used for the order.",
        ["not_configured"] = "This service is not enabled yet.",
        ["not_fan_club"] = "Only active fan club memberships can register jerseys.",
        ["not_found"] = "The requested item could not be found.",
        ["open_order_exists"] = "You already have an unfinished order for this plan. Please complete or cancel it first.",
        ["order_expired"] = "This order has expired or was cancelled. Please place a new order.",
        ["order_not_cancellable"] = "This order has been paid or is being shipped. Please contact support for returns or exchanges.",
        ["order_not_found"] = "No matching order was found. Please check the order number and the email used for the order.",
        ["order_not_paid"] = "This order has not been paid yet, so it cannot be activated.",
        ["order_not_payable"] = "This order cannot be paid in its current state.",
        ["order_not_pending"] = "This order has no payment in progress to confirm.",
        ["order_state_changed"] = "Payment completed but the order had just expired. Please contact support; do not pay again.",
        ["password_required"] = "Your account can only sign in with LINE right now. Please set a password before unlinking LINE.",
        ["payment_failed"] = "Payment was not completed. Please try again from the payment page; if you were charged, contact support and do not pay again.",
        ["payment_in_progress"] = "A payment is being created for this order. Please refresh in a moment.",
        ["payment_not_configured"] = "Online payment is not enabled yet. Please try again later or contact support.",
        ["plan_closed"] = "This plan has ended and can no longer be purchased.",
        ["plan_free"] = "This plan does not require payment.",
        ["plan_not_found"] = "This membership plan could not be found.",
        ["quantity_limit"] = "The quantity exceeds the limit for a single item.",
        ["region_not_deliverable"] = "Sorry, we do not deliver to this area. Please choose convenience store pickup or on-site pickup.",
        ["registration_closed"] = "Registration for this event has closed or the event has already started.",
        ["season_not_available"] = "This club has no season open for joining right now. Please try again later.",
        ["session_expired"] = "Your session has expired. Please sign in again.",
        ["state_invalid"] = "The LINE authorization has expired. Please try again.",
        ["ticket_invalid"] = "The LINE sign-up has expired. Please try again.",
        ["token_invalid"] = "The verification link is invalid or has expired. Please request a new verification email.",
        ["transaction_mismatch"] = "The payment transaction does not match.",
        ["unauthenticated"] = "Please sign in first.",
        ["validation_failed"] = "The request is invalid. Please check your input and try again.",
        ["variant_not_found"] = "This item could not be found or is no longer available.",
        ["weak_password"] = "The password is too weak. Please use at least 8 characters with letters and digits, and avoid common passwords.",
        ["schedule_conflict"] = "The new time slot conflicts with existing events. Please confirm and submit again if you still want to reschedule.",
        ["rate_limited"] = "Too many requests. Please try again later.",
        ["method_not_allowed"] = "This request method is not allowed here.",
        ["payload_too_large"] = "The upload is too large. Please reduce its size and try again.",
        ["unsupported_media_type"] = "This content format is not supported.",
        ["service_unavailable"] = "The service is temporarily unavailable. Please try again later.",
        ["server_error"] = "Something went wrong on our side. Please try again later.",
        ["request_failed"] = "The request could not be processed.",
    };
}
