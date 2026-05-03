"""
One-time setup: creates a YouTube OAuth 2.0 client and gets a refresh token.
Run this ONCE. After that, videos publish to YouTube automatically forever.

Usage:
  pip install google-auth-oauthlib
  python get-youtube-refresh-token.py
"""

import json
import subprocess
import sys

try:
    from google_auth_oauthlib.flow import InstalledAppFlow
except ImportError:
    print("Installing google-auth-oauthlib...")
    subprocess.check_call([sys.executable, "-m", "pip", "install", "google-auth-oauthlib"])
    from google_auth_oauthlib.flow import InstalledAppFlow

SCOPES      = ["https://www.googleapis.com/auth/youtube.upload"]
CONSENT_URL = "https://console.cloud.google.com/apis/credentials/oauthclient"

def main():
    print()
    print("=" * 65)
    print("  YouTube One-Time OAuth Setup")
    print("=" * 65)
    print()
    print("This opens your browser to create an OAuth 2.0 client in the")
    print("same Google Cloud project that owns your YouTube API key.")
    print()
    print("STEPS (takes about 2 minutes):")
    print()
    print("  1. Your browser will open to Google Cloud Console.")
    print("     Make sure you're signed in as the YouTube channel owner.")
    print()
    print("  2. If prompted, pick the project that has your YouTube API key.")
    print()
    print("  3. Under 'Application type', choose:  Desktop app")
    print()
    print("  4. Name it anything, e.g.:  CarFacts YouTube Uploader")
    print()
    print("  5. Click CREATE.")
    print()
    print("  6. A dialog shows your Client ID and Client Secret.")
    print("     Copy both and paste them below.")
    print()
    print("-" * 65)
    print("  If you have a Desktop app client, use that one.")
    print("  (Web app clients won't work for offline refresh tokens)")
    print("-" * 65)
    client_id     = input("  Paste Client ID:     ").strip()
    client_secret = input("  Paste Client Secret: ").strip()
    print("-" * 65)

    if not client_id or not client_secret:
        print("\nERROR: Both values are required.")
        sys.exit(1)

    client_config = {
        "installed": {
            "client_id":     client_id,
            "client_secret": client_secret,
            "auth_uri":      "https://accounts.google.com/o/oauth2/auth",
            "token_uri":     "https://oauth2.googleapis.com/token",
            "redirect_uris": ["urn:ietf:wg:oauth:2.0:oob", "http://localhost"],
        }
    }

    print()
    flow = InstalledAppFlow.from_client_config(client_config, SCOPES)

    print("Copy this URL and open it in your YouTube account's browser:")
    print()
    # run_local_server with open_browser=False prints the URL without auto-opening
    creds = flow.run_local_server(port=8080, open_browser=False, prompt="consent", access_type="offline")
    print()

    output = {
        "client_id":     client_id,
        "client_secret": client_secret,
        "refresh_token": creds.refresh_token,
    }

    with open("youtube-creds.json", "w") as f:
        json.dump(output, f, indent=2)

    print()
    print("=" * 65)
    print("  SUCCESS!")
    print("=" * 65)
    print()
    print("  Saved to youtube-creds.json")
    print()
    print("  Now run:  python store-youtube-secrets.py")
    print("  That stores everything in Key Vault and you're done.")
    print()

if __name__ == "__main__":
    main()
