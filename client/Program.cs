using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LibData;

class Program
{
    static void Main(string[] args)
    {
        ClientUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

class ClientUDP
{
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);
    
    public static void start()
    {
        try{
            byte[] buffer  = new byte[1024];
            Socket socket;
            try{
                 socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);  
            }
            catch(Exception e){
                Console.WriteLine("Socket creation failed:" + e.Message);
                return;
            }

            EndPoint endPoint =  new IPEndPoint(IPAddress.Parse(setting.ClientIPAddress), setting.ClientPortNumber);
            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            try{
                Message helloMessage = new Message{MsgId = 1, MsgType = MessageType.Hello, Content = $"{MessageType.Hello} from client"};
                SendMessage(helloMessage, socket, endPoint);
                ReceiveMessage(socket, remoteEndPoint, MessageType.Welcome);

                HandleDnsMethods(socket, endPoint, remoteEndPoint);
                ReceiveMessage(socket, remoteEndPoint, MessageType.End);
                    
                socket.Close();               
            }
            catch(Exception e){
                Console.WriteLine("Connection error: " + e);
            }           
        }
        catch (Exception e){
            Console.WriteLine("Socket error: " + e.Message);
        }
    }   

    private static void HandleDnsMethods(Socket socket, EndPoint endPoint, EndPoint remoteEndPoint){
        Message firstMessage = new Message{MsgId = 33, MsgType = MessageType.DNSLookup, Content = new DNSRecord{Type = "A", Name = "www.outlook.com"}};
        SendMessage(firstMessage, socket, endPoint);
        ReceiveMessage(socket, remoteEndPoint, MessageType.DNSLookupReply);               
        SendMessage(new Message{ MsgId = firstMessage.MsgId, MsgType = MessageType.Ack, Content = firstMessage.MsgId}, socket, endPoint);
                        
        Message secondMessage = new Message{MsgId = 44, MsgType = MessageType.DNSLookup, Content = new DNSRecord{Type = "MX", Name = "example.com"}};
        SendMessage(secondMessage, socket, endPoint);
        ReceiveMessage(socket, remoteEndPoint, MessageType.DNSLookupReply);
        SendMessage(new Message{ MsgId = secondMessage.MsgId, MsgType = MessageType.Ack, Content = secondMessage.MsgId}, socket, endPoint);
                        
        Message thirdMessage= new Message{MsgId = 555, MsgType = MessageType.DNSLookup, Content = new DNSRecord{Type = "A", Name = "example.com"}};
        SendMessage(thirdMessage, socket, endPoint);
        ReceiveMessage(socket, remoteEndPoint, MessageType.Error);                                         

        Message fourthMessage= new Message{MsgId = 666, MsgType = MessageType.DNSLookup, Content = new DNSRecord{Type = "MX", Name = "www.outlook.com"}};
        SendMessage(fourthMessage, socket, endPoint);
        ReceiveMessage(socket, remoteEndPoint, MessageType.Error);
    }

    private static void SendMessage(Message message, Socket socket, EndPoint endPoint){
        string jsonMessage = JsonSerializer.Serialize(message);
        byte[] byteMessage = Encoding.ASCII.GetBytes(jsonMessage);
        socket.SendTo(byteMessage, endPoint);

        if(message.Content != null){
            Console.WriteLine($"Message sent: {message.MsgId}. {message.MsgType} -> {message.Content}");
        }
        else{
            Console.WriteLine($"Message sent: {message.MsgId}. {message.MsgType}");
        }
    }

    private static void ReceiveMessage(Socket socket, EndPoint endPoint, MessageType expectedType){
        byte[] buffer = new byte[1024];
        int byteMessage = socket.ReceiveFrom(buffer, ref endPoint);
        string stringMessage = Encoding.ASCII.GetString(buffer, 0, byteMessage);

        try{
            Message receivedMessage = JsonSerializer.Deserialize<Message>(stringMessage);
            if(receivedMessage.MsgType == MessageType.Error){
                Console.WriteLine($"Error received: {receivedMessage.MsgId}. {receivedMessage.MsgType} -> {receivedMessage.Content}");
                
                if(receivedMessage.Content.ToString().Contains("Hello Message not received")){
                   HandleMissingHello(socket, endPoint);
                }
            }
            else if(receivedMessage.MsgType != expectedType){
                throw new Exception($"Wrong type: expected {expectedType}, but received {receivedMessage.MsgType}");
            }
            else{
                if(receivedMessage.MsgType == MessageType.End) {
                    Console.WriteLine($"Message received: {receivedMessage.MsgId}. {receivedMessage.MsgType}");         
                }
                else{
                    Console.WriteLine($"Message received: {receivedMessage.MsgId}. {receivedMessage.MsgType} -> {receivedMessage.Content}");
                }                          
            }
        }
        catch(Exception e){
            Console.WriteLine("Received Message could not be deserialized: " + e);
            return;
        }
    }

    private static void HandleMissingHello(Socket socket, EndPoint endPoint)
    {
        Console.WriteLine("Error: The server requires a hello message first. Resending Hello:");
        SendMessage(new Message { MsgId = 1, MsgType = MessageType.Hello, Content = "Hello from client" }, socket, endPoint);
        ReceiveMessage(socket, endPoint, MessageType.Welcome);
    }
}
