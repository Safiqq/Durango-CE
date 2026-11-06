Durango: Community Edition (v1.0.0-CE)
========================================

How to Play
  1. Extract this full zip package into a directory where you have write permissions.
  2. Launch Durango.exe directly to play offline single-player (the embedded local gateway starts automatically).
  3. Or launch DurangoLauncher.exe to connect to a community server from servers.json.

Configuration (offserver.txt)
  Client network configuration:
    gateway=http://127.0.0.1:8190 -> target server gateway
    name=Local Server

Account (Account Number)
  First time entering the game: The server automatically creates a unique account for you.
  You can check your Account Number in-game under Settings -> Account -> Account Number.
  Migrating to another machine: Open offserver.txt on the new machine, add `account=<Your Account Number>`,
  and launch the game. Your existing character will load immediately.
  WARNING: Never share your Account Number with anyone.

Troubleshooting
  * Game does not start: Check player.log in the game folder.
  * Windows SmartScreen warning: Click More info -> Run anyway (for community builds).
  * Cannot connect / stuck loading: Ensure offserver.txt points to a running gateway or 127.0.0.1.
