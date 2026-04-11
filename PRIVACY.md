# Privacy Policy

**Applies to:** RetroC IDE (free) and RetroC AI IDE (paid)
**Last updated:** April 11, 2026

---

## Summary

RetroC AI IDE does **not** collect, store, or transmit any personal information to the developer or any third party. All processing happens locally on your device.

---

## Data We Do NOT Collect

- No telemetry or usage analytics
- No crash reports sent to the developer
- No source code, file names, or project data
- No AI prompts or chat history
- No account information
- No advertising IDs or tracking identifiers

---

## AI Features (RetroC AI IDE)

The AI assistant runs **100% locally** on your device using a downloaded language model (Phi-3 mini or a model you supply).

- Your source code and prompts are processed **only on your machine**.
- Nothing is sent to cloud AI services, APIs, or the developer's servers.
- The AI model is stored at `%LOCALAPPDATA%\RetroC-IDE\models\` on your device only.

---

## Network Connections

The application connects to the internet **only** for the following user-initiated, one-time actions:

| Action | Destination | Data sent |
|--------|-------------|-----------|
| Download AI model (first use) | `huggingface.co` (Microsoft's official repo) | None — download only |
| Download TCC compiler (first run) | GitHub Releases | None — download only |

No other network connections are made. Both downloads are one-time events and can be substituted with local files.

---

## Microsoft Store

If you purchased RetroC AI IDE through the Microsoft Store, Microsoft may collect purchase and account data under their own privacy policy: https://privacy.microsoft.com

The developer does not receive any personal data from Microsoft beyond what Microsoft's standard developer reporting provides (aggregate download counts, ratings).

---

## Data Storage

All app settings are stored locally at:
- `%LOCALAPPDATA%\TurboC-IDE\settings.json` — compiler settings, AI settings
- `%LOCALAPPDATA%\RetroC-IDE\models\` — AI model file (if downloaded)

No data is synchronized to the cloud by this application.

---

## Children

This application does not knowingly collect any data from anyone, including children under 13. The app has no online features that involve user data.

---

## Changes

If this policy changes, the updated version will be published with the app update and on the GitHub repository.

---

## Contact

For privacy questions, open an issue on the project's GitHub repository.
