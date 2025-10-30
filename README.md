Client–Server Network (Custom UDP DNS)

This project is a **custom DNS client–server system** built in **C# (.NET)** using **UDP sockets**.  
This assignment was made for my Networking class, which focuses on implementing a simplified DNS protocol using raw socket communication.

The application consists of two console programs:
- **Client** → Sends DNS lookup requests
- **Server** → Receives, processes, and responds to those lookups

---

## How It Works

The client and server communicate via **UDP** using `Socket.SendTo()` and `Socket.ReceiveFrom()`.

Each message is formatted as a **JSON object** that includes a message ID, type, and content.

The communication flow:
1. **Handshake** – The client sends a “Hello” message and receives a “Welcome” from the server.
2. **DNS Lookup** – The client sends lookup requests containing a record type and domain name.
3. **Reply / Error** – The server checks the data source (a JSON-based DNS list) and sends either:
   - A valid record (`DNSLookupReply`)
   - Or an error message (`Error`)
4. **Acknowledgment** – The client confirms each reply with an `Ack` message.
5. **End** – Once all requests are complete, the server sends an `End` message to close communication.

The server stays active after completion, waiting for the next client.
