#!/usr/bin/env bash
set -e
# 1) (optional) start a local SMTP catcher:  docker compose up -d
# 2) run the API:
dotnet run --project src/EmailSendingService.Api
