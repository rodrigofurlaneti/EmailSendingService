# EmailSendingService

API .NET 9 que recebe um DTO de e-mail e, na camada de infraestrutura, realiza o **disparo via SMTP implementado 100% em C# puro** (sockets `TcpClient` + `SslStream`, sem MailKit e sem `System.Net.Mail`). Construída com **DDD + Clean Architecture**, boas práticas e três camadas de teste: **UnitTest, BddTest e ArchTest**.

> Status: compila com 0 erros/0 warnings e **45 testes passando** (38 unit — incluindo integração sobre socket real, resolver DNS MX e assinatura DKIM verificada por criptografia —, 3 BDD, 4 de arquitetura).

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
│  ├─ EmailSendingService.Infrastructure   # SMTP em C# puro (SmtpSession, SmtpTransport,
│  │                                        #  MimeMessageBuilder, DnsMxResolver, DkimSigner)
│  └─ EmailSendingService.Api              # Web API + Swagger + middleware de erros
├─ tests/
│  ├─ EmailSendingService.UnitTests        # xUnit + FluentAssertions + NSubstitute
│  ├─ EmailSendingService.BddTests         # Reqnroll (Gherkin .feature)
│  └─ EmailSendingService.ArchTests        # NetArchTest (regras de camadas)
├─ tools/
│  ├─ EmailSendingService.SmtpCatcher      # Servidor SMTP local de teste (100% C#, sem Docker)
│  └─ EmailSendingService.DkimKeygen       # Gera par de chaves DKIM + registros DNS
├─ docker-compose.yml                      # (Opcional) MailHog como SMTP local
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

