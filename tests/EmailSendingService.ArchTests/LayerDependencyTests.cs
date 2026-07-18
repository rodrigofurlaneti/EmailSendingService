using EmailSendingService.Domain.ValueObjects;
using EmailSendingService.Application.Abstractions;
using EmailSendingService.Infrastructure.Smtp;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace EmailSendingService.ArchTests;

/// <summary>
/// Enforces the Clean Architecture dependency rule: dependencies point inward.
/// Domain &lt;- Application &lt;- Infrastructure/Api. Inner layers never reference outer ones.
/// </summary>
public class LayerDependencyTests
{
    private const string Domain = "EmailSendingService.Domain";
    private const string Application = "EmailSendingService.Application";
    private const string Infrastructure = "EmailSendingService.Infrastructure";
    private const string Api = "EmailSendingService.Api";

    private static System.Reflection.Assembly DomainAsm => typeof(EmailAddress).Assembly;
    private static System.Reflection.Assembly ApplicationAsm => typeof(IEmailSender).Assembly;
    private static System.Reflection.Assembly InfrastructureAsm => typeof(SmtpEmailSender).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOn_AnyOuterLayer()
    {
        var result = Types.InAssembly(DomainAsm)
            .ShouldNot()
            .HaveDependencyOnAny(Application, Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildMessage(result));
    }

    [Fact]
    public void Application_ShouldNotDependOn_InfrastructureOrApi()
    {
        var result = Types.InAssembly(ApplicationAsm)
            .ShouldNot()
            .HaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildMessage(result));
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(InfrastructureAsm)
            .ShouldNot()
            .HaveDependencyOn(Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildMessage(result));
    }

    [Fact]
    public void Domain_ShouldNotReference_MicrosoftExtensions()
    {
        // The domain must stay a pure POCO model, free of framework/IO concerns.
        var result = Types.InAssembly(DomainAsm)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.Extensions")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildMessage(result));
    }

    private static string BuildMessage(TestResult result)
        => result.IsSuccessful
            ? string.Empty
            : "Offending types: " + string.Join(", ", result.FailingTypeNames ?? new List<string>());
}
