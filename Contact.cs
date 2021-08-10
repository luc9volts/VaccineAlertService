using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using System.Linq;

namespace VaccineAlertService
{
    public class Contact
    {
        private readonly ContactSettings _contactSettings;

        public Contact(ContactSettings contactSettings)
        {
            _contactSettings = contactSettings;
            TwilioClient.Init(_contactSettings.AccountSid, _contactSettings.AuthToken);
        }

        public string MakeCall(string[] destinationPhones, string message)
        {
            var xmlToSay = new Twiml($@"<?xml version='1.0' encoding='UTF-8'?>
                                        <Response>
                                        <Say voice='alice' language='pt-BR'>{message}</Say>
                                        </Response>");

            return destinationPhones
                            .Select(phone => CallResource.Create(twiml: xmlToSay,
                                                            from: new PhoneNumber(_contactSettings.OriginPhone),
                                                            to: new PhoneNumber(phone)))
                            .Select(call => call.Sid)
                            .Aggregate((sid1, sid2) => $"{sid1},{sid2}");
        }

        public string SendSMS(string text, string[] destinationPhones)
        {
            return destinationPhones
                            .Select(phone => MessageResource.Create(
                                                            body: text,
                                                            from: new PhoneNumber(_contactSettings.OriginPhone),
                                                            to: new PhoneNumber(phone)))
                            .Select(message => message.Sid)
                            .Aggregate((sid1, sid2) => $"{sid1},{sid2}");
        }
    }
}