Use o **SMTP Catcher** incluído (100% C#, sem Docker) — veja a seção "Testar localmente sem servidor externo" abaixo. Alternativamente, se tiver Docker, `docker compose up -d` sobe o MailHog em `http://localhost:8025`.

## Entrega real em produção (DirectMx sem relay)

O código é um MTA completo, mas **entregar em caixas de grandes provedores (Hotmail/Gmail) depende de infraestrutura**, não do código. De um IP residencial a entrega direta é recusada (ex.: `550 blocked using Spamhaus`). Para funcionar de verdade você precisa:

1. **Host com IP dedicado e reputação limpa** (VPS/servidor), não IP residencial. Confirme que a **porta 25 de saída** está liberada (muitas clouds bloqueiam por padrão — abra um ticket).
2. **PTR (DNS reverso)** do IP apontando para o hostname do seu servidor (`mail.seudominio.com`). Configurado no provedor do IP.
3. **`ClientHostName`** no appsettings igual a esse FQDN (usado no EHLO/HELO).
4. **SPF** — TXT no domínio: `v=spf1 ip4:SEU.IP -all`.
5. **DKIM** — assine as mensagens (suporte nativo, abaixo) e publique a chave pública em `<selector>._domainkey.seudominio.com`.
6. **DMARC** — TXT em `_dmarc.seudominio.com`: `v=DMARC1; p=none; rua=mailto:postmaster@seudominio.com`.

Com esses seis itens, o modo `DirectMx` entrega direto no MX do destinatário, **sem nenhum SMTP externo**.

### Assinatura DKIM (100% C#)

Gerar o par de chaves e ver os registros DNS a publicar:

```bash
dotnet run --project tools/EmailSendingService.DkimKeygen -- seudominio.com default
```

Isso salva a chave privada em `dkim-keys/` e imprime o TXT do DKIM (+ sugestões de SPF/DMARC). Depois habilite no `appsettings.json`:

```json
"Smtp": {
  "DeliveryMode": "DirectMx",
  "ClientHostName": "mail.seudominio.com",
  "DefaultFromAddress": "no-reply@seudominio.com",
  "Dkim": {
    "Enabled": true,
    "Domain": "seudominio.com",
    "Selector": "default",
    "PrivateKeyPath": "dkim-keys/default.seudominio.com.private.pem"
  }
}
```

A assinatura usa RSA-SHA256 com canonicalização relaxed/relaxed (`Smtp/DkimSigner.cs`), assinando os cabeçalhos `from:to:cc:subject:date:message-id` e o hash do corpo.

## Testar localmente sem servidor externo (SMTP Catcher)

Sem Docker e sem ferramentas de terceiros — um catcher SMTP próprio, 100% C#, em `tools/EmailSendingService.SmtpCatcher`. Rode da **raiz do projeto**:

```bash
# Terminal 1 (deixe rodando)
dotnet run --project tools/EmailSendingService.SmtpCatcher

# Terminal 2
dotnet run --project src/EmailSendingService.Api
```

Com o `appsettings.Development.json` em modo `Relay` para `127.0.0.1:1025`, o POST em `/api/Emails` cai no catcher, que imprime o e-mail e salva um `.eml` em `received-emails/`. Serve para validar toda a cadeia (DTO → domínio → MIME → SMTP) sem depender de reputação de IP.

## Pré-requisitos

- .NET SDK 9.0
- (Opcional) Docker, apenas se quiser usar o MailHog em vez do SMTP Catcher

## Executar (2 terminais, a partir da raiz do projeto)

```bash
# Terminal 1 — servidor SMTP local de teste (deixe rodando)
dotnet run --project tools/EmailSendingService.SmtpCatcher

# Terminal 2 — a API (Swagger em http://localhost:5080/swagger)
dotnet run --project src/EmailSendingService.Api
```

O `appsettings.Development.json` já vem em modo `Relay` apontando para `127.0.0.1:1025` (o catcher). Enviar um e-mail:

```bash
curl -X POST http://localhost:5080/api/Emails \
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

O catcher imprime o e-mail recebido e salva um `.eml` em `received-emails/`.

### Enviar de verdade (Gmail) sem servidor de teste

No `appsettings.Development.json`, use modo `Relay` com o SMTP do Gmail e uma **senha de app** (conta com 2FA). Guarde a senha em `dotnet user-secrets`, nunca no arquivo versionado:

```bash
cd src/EmailSendingService.Api
dotnet user-secrets set "Smtp:Username" "voce@gmail.com"
dotnet user-secrets set "Smtp:Password" "sua-senha-de-app"
```

```json
"Smtp": { "DeliveryMode": "Relay", "Host": "smtp.gmail.com", "Port": 587, "UseStartTls": true, "DefaultFromAddress": "voce@gmail.com" }
```

No DTO, o `from` deve ser o próprio Gmail autenticado.

## Configuração (`appsettings.json` → seção `Smtp`)

| Chave | Descrição |
|-------|-----------|
| `DeliveryMode` | `DirectMx` (MTA próprio) ou `Relay` (encaminha por outro SMTP) |
| `DnsServer` | Servidor DNS para resolver MX no modo DirectMx (padrão `8.8.8.8`) |
| `DirectMxPort` | Porta usada na entrega direta ao MX (padrão `25`) |
| `Host` / `Port` | Servidor SMTP no modo Relay |
| `UseStartTls` | `STARTTLS` em portas 25/587 |
| `UseImplicitTls` | TLS implícito na porta 465 |
| `Username` / `Password` | Credenciais para `AUTH LOGIN` (vazio = sem auth) |
| `DefaultFromAddress` / `DefaultFromName` | Remetente padrão quando o DTO não informa `from` |
| `ClientHostName` | Nome usado no EHLO/HELO (use um FQDN real em produção) |
| `TimeoutMilliseconds` | Timeout de conexão/leitura |
| `AllowInvalidCertificates` | Aceita certificados TLS inválidos (apenas servidores de teste) |
| `Dkim` | Assinatura DKIM: `Enabled`, `Domain`, `Selector`, `PrivateKeyPath` |

## Anexos

O DTO aceita uma lista `attachments`, cada item com `fileName`, `contentType` e `contentBase64`. Para anexar um arquivo real, gere o Base64 (ex.: PowerShell):

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\caminho\arquivo.pdf"))
```

Itens de anexo totalmente vazios são ignorados. Com anexos, a mensagem é montada como `multipart/mixed`.

## Testes

```bash
dotnet test            # roda as 3 camadas: Unit, BDD e Arch
```

- **UnitTests** — Value Objects, agregado, `MimeMessageBuilder`, protocolo `SmtpSession`, parser do resolver DNS MX, assinatura DKIM (assina e **verifica** com a chave pública) e integração sobre socket real (loopback) tanto no envio relay quanto no DirectMx.
- **BddTests** — cenários Gherkin em `Features/SendEmail.feature` (envio válido, destinatário inválido, assunto ausente).
- **ArchTests** — garante a regra de dependência da Clean Architecture.

## Notas técnicas

- No modo `DirectMx`, o `DirectSmtpEmailSender` agrupa os destinatários por domínio, resolve o MX via DNS e entrega direto no servidor de destino (um envio por MX), sem relay.
- O envio via `STARTTLS` reaproveita o mesmo socket após o upgrade TLS. Para servidores públicos (Gmail/Outlook) use porta 587 (`UseStartTls`) ou 465 (`UseImplicitTls`).
- Com DKIM habilitado, cada mensagem recebe um cabeçalho `DKIM-Signature` (RSA-SHA256, relaxed/relaxed) antes do envio.
- O `Bcc` é entregue via `RCPT TO`, mas nunca aparece nos cabeçalhos da mensagem.
- Conteúdo e anexos são codificados em Base64 (quebra em 76 colunas, conforme MIME).
- Segredos (senha de app, chaves DKIM) ficam fora do repositório: use `dotnet user-secrets`; `*.pem`, `dkim-keys/` e `received-emails/` estão no `.gitignore`.
