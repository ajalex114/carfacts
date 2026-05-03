import subprocess, shutil, json, sys

sub  = "9fd42f26-d2b2-4371-b607-6495c3dde570"
rg   = "rg-carfacts"
app  = "func-poc-vidgen"

# Use Azure Python SDK to avoid cmd.exe semicolon parsing issues
from azure.identity import AzureCliCredential
from azure.mgmt.web import WebSiteManagementClient

credential = AzureCliCredential()
client     = WebSiteManagementClient(credential, sub)

# Get existing app settings
existing = client.web_apps.list_application_settings(rg, app)
settings = dict(existing.properties)

# Add KV references
kv_settings = {
    "YouTube:ClientId":     "@Microsoft.KeyVault(VaultName=kv-carfacts;SecretName=YouTube--ClientId)",
    "YouTube:ClientSecret": "@Microsoft.KeyVault(VaultName=kv-carfacts;SecretName=YouTube--ClientSecret)",
    "YouTube:RefreshToken": "@Microsoft.KeyVault(VaultName=kv-carfacts;SecretName=YouTube--RefreshToken)",
}
settings.update(kv_settings)

from azure.mgmt.web.models import StringDictionary
client.web_apps.update_application_settings(rg, app, StringDictionary(properties=settings))

for k, v in kv_settings.items():
    print(f"  {k} = {v}")
print("\nDone! Key Vault references wired up.")
