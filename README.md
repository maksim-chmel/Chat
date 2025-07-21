Anonymous Encrypted P2P Chat – User Manual
==========================================

What is this?
-------------
This is a secure peer-to-peer (P2P) chat app built in C#. It uses RSA + AES encryption 
to protect your messages. No internet or servers are needed — just a local network (LAN).

You can chat anonymously and securely between two computers.

How to Use
----------

1. Build or Download
--------------------
- To build:
  dotnet publish -c Release -r win-x64 --self-contained true

  (You can change `win-x64` to `linux-x64`, `osx-arm64`, etc.)

- Or download the release version from GitHub.

2. Start the App
----------------

On Device 1 (Host):
- Run the app: `Chat.exe` (or `./Chat` on Linux/macOS)
- Press `h` and Enter to host
- The app will show your local IP (e.g. 192.168.0.5)
- Wait for the second user to connect

On Device 2 (Client):
- Run the app: `Chat.exe` (or `./Chat`)
- Press `c` and Enter to connect
- Enter the host’s IP (shown on first device)

3. Chat
-------
Once connected, you can start chatting securely.

All messages are:
- Encrypted using AES
- AES key is securely exchanged using RSA
- Private — no one else on the network can read your messages

Available Commands
------------------
/q      Quit the chat
/help   Show command list
/clear  Clear the screen

Requirements
------------
- Both devices must be on the same LAN (e.g. Wi-Fi)
- Port 9000 must be open (or you can change it in code)
- .NET 8 required to build from source

Enjoy safe chatting!
