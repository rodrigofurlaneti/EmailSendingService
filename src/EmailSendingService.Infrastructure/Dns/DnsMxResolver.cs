using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EmailSendingService.Infrastructure.Dns;

/// <summary>
/// Minimal DNS client (RFC 1035) implemented in pure C# over UDP. It queries a
/// resolver for the MX records of a domain so the service can deliver mail
/// directly to the recipient's mail server — no external SMTP relay involved.
/// Falls back to the domain itself (implicit MX / A record) when no MX exists.
/// </summary>
public sealed class DnsMxResolver : IMxResolver
{
    private const ushort TypeMx = 15;
    private const ushort ClassIn = 1;

    private readonly string _dnsServer;
    private readonly int _timeoutMs;

    public DnsMxResolver(string dnsServer = "8.8.8.8", int timeoutMs = 10_000)
    {
        _dnsServer = string.IsNullOrWhiteSpace(dnsServer) ? "8.8.8.8" : dnsServer;
        _timeoutMs = timeoutMs;
    }

    public async Task<IReadOnlyList<string>> ResolveAsync(string domain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return Array.Empty<string>();

        var query = BuildQuery(domain);

        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Connect(IPAddress.Parse(_dnsServer), 53);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeoutMs);

        await udp.SendAsync(query, cancellationToken);
        var response = await udp.ReceiveAsync(timeoutCts.Token);

        var records = ParseMxRecords(response.Buffer);

        // Order by preference (lowest first); fall back to the domain itself.
        var hosts = records
            .OrderBy(r => r.Preference)
            .Select(r => r.Exchange)
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToList();

        if (hosts.Count == 0)
            hosts.Add(domain);

        return hosts;
    }

    // -------- wire-format helpers (internal for unit testing) --------

    internal static byte[] BuildQuery(string domain)
    {
        var buffer = new List<byte>(32);

        var id = (ushort)Random.Shared.Next(1, ushort.MaxValue);
        buffer.Add((byte)(id >> 8));
        buffer.Add((byte)(id & 0xFF));
        buffer.Add(0x01); buffer.Add(0x00);   // flags: standard query, recursion desired
        buffer.Add(0x00); buffer.Add(0x01);   // QDCOUNT = 1
        buffer.Add(0x00); buffer.Add(0x00);   // ANCOUNT
        buffer.Add(0x00); buffer.Add(0x00);   // NSCOUNT
        buffer.Add(0x00); buffer.Add(0x00);   // ARCOUNT

        foreach (var label in domain.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            buffer.Add((byte)bytes.Length);
            buffer.AddRange(bytes);
        }
        buffer.Add(0x00);                      // end of QNAME

        buffer.Add((byte)(TypeMx >> 8)); buffer.Add((byte)(TypeMx & 0xFF));
        buffer.Add((byte)(ClassIn >> 8)); buffer.Add((byte)(ClassIn & 0xFF));

        return buffer.ToArray();
    }

    internal static List<MxRecord> ParseMxRecords(byte[] message)
    {
        var result = new List<MxRecord>();
        if (message.Length < 12)
            return result;

        int qdCount = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(4, 2));
        int anCount = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(6, 2));

        int offset = 12;

        // Skip the question section.
        for (int i = 0; i < qdCount; i++)
        {
            offset = SkipName(message, offset);
            offset += 4; // QTYPE + QCLASS
        }

        for (int i = 0; i < anCount && offset < message.Length; i++)
        {
            offset = SkipName(message, offset);           // NAME
            if (offset + 10 > message.Length) break;

            ushort type = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(offset, 2));
            int rdLength = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(offset + 8, 2));
            int rdStart = offset + 10;

            if (type == TypeMx && rdStart + 2 <= message.Length)
            {
                ushort preference = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(rdStart, 2));
                string exchange = ReadName(message, rdStart + 2, out _);
                result.Add(new MxRecord(preference, exchange));
            }

            offset = rdStart + rdLength;
        }

        return result;
    }

    /// <summary>Advances past a (possibly compressed) domain name, returning the next offset.</summary>
    private static int SkipName(byte[] msg, int offset)
    {
        while (offset < msg.Length)
        {
            byte len = msg[offset];
            if (len == 0) return offset + 1;
            if ((len & 0xC0) == 0xC0) return offset + 2; // compression pointer ends the name here
            offset += len + 1;
        }
        return offset;
    }

    /// <summary>Reads a domain name, following compression pointers.</summary>
    private static string ReadName(byte[] msg, int offset, out int nextOffset)
    {
        var labels = new List<string>();
        nextOffset = -1;
        bool jumped = false;
        int guard = 0;

        while (offset < msg.Length && guard++ < 128)
        {
            byte len = msg[offset];
            if (len == 0)
            {
                if (!jumped) nextOffset = offset + 1;
                break;
            }

            if ((len & 0xC0) == 0xC0)
            {
                int pointer = ((len & 0x3F) << 8) | msg[offset + 1];
                if (!jumped) nextOffset = offset + 2;
                jumped = true;
                offset = pointer;
                continue;
            }

            labels.Add(Encoding.ASCII.GetString(msg, offset + 1, len));
            offset += len + 1;
        }

        return string.Join('.', labels);
    }

    internal readonly record struct MxRecord(int Preference, string Exchange);
}
