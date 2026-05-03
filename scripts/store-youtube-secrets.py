"""
Stores YouTube OAuth credentials in Azure Key Vault and wires up
the Azure Function app settings to use Key Vault references.

Usage:
  python store-youtube-secrets.py <client_id> <client_secret> <refresh_token>

Or just run without args to read from youtube-creds.json (output of get-youtube-refresh-token.py).
"""

import json
import subprocess
import sys

SUBSCRIPTION = "9fd42f26-d2b2-4371-b607-6495c3dde570"
KEYVAULT     = "kv-carfacts"
FUNCTION_APP = "func-poc-vidgen"
RESOURCE_GRP = "rg-carfacts"

def az(*args):
    result = subprocess.run(["az", *args], capture_output=True, text=True)
    if result.returncode != 0:
        print(f"ERROR: {result.stderr.strip()}")
        sys.exit(1)
    return result.stdout.strip()

def main():
    if len(sys.argv) == 4:
        client_id, client_secret, refresh_token = sys.argv[1], sys.argv[2], sys.argv[3]
    else:
        try:
            with open("youtube-creds.json") as f:
                creds = json.load(f)
            client_id     = creds["client_id"]
            client_secret = creds["client_secret"]
            refresh_token = creds["refresh_token"]
            print("Loaded credentials from youtube-creds.json")
        except FileNotFoundError:
            print("Usage: python store-youtube-secrets.py <client_id> <client_secret> <refresh_token>")
            print("       or run get-youtube-refresh-token.py first to generate youtube-creds.json")
            sys.exit(1)

    print(f"\nStoring secrets in Key Vault: {KEYVAULT}")

    secrets = {
        "YouTube--ClientId":     client_id,
        "YouTube--ClientSecret": client_secret,
        "YouTube--RefreshToken": refresh_token,
    }

    for name, value in secrets.items():
        print(f"  Setting {name}...", end=" ", flush=True)
        az("keyvault", "secret", "set",
           "--vault-name", KEYVAULT,
           "--subscription", SUBSCRIPTION,
           "--name", name,
           "--value", value,
           "--output", "none")
        print("done")

    print(f"\nWiring Key Vault references into Function App: {FUNCTION_APP}")

    kv_ref = lambda secret: f"@Microsoft.KeyVault(VaultName={KEYVAULT};SecretName={secret})"

    az("functionapp", "config", "appsettings", "set",
       "--subscription", SUBSCRIPTION,
       "-g", RESOURCE_GRP,
       "-n", FUNCTION_APP,
       "--settings",
       f"YouTube:ClientId={kv_ref('YouTube--ClientId')}",
       f"YouTube:ClientSecret={kv_ref('YouTube--ClientSecret')}",
       f"YouTube:RefreshToken={kv_ref('YouTube--RefreshToken')}",
       "--output", "none")
    print("  App settings updated with Key Vault references.")

    print("\nDone! The function app will now publish videos to YouTube automatically.")
    print("You can delete youtube-creds.json now.")

if __name__ == "__main__":
    main()
