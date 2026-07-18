using System.Security.Cryptography;

// Generates a DKIM RSA key pair (100% C#) and prints:
//   1) the private key PEM (save it and point Smtp:Dkim:PrivateKeyPath to it), and
//   2) the DNS TXT record you must publish at <selector>._domainkey.<domain>.
//
// Usage:
//   dotnet run --project tools/EmailSendingService.DkimKeygen -- <domain> [selector]
// Example:
//   dotnet run --project tools/EmailSendingService.DkimKeygen -- seudominio.com default

string domain = args.Length > 0 ? args[0] : "seudominio.com";
string selector = args.Length > 1 ? args[1] : "default";

using var rsa = RSA.Create(2048);

string privatePem = rsa.ExportPkcs8PrivateKeyPem();
string publicBase64 = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());

var outDir = Path.Combine(Directory.GetCurrentDirectory(), "dkim-keys");
Directory.CreateDirectory(outDir);
var privatePath = Path.Combine(outDir, $"{selector}.{domain}.private.pem");
File.WriteAllText(privatePath, privatePem);

Console.WriteLine("======================================================");
Console.WriteLine("  DKIM key pair gerado (RSA 2048)");
Console.WriteLine("======================================================");
Console.WriteLine();
Console.WriteLine($"Chave privada salva em:\n  {privatePath}");
Console.WriteLine();
Console.WriteLine("No appsettings, aponte:");
Console.WriteLine($"  \"Dkim\": {{ \"Enabled\": true, \"Domain\": \"{domain}\", \"Selector\": \"{selector}\", \"PrivateKeyPath\": \"{privatePath.Replace("\\", "\\\\")}\" }}");
Console.WriteLine();
Console.WriteLine("Publique este registro DNS TXT:");
Console.WriteLine($"  Nome : {selector}._domainkey.{domain}");
Console.WriteLine( "  Tipo : TXT");
Console.WriteLine($"  Valor: v=DKIM1; k=rsa; p={publicBase64}");
Console.WriteLine();
Console.WriteLine("Sugestao de SPF (TXT no dominio raiz), ajuste o IP do seu servidor:");
Console.WriteLine($"  {domain}  TXT  \"v=spf1 ip4:SEU.IP.DO.SERVIDOR -all\"");
Console.WriteLine();
Console.WriteLine("Sugestao de DMARC (TXT em _dmarc." + domain + "):");
Console.WriteLine("  _dmarc." + domain + "  TXT  \"v=DMARC1; p=none; rua=mailto:postmaster@" + domain + "\"");
Console.WriteLine("======================================================");
