"""Upload the local release key via an already authenticated GitHub CLI."""
import base64
import json
from pathlib import Path
import shutil
import subprocess
import sys


def main():
    if not shutil.which("gh"):
        sys.exit("GitHub CLI fehlt. Bitte gh installieren und mit gh auth login anmelden.")
    subprocess.run(["gh", "auth", "status", "--hostname", "github.com"], check=True)
    folder = Path(__file__).resolve().parents[1] / ".local" / "signing"
    config = json.loads((folder / "android-signing.json").read_text())
    values = {
        "ANDROID_KEYSTORE_BASE64": base64.b64encode((folder / "campermanagement.p12").read_bytes()).decode("ascii"),
        "ANDROID_KEY_ALIAS": config["alias"],
        "ANDROID_KEYSTORE_PASSWORD": config["store_password"],
        "ANDROID_KEY_PASSWORD": config["key_password"],
    }
    for name, value in values.items():
        subprocess.run(["gh", "secret", "set", name, "--repo", "SlDracon/CamperManagement"], input=value, text=True, check=True)
        print(name + " hinterlegt.")


if __name__ == "__main__":
    main()
