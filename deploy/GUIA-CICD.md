# CI/CD — EmailSendingService.Api → VM Azure (GitHub Actions + Docker)

Deploy automático da API **.NET 9** na VM **Ubuntu 20.04** (`20.118.214.216`).

**Fluxo:** `push` na `main` → workflow **CI** (`ci.yml`) faz build e testes → ao passar,
o **Deploy** (`deploy.yml`) faz build da imagem Docker → publica no **GHCR** →
conecta por **SSH** na VM → `docker pull` e sobe o container. A API expõe `/health`.

## Arquivos criados

| Arquivo | Onde fica |
|---|---|
| `Dockerfile`, `.dockerignore` | raiz do repo |
| `.github/workflows/deploy.yml` | já no lugar |
| `deploy/docker-compose.prod.yml` | referência; opcional copiar pra VM |
| `deploy/setup-vm.sh` | rodar 1x na VM |

> Seus workflows existentes (`ci.yml`, `publish.yml`) foram preservados. O `docker-compose.yml`
> da raiz (MailHog de teste) também — o compose de produção está em `deploy/`.

---

## Passo 1 — Preparar a VM (uma vez)

```bash
ssh SEU_USUARIO@20.118.214.216
# copie deploy/setup-vm.sh para a VM e rode:
bash setup-vm.sh
# faça logout/login e edite as credenciais:
sudo nano /opt/emailservice/.env
```

> **Atenção SMTP:** a VM Azure **bloqueia a porta 25 de saída**. O modo `DirectMx`
> padrão do serviço não vai enviar e-mails na VM. Use um **relay** (SendGrid, Mailgun,
> Amazon SES, Office365) — o `.env` já vem com esse formato preenchível.

## Passo 2 — Liberar porta no NSG (Azure)

Portal → sua VM → **Configurações de rede** → **Adicionar regra de porta de entrada**:
porta `80`, TCP, Permitir, prioridade livre (ex.: 310). SSH (22) já costuma estar aberto.

## Passo 3 — Chave SSH de deploy

```bash
ssh-keygen -t ed25519 -f ./deploy_key -C "gh-actions" -N ""
ssh-copy-id -i ./deploy_key.pub SEU_USUARIO@20.118.214.216
ssh -i ./deploy_key SEU_USUARIO@20.118.214.216 "docker --version"
```

## Passo 4 — PAT para a VM baixar do GHCR

GitHub → **Settings → Developer settings → Personal access tokens (classic)** →
gerar com escopo **`read:packages`**. (Se o pacote for público, dispensável.)

## Passo 5 — Secrets no repositório

**Settings → Secrets and variables → Actions:**

| Secret | Valor |
|---|---|
| `VM_HOST` | `20.118.214.216` |
| `VM_USER` | usuário admin da VM |
| `VM_SSH_KEY` | conteúdo da chave privada `deploy_key` |
| `GHCR_PAT` | token `read:packages` |

## Passo 6 — Commitar e disparar

```bash
git add Dockerfile .dockerignore .github/workflows/deploy.yml deploy/
git commit -m "ci: deploy Docker na VM Azure"
git push origin main
```

O CI roda; ao passar, o Deploy publica a imagem e reimplanta. Também dá pra rodar
manual em **Actions → Deploy to Azure VM → Run workflow**.

## Passo 7 — Verificar

```bash
# na VM
docker ps
docker logs emailsendingservice-api
```
No navegador: `http://20.118.214.216/health` → `{"status":"healthy"}`.

---

## Troubleshooting

| Sintoma | Correção |
|---|---|
| `Permission denied (publickey)` | `VM_SSH_KEY` incompleta ou pública não autorizada na VM |
| `denied` no `docker pull` | `GHCR_PAT` sem `read:packages` (pacote privado) |
| Container reinicia | `docker logs`; variável faltando no `.env` |
| `/health` não responde | porta 80 não liberada no NSG |
| E-mails não saem | porta 25 bloqueada no Azure — use relay SMTP |
