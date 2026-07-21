#!/usr/bin/env bash
# ============================================================
# Preparação da VM Azure (Ubuntu 20.04) — rode UMA vez na VM:
#   ssh <usuario>@20.118.214.216
#   bash setup-vm.sh
# ============================================================
set -e

echo ">> Atualizando pacotes..."
sudo apt-get update -y

echo ">> Instalando Docker Engine..."
if ! command -v docker &> /dev/null; then
  curl -fsSL https://get.docker.com | sudo sh
fi
sudo systemctl enable --now docker
sudo usermod -aG docker "$USER"

echo ">> Criando /opt/emailservice/.env ..."
sudo mkdir -p /opt/emailservice
if [ ! -f /opt/emailservice/.env ]; then
  sudo tee /opt/emailservice/.env > /dev/null <<'EOF'
# Configuração do EmailSendingService (chaves ASP.NET usam __ para aninhar)
ASPNETCORE_ENVIRONMENT=Production

# IMPORTANTE: o Azure BLOQUEIA a porta 25 de saída por padrão, então
# "DirectMx" NÃO funciona na VM. Use um relay SMTP (SendGrid, Mailgun,
# Amazon SES, Office365) com host/porta/credenciais abaixo.
Smtp__DeliveryMode=Relay
Smtp__Host=smtp.seurelay.com
Smtp__Port=587
Smtp__UseStartTls=true
Smtp__Username=apikey-ou-usuario
Smtp__Password=troque-aqui
Smtp__DefaultFromAddress=no-reply@seudominio.com
Smtp__DefaultFromName=Email Sending Service
EOF
  echo "   -> .env criado. EDITE com os valores reais: sudo nano /opt/emailservice/.env"
fi

echo ""
echo ">> Pronto. Faça logout/login (ou 'newgrp docker') para usar docker sem sudo."
echo ">> $(docker --version)"
