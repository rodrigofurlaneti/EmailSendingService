# EmailSendingService

API .NET 9 que recebe um DTO de e-mail e, na camada de infraestrutura, realiza o **disparo via SMTP implementado 100% em C# puro** (sockets `TcpClient` + `SslStream`, sem MailKit e sem `System.Net.Mail`). Construída com **DDD + Clean Architecture**, boas práticas e três camadas de teste: **UnitTest, BddTest e ArchTest**.

> Status: compila com 0 erros/0 warnings e **39 testes passando** (32 unit — incluindo um teste de integração sobre socket real —, 3 BDD, 4 de arquitetura).

## Arquitetura (a dependência aponta para dentro)

```
              +-------------------+
  HTTP  --->  |   Api (Web API)   |  Controller recebe o DTO
              +---------+---------+
                        |
        +---------------+----------------+
        v                                v
+---------------+                +------------------------+
| Application   |  (porta        | Infrastructure          |
| use case      |   IEmailSender)|  SmtpEmailSender (SMTP  |
| DTO -> Domain |<---------------|  puro via sockets)      |
+-------+-------+                +-----------+------------+
        |                                    |
        v                                    v
+-------------------------------------------------+
|                    Domain                        |
|  EmailMessage (aggregate), EmailAddress (VO),    |
|  EmailAttachment, regras/invariantes, exceptions |
+-------------------------------------------------+
```

Regra de dependência (validada automaticamente no **ArchTests**):

- `Domain` não referencia nenhuma camada externa nem `Microsoft.Extensions.*` (POCO puro).
- `Application` depende apenas de `Domain`; define a porta `IEmailSender` (Dependency Inversion).
- `Infrastructure` implementa a porta e não conhece a `Api`.
- `Api` apenas orquestra a injeção de dependências.

## Estrutura de pastas

```
EmailSendingService/
├─ src/
│  ├─ EmailSendingService.Domain          # Entidades, Value Objects, exceções de domínio
│  ├─ EmailSendingService.Application      # DTOs, porta IEmailSender, use case SendEmail
│  ├─ EmailSendingService.Infrastructure   # SMTP em C# puro (SmtpSession, SmtpTransport, MimeMessageBuilder)
│  └─ EmailSendingService.Api              # Web API + Swagger + middleware de erros
├─ tests/
│  ├─ EmailSendingService.UnitTests        # xUnit + FluentAssertions + NSubstitute
│  ├─ EmailSendingService.BddTests         # Reqnroll (Gherkin .feature)
│  └─ EmailSendingService.ArchTests        # NetArchTest (regras de camadas)
├─ docker-compose.yml                      # MailHog (SMTP local para teste manual)
├─ sample-request.json                     # Exemplo de corpo da requisição
├─ run.sh / test.sh
└─ EmailSendingService.sln
```

## Como o SMTP funciona (100% C#)

Toda a conversa SMTP é implementada à mão em `Infrastructure/Smtp`:

- **`SmtpSession`** — motor do protocolo RFC 5321 sobre qualquer `TextReader`/`TextWriter`: leitura de respostas multi-linha, `EHLO`, `AUTH LOGIN` (Base64), `MAIL FROM`, `RCPT TO`, `DATA` com *dot-stuffing*. Por ser desacoplado de socket, é 100% testável em memória.
- **`SmtpTransport`** — dono do socket TCP e do upgrade TLS: conecta, faz `STARTTLS` (portas 25/587) ou TLS implícito (porta 465), autentica.
- **`MimeMessageBuilder`** — serializa o `EmailMessage` em RFC 5322/MIME: cabeçalhos UTF-8 (encoded-words), corpo `text/plain` ou `text/html`, e `multipart/mixed` quando há anexos. `Bcc` nunca vaza para os cabeçalhos.
- **`SmtpEmailSender`** — implementa a porta `IEmailSender` orquestrando tudo acima.

## Modos de entrega (`Smtp:DeliveryMode`)

A aplicação é **o próprio servidor de envio** — não precisa de um SMTP externo. Há dois modos:

### `DirectMx` (padrão) — a app age como MTA

Não há relay. Para cada destinatário a app:

1. resolve o **MX do domínio** via DNS (cliente DNS próprio, UDP/53, em `Dns/DnsMxResolver.cs`);
2. conecta **direto no servidor do destinatário na porta 25** e executa a conversa SMTP (`MAIL FROM`/`RCPT TO`/`DATA`), com STARTTLS oportunista.

Destinatários são agrupados por domínio (um envio por MX). Configuração:

