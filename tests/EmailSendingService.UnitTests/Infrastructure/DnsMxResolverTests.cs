using System.Text;
using EmailSendingService.Infrastructure.Dns;
using FluentAssertions;
using Xunit;

namespace EmailSendingService.UnitTests.Infrastructure;

public class DnsMxResolverTests
{
    [Fact]
    public void BuildQuery_EncodesDomainAndMxQuestion()
    {
        var query = DnsMxResolver.BuildQuery("example.com");

        // Header: QDCOUNT = 1
        query[4].Should().Be(0x00);
        query[5].Should().Be(0x01);
        // QNAME contains the labels
        Encoding.ASCII.GetString(query).Should().Contain("example").And.Contain("com");
        // QTYPE = 15 (MX) at the end
        query[^4].Should().Be(0x00);
        query[^3].Should().Be(0x0F);
    }

    [Fact]
    public void ParseMxRecords_ReturnsPreferenceAndExchange()
    {
        var packet = BuildMxResponse("example.com",
            (10, "mail.example.com"),
            (20, "backup.example.com"));

        var records = DnsMxResolver.ParseMxRecords(packet);

        records.Should().HaveCount(2);
        records.Should().ContainSingle(r => r.Preference == 10 && r.Exchange == "mail.example.com");
        records.Should().ContainSingle(r => r.Preference == 20 && r.Exchange == "backup.example.com");
    }

    // Builds a minimal but valid DNS MX response packet.
    private static byte[] BuildMxResponse(string queriedDomain, params (int pref, string host)[] answers)
    {
        var b = new List<byte>();
        // Header
        b.AddRange(new byte[] { 0x00, 0x01 });              // ID
        b.AddRange(new byte[] { 0x81, 0x80 });              // flags (response, recursion available)
        b.AddRange(new byte[] { 0x00, 0x01 });              // QDCOUNT
        b.AddRange(new byte[] { 0x00, (byte)answers.Length }); // ANCOUNT
        b.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });  // NSCOUNT + ARCOUNT

        int questionNameOffset = b.Count;
        AppendName(b, queriedDomain);
        b.AddRange(new byte[] { 0x00, 0x0F, 0x00, 0x01 });  // QTYPE MX, QCLASS IN

        foreach (var (pref, host) in answers)
        {
            // NAME as compression pointer to the question name
            b.Add((byte)(0xC0 | (questionNameOffset >> 8)));
            b.Add((byte)(questionNameOffset & 0xFF));
            b.AddRange(new byte[] { 0x00, 0x0F });          // TYPE MX
            b.AddRange(new byte[] { 0x00, 0x01 });          // CLASS IN
            b.AddRange(new byte[] { 0x00, 0x00, 0x0E, 0x10 }); // TTL

            var rdata = new List<byte> { (byte)(pref >> 8), (byte)(pref & 0xFF) };
            AppendName(rdata, host);
            b.Add((byte)(rdata.Count >> 8));
            b.Add((byte)(rdata.Count & 0xFF));              // RDLENGTH
            b.AddRange(rdata);
        }

        return b.ToArray();
    }

    private static void AppendName(List<byte> buffer, string name)
    {
        foreach (var label in name.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            buffer.Add((byte)bytes.Length);
            buffer.AddRange(bytes);
        }
        buffer.Add(0x00);
    }
}
