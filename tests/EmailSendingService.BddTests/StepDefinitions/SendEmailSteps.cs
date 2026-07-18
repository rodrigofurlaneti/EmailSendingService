using EmailSendingService.Application.Emails.Dtos;
using EmailSendingService.Application.Emails.SendEmail;
using EmailSendingService.Application.Common;
using EmailSendingService.BddTests.Support;
using FluentAssertions;
using Reqnroll;

namespace EmailSendingService.BddTests.StepDefinitions;

[Binding]
public sealed class SendEmailSteps
{
    private readonly InMemoryEmailSender _sender = new();
    private readonly SendEmailRequest _request = new();
    private Result<SendEmailResponse>? _result;

    [Given(@"a sender ""(.*)""")]
    public void GivenASender(string address)
        => _request.From = new RecipientDto { Address = address };

    [Given(@"a recipient ""(.*)""")]
    public void GivenARecipient(string address)
        => _request.To.Add(new RecipientDto { Address = address });

    [Given(@"the subject ""(.*)""")]
    public void GivenTheSubject(string subject) => _request.Subject = subject;

    [Given(@"the body ""(.*)""")]
    public void GivenTheBody(string body) => _request.Body = body;

    [When(@"the e-mail is submitted")]
    public async Task WhenTheEmailIsSubmitted()
    {
        var handler = new SendEmailHandler(_sender, new EmailDefaults());
        _result = await handler.HandleAsync(new SendEmailCommand(_request));
    }

    [Then(@"the e-mail is delivered")]
    public void ThenTheEmailIsDelivered()
    {
        _result!.IsSuccess.Should().BeTrue();
        _result.Value!.Delivered.Should().BeTrue();
        _sender.Sent.Should().ContainSingle();
    }

    [Then(@"the delivery has a provider message id")]
    public void ThenTheDeliveryHasAProviderMessageId()
        => _result!.Value!.ProviderMessageId.Should().NotBeNullOrWhiteSpace();

    [Then(@"the submission is rejected")]
    public void ThenTheSubmissionIsRejected()
    {
        _result!.IsSuccess.Should().BeFalse();
        _result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Then(@"no e-mail is dispatched")]
    public void ThenNoEmailIsDispatched()
        => _sender.Sent.Should().BeEmpty();
}