```json
"Smtp": {
  "DeliveryMode": "DirectMx",
  "DnsServer": "8.8.8.8",
  "DirectMxPort": 25,
  "ClientHostName": "mail.seudominio.com",
  "DefaultFromAddress": "no-reply@seudominio.com"
}
```

> **Atenção — realidade da entrega direta.** Ser seu próprio MTA tem implicações que independem do código:
> - A **porta 25 de saída** costuma ser **bloqueada** por provedores residenciais e por várias clouds (Azure/AWS/GCP). Se der *connection refused/timeout*, é bloqueio de rede — não da aplicação.
> - Servidores como Outlook/Gmail exigem **PTR (DNS reverso), SPF, DKIM e DMARC** e boa reputação de IP. Enviando de um IP doméstico, a mensagem tende a ser **recusada ou cair em spam**.
> - O `ClientHostName` (usado no EHLO/HELO) deve ser um FQDN real do seu servidor.
>
> Ou seja: o código entrega direto no MX de verdade, mas para chegar na caixa de entrada de terceiros você precisa de um IP com reputação, DNS reverso e registros de autenticação — exatamente o que um MTA de produção exige.

### `Relay` — encaminha por um SMTP existente

Se preferir usar um SMTP (Gmail/Outlook/MailHog), troque o modo:

```json
"Smtp": {
  "DeliveryMode": "Relay",
  "Host": "smtp.gmail.com",
  "Port": 587,
  "UseStartTls": true,
  "Username": "voce@gmail.com",
  "Password": "app-password",
  "DefaultFromAddress": "voce@gmail.com"
}
```

### Testar localmente sem depender de nada externo

Suba o MailHog (modo `Relay`, `Host=localhost`, `Port=1025`) e veja tudo em `http://localhost:8025`:

```bash
docker compose up -d
```

## Pré-requisitos

- .NET SDK 9.0
- (Opcional) Docker, para subir um servidor SMTP local (MailHog)

## Executar

```bash
# 1) SMTP local para capturar os e-mails (UI em http://localhost:8025)
docker compose up -d

# 2) subir a API (Swagger em http://localhost:5080/swagger)
dotnet run --project src/EmailSendingService.Api
```

Enviar um e-mail:

```bash
curl -X POST http://localhost:5080/api/emails \
  -H "Content-Type: application/json" \
  -d @sample-request.json
```

Resposta (`202 Accepted`):

```json
{
  "emailId": "…",
  "providerMessageId": "…@example.com",
  "delivered": true,
  "sentAtUtc": "…"
}
```

Abra `http://localhost:8025` para ver a mensagem capturada pelo MailHog.

## Configuração (`appsettings.json` → seção `Smtp`)

| Chave | Descrição |
|-------|-----------|
| `Host` / `Port` | Servidor SMTP (padrão `localhost:1025` = MailHog) |
| `UseStartTls` | `STARTTLS` em portas 25/587 |
| `UseImplicitTls` | TLS implícito na porta 465 |
| `Username` / `Password` | Credenciais para `AUTH LOGIN` (vazio = sem auth) |
| `DefaultFromAddress` / `DefaultFromName` | Remetente padrão quando o DTO não informa `from` |
| `AllowInvalidCertificates` | Aceita certificados inválidos (apenas para servidores locais) |

### Exemplo Gmail (produção)

```json
"Smtp": {
  "Host": "smtp.gmail.com",
  "Port": 587,
  "UseStartTls": true,
  "Username": "voce@gmail.com",
  "Password": "sua-app-password",
  "DefaultFromAddress": "voce@gmail.com"
}
```

## Testes

```bash
dotnet test            # roda as 3 camadas: Unit, BDD e Arch
```

- **UnitTests** — Value Objects, agregado, `MimeMessageBuilder`, protocolo `SmtpSession` e um teste de integração que sobe um servidor SMTP loopback e valida o envio sobre socket real.
- **BddTests** — cenários Gherkin em `Features/SendEmail.feature` (envio válido, destinatário inválido, assunto ausente).
- **ArchTests** — garante a regra de dependência da Clean Architecture.

## Notas técnicas

- O envio via `STARTTLS` reaproveita o mesmo socket após o upgrade TLS. Para servidores públicos (Gmail/Outlook) use porta 587 (`UseStartTls`) ou 465 (`UseImplicitTls`).
- O `Bcc` é entregue via `RCPT TO`, mas nunca aparece nos cabeçalhos da mensagem.
- Conteúdo e anexos são codificados em Base64 (quebra em 76 colunas, conforme MIME).
