using Twilio.Types;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

public class TwilioService
{
	private readonly string _accountSid;
	private readonly string _authToken;
	private readonly string _messagingServiceSid;

	public TwilioService(IConfiguration configuration)
	{
		var twilioSection = configuration.GetSection("Twilio");
		_accountSid = twilioSection["AccountSid"];
		_authToken = twilioSection["AuthToken"];
		_messagingServiceSid = twilioSection["MessagingServiceSid"];
	}

	public async Task SendOtpAsync(string toPhoneNumber, string messageContent)
	{
		TwilioClient.Init(_accountSid, _authToken);

		var message = await MessageResource.CreateAsync(
			to: new PhoneNumber(toPhoneNumber),
			body: messageContent,
			messagingServiceSid: _messagingServiceSid  // dùng Messaging Service
													   // KHÔNG dùng from: new PhoneNumber(...)
		);
	}
}
