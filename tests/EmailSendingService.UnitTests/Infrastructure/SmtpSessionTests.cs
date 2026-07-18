using System.Text;
using EmailSendingService.Infrastructure.Smtp;
using FluentAssertions;
using Xunit;

namespace EmailSendingService.UnitTests.Infrastructure;

public class SmtpSessionTests
{
    private static (SmtpSession session, StringWriter client) CreateSession(string serverScript)
    {
        var reader = new StringReader(serverScript);
        var client = new StringWriter { NewLine = "\r\n" };
        return (new SmtpSession(reader, client), client);
    }

    [Fact]
    public async Task ReadReplyAsync_ParsesMultiLineReply()
    {
        var (session, _) = CreateSession("250-mail.example.com\r\n250-STARTTLS\r\n250 OK\r\n");
        var reply = await session.ReadReplyAsync();

        reply.Code.Should().Be(250);
        reply.Lines.Should().HaveCount(3);
        reply.Lines.Should().Contain("STARTTLS");
        reply.IsPositiveCompletion.Should().BeTrue();
    }

    [Fact]
    public async Task SendCommandAsync_OnUnexpectedCode_Throws()
    {
        var (session, _) = CreateSession("500 command not recognized\r\n");
        var act = async () => await session.SendCommandAsync("EHLO x", r => r.IsPositiveCompletion);
        await act.Should().ThrowAsync<SmtpException>();
    }

    [Fact]
    public async Task AuthLoginAsync_SendsBase64Credentials()
    {
        var (session, client) = CreateSession("334 VXNlcm5hbWU6\r\n334 UGFzc3dvcmQ6\r\n235 Authenticated\r\n");
        await session.AuthLoginAsync("user", "pass");

        var sent = client.ToString();
        sent.Should().Contain("AUTH LOGIN");
        sent.Should().Contain(Convert.ToBase64String(Encoding.UTF8.GetBytes("user")));
        sent.Should().Contain(Convert.ToBase64String(Encoding.UTF8.GetBytes("pass")));
    }

    [Fact]
    public async Task DataAsync_SendsBodyTerminatedWithDot()
    {
        var (session, client) = CreateSession("354 End data with <CRLF>.<CRLF>\r\n250 OK queued\r\n");
        await session.DataAsync("Subject: hi\r\n\r\nbody");

        var sent = client.ToString();
        sent.Should().StartWith("DATA\r\n");
        sent.Should().EndWith("\r\n.\r\n");
    }

    [Fact]
    public void DotStuff_EscapesLeadingDots()
    {
        var stuffed = SmtpSession.DotStuff("normal\r\n.hidden\r\n..double");
        stuffed.Should().Be("normal\r\n..hidden\r\n...double");
    }
}
