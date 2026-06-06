using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Threading;

public class Program
{
    static UdpClient UDPClient;
    static string Folder;
    static List<IPEndPoint> Peers = new List<IPEndPoint>();
    static Dictionary<string, string> Files = new Dictionary<string, string>();
    static Dictionary<string, IPEndPoint> Calls = new Dictionary<string, IPEndPoint>();
    static Dictionary<string, FileBuffer> FileBuffers = new Dictionary<string, FileBuffer>();
    static int Port = 2606;

    public class FileBuffer
    {
        public byte[] Data { get; set; }
        public int ReceivedBytes { get; set; }
        public int TotalSize { get; set; }
    }

    static string ComputeHash(byte[] fileData)
    {
        using (var sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(fileData);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
    }
    public static void Main(string[] args)
    {
        Console.WriteLine("Welcome to FDAP UwU (NOTHING IS SAVE ALL PEERS AND CALL IS ON CACHE)");
        UDPClient = new UdpClient(Port); Console.WriteLine($"UDP Client is running on port : {Port} >///<");
        Console.WriteLine($"Who you know UwU (separate with ',', or press Enter): ");
        string peersInput = Console.ReadLine();
        if (!string.IsNullOrEmpty(peersInput))
        {
            foreach (string ip in peersInput.Split(','))
            {
                Peers.Add(new IPEndPoint(IPAddress.Parse(ip.Trim()), Port));
            }
        }
        Console.WriteLine($"Where you stock the files OwO : ");
        string filesInput = Console.ReadLine();
        while (Directory.Exists(filesInput))
        {
            filesInput = Console.ReadLine();
        }
        Folder = filesInput;
        Thread thread = new Thread(SendUserRequest);
        Thread thread2 = new Thread(LoadFiles);
        thread.Start();
        thread2.Start();
        while (true)
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] receivedData = UDPClient.Receive(ref remoteEP); Console.WriteLine($"DATA !!!! from {remoteEP} OwO");
            string message = Encoding.UTF8.GetString(receivedData); Console.WriteLine($"We received {message} !!! UwU");
            if (message.Trim().Split(' ')[0] == "GET")
            {
                if (!Peers.Contains(remoteEP))
                {
                    Peers.Add(remoteEP);
                }

                bool wasfound = false;

                foreach (var filehash in Files.Keys)
                {
                    if (filehash.ToString() == message.Trim().Split(' ')[1])
                    {
                        string filePath = Files[filehash];
                        SendFiles(remoteEP, filePath);
                        wasfound = true;
                    }
                }

                if (!wasfound)
                {
                    string requestedHash = message.Trim().Split(' ')[1];
                    if (!Calls.ContainsKey(requestedHash))
                    {
                        Calls.Add(requestedHash, remoteEP);
                        foreach (IPEndPoint peer in Peers)
                        {
                            if (peer != remoteEP)
                            {
                                UDPClient.Send(receivedData, receivedData.Length, peer);
                                Console.WriteLine($"Relayed {requestedHash} to {peer} yah >_<");
                            }
                        }
                    }
                }
            }
            if (message.Trim().Split(' ')[0] == "FILE")
            {
                string hash = message.Trim().Split(' ')[1];
                int fileSize =  int.Parse(message.Trim().Split(' ')[2]);
                int PacketsNumber =  int.Parse(message.Trim().Split(' ')[3]);


                FileBuffers[hash] = new FileBuffer
                {
                    Data = new byte[fileSize],
                    ReceivedBytes = 0,
                    TotalSize = fileSize
                };
                Console.WriteLine($"Buffer initialized for {hash} (size: {fileSize}) OwO");
            }

            if (message.Trim().Split(' ')[0] != "GET" && message.Trim().Split(' ')[0] != "FILE")
            {
                int separatorIndex = Array.IndexOf(receivedData, (byte)':');
                if (separatorIndex > 0)
                {
                    string hash = Encoding.UTF8.GetString(receivedData, 0, separatorIndex);
                    int dataStart = separatorIndex + 1;
                    int dataLength = receivedData.Length - dataStart;
                    byte[] packetData = new byte[dataLength];
                    Array.Copy(receivedData, dataStart, packetData, 0, dataLength);

                    if (FileBuffers.TryGetValue(hash, out FileBuffer buffer))
                    {
                        Array.Copy(packetData, 0, buffer.Data, buffer.ReceivedBytes, packetData.Length);
                        buffer.ReceivedBytes += packetData.Length;

                        Console.WriteLine($"Packet for {hash} ({buffer.ReceivedBytes}/{buffer.TotalSize} bytes) >->");

                        if (buffer.ReceivedBytes == buffer.TotalSize)
                        {
                                string savePath = Path.Combine(Folder, $"{hash}.dat");
                                File.WriteAllBytes(savePath, buffer.Data);
                                FileBuffers.Remove(hash);
                                Console.WriteLine($"Files {hash} save in {savePath} !!!!!! OIIA");
                                if (Calls.ContainsKey(hash))
                                {
                                    SendFiles(Calls[hash], savePath);
                                    Calls.Remove(hash);
                                }
                        }
                    }
                }
            }
        }
    }

    public static void SendFiles(IPEndPoint ip, string file)
    {
        // Began with a packet with the hash/numberofpackets
        byte[] fileData = File.ReadAllBytes(file);
        string hash = ComputeHash(fileData);
        int totalPackets = (int)Math.Ceiling((double)fileData.Length / Port);

        UDPClient.Send(Encoding.UTF8.GetBytes($"FILE {hash} {fileData.Length} {totalPackets}"), ip);

        for (int i = 0; i < fileData.Length; i += Port)
        {
            int size = Math.Min(Port, fileData.Length - i);
            byte[] packet = new byte[size];
            byte[] signature = UTF8Encoding.UTF8.GetBytes(hash + ":");
            Array.Copy(fileData, i, packet, 0, size);
            byte[] finalPacket = signature.Concat(packet).ToArray();
            UDPClient.Send(finalPacket, finalPacket.Length, ip);
            Console.WriteLine($"We sent packet{i} on total {totalPackets} to {ip}! ^^");
        }

        Console.WriteLine($"We sent all packets UwU !!!!");
    }

    public static void LoadFiles()
    {
        if (Directory.Exists(Folder))
        {
            foreach (string path in Directory.EnumerateFiles(Folder))
            {
                byte[] fileData = File.ReadAllBytes(path);
                string hash = ComputeHash(fileData);
                if (!Files.ContainsKey(hash))
                {
                    Files.Add(hash, path);
                    Console.WriteLine($"File : {path} loaded OwO");
                }
            }
        }
        else
        {
            Console.WriteLine("Folder not found !!! =_=");
        }
    }
    public static void SendUserRequest()
    {
        Console.Write("UwU FDAP >>");
        string UserRequest = Console.ReadLine();

        if (UserRequest.Trim().Split(' ')[0] == "GET")
        {
            foreach (var ip in Peers)
            {
                UDPClient.Send(UTF8Encoding.UTF8.GetBytes($"GET {UserRequest.Trim().Split(' ')[1]}"),  ip);
                Console.WriteLine($"We sent {UserRequest} to {ip}! OwO");
            }
        }
    }
}
