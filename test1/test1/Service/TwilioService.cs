using Twilio;
using Twilio.Rest.Verify.V2.Service;

public class TwilioService
{
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _verifyServiceSid;

    public TwilioService(IConfiguration configuration)
    {
        var twilioSection = configuration.GetSection("Twilio");
        _accountSid = twilioSection["AccountSid"];
        _authToken = twilioSection["AuthToken"];
        _verifyServiceSid = twilioSection["VerifyServiceSid"];
        TwilioClient.Init(_accountSid, _authToken);
    }

    // Gửi OTP (SMS / email / voice). Mặc định: SMS
    public async Task<string> StartVerificationAsync(string toPhoneNumber, string channel = "sms")
    {
        var verification = await VerificationResource.CreateAsync(
            to: toPhoneNumber,
            channel: channel,
            pathServiceSid: _verifyServiceSid
        );
        return verification.Status; // "pending" nếu gửi ok
    }

    // Kiểm tra mã OTP
    public async Task<bool> CheckVerificationAsync(string toPhoneNumber, string code)
    {
        var check = await VerificationCheckResource.CreateAsync(
            to: toPhoneNumber,
            code: code,
            pathServiceSid: _verifyServiceSid
        );
        return string.Equals(check.Status, "approved", StringComparison.OrdinalIgnoreCase);
    }
}
