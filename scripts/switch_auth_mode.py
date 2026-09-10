import sys
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ENV_TS = ROOT / "frontend" / "src" / "environments" / "environment.ts"
APP_SETTINGS = ROOT / "backend" / "appsettings.Development.json"

def set_mode(mode: str):
    is_auth = (mode.lower() == "auth")

    # 1. Update frontend environment.ts
    if ENV_TS.exists():
        content = ENV_TS.read_text(encoding="utf-8")
        new_content = re.sub(
            r"authEnabled:\s*(true|false)",
            f"authEnabled: {'true' if is_auth else 'false'}",
            content
        )
        ENV_TS.write_text(new_content, encoding="utf-8")
        print(f"[OK] Frontend environment.ts -> authEnabled = {'true' if is_auth else 'false'}")
    else:
        print(f"[WARN] {ENV_TS} non trouve")

    # 2. Update backend appsettings.Development.json
    if APP_SETTINGS.exists():
        content = APP_SETTINGS.read_text(encoding="utf-8")
        new_content = re.sub(
            r'"Mode":\s*"(Dev|Keycloak)"',
            f'"Mode": "{"Keycloak" if is_auth else "Dev"}"',
            content
        )
        APP_SETTINGS.write_text(new_content, encoding="utf-8")
        print(f"[OK] Backend appsettings.Development.json -> Auth:Mode = {'Keycloak' if is_auth else 'Dev'}")
    else:
        print(f"[WARN] {APP_SETTINGS} non trouve")

    print(f"\n>>> Mode actif : {'AUTH (Keycloak actif)' if is_auth else 'DEV (Sans authentification - mock)'}")

if __name__ == "__main__":
    if len(sys.argv) < 2 or sys.argv[1].lower() not in ("auth", "dev"):
        print("Usage: python switch_auth_mode.py [auth|dev]")
        sys.exit(1)
    set_mode(sys.argv[1])
