#!/usr/bin/env bash
# Script pour basculer facilement entre le Mode Dev (sans auth) et le Mode Auth (Keycloak)

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$DIR"

echo "============================================================"
echo "     NextStep - Switch Auth Mode (Shell / Bash)"
echo "============================================================"
echo ""
echo "  1) Activer Mode AUTH (Keycloak / Login obligatoire)"
echo "  2) Activer Mode DEV  (Sans Login / Utilisateur Seed)"
echo "  3) Lancer Keycloak Docker (docker compose up -d keycloak)"
echo "  4) Quitter"
echo ""

read -p "Entrez votre choix [1-4] : " choice

case $choice in
  1)
    python scripts/switch_auth_mode.py auth
    ;;
  2)
    python scripts/switch_auth_mode.py dev
    ;;
  3)
    docker compose up -d keycloak
    ;;
  4)
    exit 0
    ;;
  *)
    echo "Choix invalide."
    ;;
esac
