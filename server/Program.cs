using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LibData;

class Program
{
    static void Main(string[] args)
    {
        ServerUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

class ServerUDP
{
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);
    static List<DNSRecord> recordList;
    static int correctDnsCounter = 0;
    static int incorrectDnsCounter = 0;
    static int counter  = 0;
    static DNSRecord data;

    public static void start()
    {
        try
        {
            recordList = ReadRecords("../Server/DNSrecords.json");

            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint iPEndPoint = new IPEndPoint(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);
            socket.Bind(iPEndPoint);

            Console.WriteLine("The server is running:...");

            byte[] buffer = new byte[1024];
            IPEndPoint client = new IPEndPoint(IPAddress.Any, 0);
            EndPoint remoteEndPoint = (EndPoint)client;
            
            while(true){ 
                try{
                    int receiveBytes = socket.ReceiveFrom(buffer, ref remoteEndPoint);
                    string receivedData = Encoding.ASCII.GetString(buffer, 0, receiveBytes);
                    Message message = JsonSerializer.Deserialize<Message>(receivedData);

                    Console.WriteLine($"Message received: {message.MsgId}. {message.MsgType} -> {message.Content}");
                    
                    if(message.MsgType != MessageType.Hello && counter == 0){
                        SendMessage(new Message{MsgId = message.MsgId, MsgType = MessageType.Error, Content = "Hello Message not received"}, socket, remoteEndPoint);
                    }
                    counter++;

                    if(message.MsgType == MessageType.Hello){
                        Message welcomeMessage = new Message{MsgId = 2, MsgType = MessageType.Welcome, Content = "Welcome from server"};
                        SendMessage(welcomeMessage, socket, remoteEndPoint);
                    }
                    else if(message.MsgType == MessageType.DNSLookup){                     
                        HandleReceivedDnsRecord(message, socket, remoteEndPoint);
                        EndMessage(socket,remoteEndPoint);
                    }                       
                }
                catch(Exception e){
                    Console.WriteLine("Message error:" + e.Message);
                    SendMessage(new Message{MsgId = 0, MsgType = MessageType.Error, Content = "Message error"}, socket, remoteEndPoint);
                }            
            }   
        }
        catch(Exception e){
            Console.WriteLine("Socket error: " + e.Message);
        }
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

    private static void HandleReceivedDnsRecord(Message message, Socket socket, EndPoint endPoint){
        if(message.Content == null){
            SendMessage(new Message{MsgId = 0, MsgType = MessageType.Error, Content = "No DNS Record found"}, socket, endPoint);
            incorrectDnsCounter++;
            return;
        }

        try{
            data = JsonSerializer.Deserialize<DNSRecord>(message.Content.ToString());
        }
        catch(Exception e){
            SendMessage(new Message {MsgId = 0, MsgType = MessageType.Error, Content = "Content could not be read: " + e}, socket, endPoint);
            incorrectDnsCounter++;
            return;         
        }

        if(string.IsNullOrEmpty(data.Type) && string.IsNullOrEmpty(data.Name)){
            string errorMessage = "Type and Name are null or empty";
            SendMessage(new Message {MsgId = 0, MsgType = MessageType.Error, Content = errorMessage}, socket, endPoint);
            incorrectDnsCounter++;
            return;
        }
        
        if(string.IsNullOrEmpty(data.Type) || string.IsNullOrEmpty(data.Name)){
            string errorMessage = string.IsNullOrEmpty(data.Type) ? "Type is null or empty" : "Name is null or empty";
            SendMessage(new Message {MsgId = 0, MsgType = MessageType.Error, Content = errorMessage}, socket, endPoint);
            incorrectDnsCounter++;
            return;
        }     
        CheckForDnsMatch(message, socket, endPoint);  
    }

    private static void CheckForDnsMatch(Message message, Socket socket, EndPoint endPoint){
        var findRecord = recordList.Find(x => x.Type == data.Type && x.Name == data.Name);
        if(findRecord != null){
            Message replyMessage = new Message {
                MsgId = message.MsgId, 
                MsgType = MessageType.DNSLookupReply,
                Content = findRecord
                };

            SendMessage(replyMessage, socket, endPoint);
            correctDnsCounter++;
        }
        else{
            Message errorMessage = new Message{
                MsgId = message.MsgId, 
                MsgType = MessageType.Error, 
                Content = "No match found for this DNSRecord"
                };
            SendMessage(errorMessage, socket, endPoint);
            incorrectDnsCounter++;
        }      
       
    }
    
    private static void EndMessage(Socket socket, EndPoint endPoint){
         if(incorrectDnsCounter+correctDnsCounter == 4){
            SendMessage(new Message{MsgType = MessageType.End}, socket, endPoint);  
            incorrectDnsCounter = 0;
            correctDnsCounter = 0;
            counter = 0;
        }
    }

    private static List<DNSRecord> ReadRecords(string path){
        if(!File.Exists(path)){
            Console.WriteLine("path was not found");
            return new List<DNSRecord>();
        }

        string json = File.ReadAllText(path);
        List<DNSRecord> recordList = JsonSerializer.Deserialize<List<DNSRecord>>(json);
        return recordList;
    }
}