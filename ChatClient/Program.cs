using System.Net.Sockets;
using System.Net;
using System.Security.Cryptography;
using ChatMiscellaneous;
using SerializationLibrary;
using SerializationLibrary.Sockets;
using System.Threading;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Serialization;

namespace ChatClient
{
        
    internal class Program
    {       
        //static private Socket? _connectionSocket = null;
        //static private SocketObjectWriter<ChatMessage>? _socketWriter = null;
        //static private SocketObjectReader<ChatMessage>? _socketReader = null;
        static private int _numConnectionsSpawned = 0;
        static private bool _connecting = false;
        static CancellationTokenSource? _connectionCancellationSource;

        static private readonly SerializationMode _serializationMode = SerializationMode.Custom;
        static private readonly int _serverPort = 2812;
        static private readonly IPAddress _serverIp = new IPAddress(new byte[] { 192, 168, 1, 25 });
        //static private IPAddress _serverIp = new IPAddress(new byte[] { 192, 168, 204, 128 });
        //static private IPAddress _serverIp = IPAddress.Loopback;


        //static SemaphoreSlim _numConnectionsSpawnedSemaphore = new SemaphoreSlim(1);
        /*
        static ChatClientProgram()
        {
            var temp = new List<ChatMessage>();
            for (int i = 0; i < 100000; i++)
                temp.Add(new LoginRequest(i.ToString()));

            _msgListToShare = new MessageList(temp);
        }
        */
       
       
        static void Main(string[] args)
        {
            //TestConnectionsWithRetries();
            SpawnMultipleConnectionsMain(args, 10000); 
            //ManageUserCommands();            
        }

        static void TestConnectionsWithRetries()
        {
            CancellationTokenSource cts = new CancellationTokenSource(); 

            bool exit = false;
            bool serverIpIsGood = false;
            int numConnToSpawn = 0;
            string cmd;
            string serverIpString; //= "192.168.204.128";   

            /*
            while (!serverIpIsGood)
            {
                Console.Write("Server IP: ");
                serverIpString = Console.ReadLine();
                serverIpIsGood = IPAddress.TryParse(serverIpString, out serverIp);
                if (!serverIpIsGood)
                    Console.WriteLine("Bad Server IP !");
            }
            Console.WriteLine("IP is valid"); 
            */

            Timer t = new Timer((obj) => Console.WriteLine("Current connections: " + _numConnectionsSpawned), null, 4000, 4000);

            while (!exit)
            {                
                cmd = Console.ReadLine();

                if (cmd == "0")
                {
                    exit = true;
                    cts.Cancel();
                }
                else if (Int32.TryParse(cmd, out numConnToSpawn) && numConnToSpawn > 0)
                {
                    for (int i = 0; i < numConnToSpawn; i++)
                    {
                        //Thread.Sleep(20);
                        //Task.Run(() => SpawnNewConnectionAsync(serverIp, 5, 2, 10, cts.Token));
                        //new Thread(() => SpawnNewConnectionAsync()).Start();
                        SpawnNewConnectionAsync(_serverIp, _serverPort, 5, 2, 10, cts.Token);
                    }
                }
                else if(cmd == "x")
                {
                    cts.Cancel();
                    cts.Dispose();
                    cts = new CancellationTokenSource();
                }
            }
        }
        
        static void SpawnMultipleConnectionsMain(string[] args, int numClients)
        {
            /*
            Thread manageUserCommandsThread = new Thread(ManageUserCommands);          
            manageUserCommandsThread.Start();           
            manageUserCommandsThread.Join();
            */
            
            if(args.Length >= 1)
                _serverIp = IPAddress.Parse(args[0]);
            if (args.Length == 2)
                numClients = Int32.Parse(args[1]);

            ChatClient[] clients = new ChatClient[numClients];

            for (int i = 0; i < clients.Length; i++) 
            { 
                clients[i] = new ChatClient(_serverIp, _serverPort, i.ToString(), 100, _serializationMode);
                clients[i].ConnectAsync(3, 4).Wait();    //we wait synchronously for each client to connect
            }

            Console.WriteLine("Running...");

            for (int i = 0; i < clients.Length; i++)
            {
                if (clients[i].Connected) 
                {
                    _ = clients[i].LogResponsesAsync();
                }
            }

            for (int i = 0; i < clients.Length; i++)
            {
                if (clients[i].Connected)
                {
                    //clients[i].CreateTrafficAsync(0, 100, 1000, 100, numClients);                    
                    _ = clients[i].CreateRandomLoginLogoutTrafficAsync(0, 100, 1000, 100, numClients);                    
                }
            }

            string cmd = "";
            while(cmd != "x")
            {
                cmd = Console.ReadLine();
            }            
        }
        

        static void ManageUserCommands()
        {
            bool exit = false;
            string[] cmdTokens;
            string cmd; 
            ChatClient client = new ChatClient(_serverIp, _serverPort, "pippo", 0, _serializationMode);

            while (!exit)
            {                
                cmdTokens = Console.ReadLine().Split(' ', 4);   //max 4 splits for: msg <from> <to> <msg>
                cmd = cmdTokens[0];

                if (cmd.Equals("Exit", StringComparison.OrdinalIgnoreCase))
                {
                    exit = true;
                    if(client.Connected)
                        client.Disconnect();                   
                }
                else if (cmd.Equals("Connect", StringComparison.OrdinalIgnoreCase))
                {
                    _ = TryToConnectAsync(client);
                }
                else if (cmd.Equals("Disconnect", StringComparison.OrdinalIgnoreCase))
                {
                    bool disconnectionOk;
                    
                    disconnectionOk = client.Disconnect();
                    if (disconnectionOk)
                        Console.WriteLine("Disconnected !");
                    else
                        Console.WriteLine("Were not connected !");
                }
                else if (cmd.Equals("x", StringComparison.OrdinalIgnoreCase))
                {
                    if(Connecting)
                    {
                        Console.WriteLine("Connection aborted !");                       
                        _connectionCancellationSource!.Cancel();        //if we're connecting the CTS is not null
                    }                        
                }
                else if(cmd.Equals("Login", StringComparison.OrdinalIgnoreCase))
                {
                    if (client.Connected)
                    {                        
                        if(cmdTokens.Length == 2)
                        {  
                            try 
                            {
                                client.LoginUserAsync(cmdTokens[1]).Wait();
                            }
                            catch
                            {
                                Console.WriteLine("Connection error !");
                            }
                        }
                        else
                            Console.WriteLine("Syntax error !");
                    }
                    else
                        Console.WriteLine("Not connected !");
                }
                else if (cmd.Equals("Logout", StringComparison.OrdinalIgnoreCase))
                {
                    if (client.Connected)
                    {
                        if (cmdTokens.Length == 2)
                        {
                            try
                            {
                                client.LogoutUserAsync(cmdTokens[1]).Wait();
                            }
                            catch
                            {
                                Console.WriteLine("Connection error !");
                            }
                        }
                        else
                            Console.WriteLine("Syntax error !");
                    }
                    else
                        Console.WriteLine("Not connected !");
                }
                else if (cmd.Equals("Msg", StringComparison.OrdinalIgnoreCase))
                {
                    if (client.Connected)
                    {
                        if (cmdTokens.Length == 4)
                        { 
                            try
                            {
                                client.SendMessageFromUserToUserAsync(cmdTokens[1], cmdTokens[2], cmdTokens[3]).Wait();
                            }
                            catch
                            {
                                Console.WriteLine("Connection error !");
                            }
                        }
                        else
                            Console.WriteLine("Syntax error !");
                    }
                    else
                        Console.WriteLine("Not connected !");
                }


            }
        }

        static async Task ManageServerMessagesAsync(ChatClient client)
        {
            bool connectionOpen = true;
            ChatMessage receivedMessage;
            
            
            while (connectionOpen)
            {
                try
                {
                    receivedMessage = await client.ReceiveMessageAsync();
                    if (receivedMessage != null)
                    {
                        Type msgType = receivedMessage.GetType();

                        if (msgType == typeof(LoginOk))
                        {
                            Console.WriteLine("Login ok !");
                        }
                        else if (msgType == typeof(LoginEvent))
                        {
                            LoginEvent castMsg = (LoginEvent) receivedMessage;
                            Console.WriteLine("{0} logged in !", castMsg.UserName);
                        }
                        else if (msgType == typeof(SendMessageFailedSenderNotLogged))
                        {
                            Console.WriteLine("You're not logged !");
                        }
                        else if (msgType == typeof(LoginFailed))
                        {
                            Console.WriteLine("Login failed !");
                        }                        
                        else if (msgType == typeof(SendMessageFailedRecipientNotLogged))
                        {
                            Console.WriteLine("Recipient not logged !");
                        }
                        else if (msgType == typeof(LogoutOk))
                        {
                            Console.WriteLine("Logout ok !");
                        }
                        else if (msgType == typeof(LogoutFailed))
                        {
                            Console.WriteLine("Logout failed !");
                        }
                        else if (msgType == typeof(LogoutEvent))
                        {
                            LogoutEvent castMsg = (LogoutEvent) receivedMessage;
                            Console.WriteLine("{0} logged out !", castMsg.UserName);
                        }
                        else if (msgType == typeof(SendMessageOk))
                        {
                            Console.WriteLine("Message sent !");
                        }
                        else if (msgType == typeof(MessageFromUserToUser))
                        {
                            MessageFromUserToUser castMsg = (MessageFromUserToUser) receivedMessage;
                            Console.WriteLine("[{0}] {1}", castMsg.Sender, castMsg.Message);
                        }
                    }
                }
                catch(SocketException ex)       
                {
                    connectionOpen = false;            
                }
                
            }

            Console.WriteLine("Connection closed !");
        }

        static void CloseSocket(object targetSocket)        //delegate for Timer and CancellationTokenRegistration
        {            
            ((Socket) targetSocket).Close();
        }

        static bool Connecting
        {
            get { return _connecting; }
            set { _connecting = value; }
        }

        static async Task TryToConnectAsync(ChatClient client)
        {
            if (client.Connected)
            {
                Console.WriteLine("Client already connected !");
            }
            else
            {
                if (!Connecting)
                {
                    bool connResult;
                    
                    Connecting = true;
                    _connectionCancellationSource = new CancellationTokenSource();
                    try
                    {
                        connResult = await client.ConnectAsync(5, 5, _connectionCancellationSource.Token);
                        if (connResult)
                        {
                            Console.WriteLine("Connected !");
                            ManageServerMessagesAsync(client);
                        }
                        else
                        {
                            Console.WriteLine("Connection failed !");
                        }
                    }
                    catch
                    {
                        Console.WriteLine("Connection failed !");
                    }
                    _connectionCancellationSource.Dispose();
                    _connectionCancellationSource = null;
                    Connecting = false;
                }
                else
                    Console.WriteLine("Connection already in progress !");
            }
        }



        static async Task SpawnNewConnectionAsync(IPAddress serverIp, int serverPort, int maxConnAttempts, int connTimeoutInSeconds, int randomPeriodBetweenWritesInSeconds, CancellationToken cancellationToken = default)
        {
            //int numConn = RandomNumberGenerator.GetInt32(10000);            

            int connTimeoutInMillis = connTimeoutInSeconds * 1000; 
            bool maxConnectAttemptsReached = false;
            int failedConnAttempts = 0;
            Socket? newConnSocket = null;    //we cannot use a 'using' block because we have to re-create a Socket if we fail to connect    

            //update number of connections atomically            
            int numConn = Interlocked.Increment(ref _numConnectionsSpawned);   //Increment returns the updated value so this is ok         

            //we try to connect until cancellation or attempts exceeded                               
            while (newConnSocket==null || (!newConnSocket.Connected && !cancellationToken.IsCancellationRequested && !maxConnectAttemptsReached))
            {                
                using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    
                    cts.CancelAfter(connTimeoutInMillis);
                    Console.WriteLine("#" + numConn + " connecting...");
                    newConnSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                    try 
                    { 
                        await newConnSocket.ConnectAsync(serverIp, serverPort, cts.Token);                        
                    }
                    catch(OperationCanceledException ex)
                    {
                        newConnSocket.Close();    //probably redundant as a cancelled ConnectAsync will close the Socket
                        failedConnAttempts++;
                        if (failedConnAttempts >= maxConnAttempts)
                            maxConnectAttemptsReached = true;                        
                    }                       
                }                   
            }

            //if we're connected we proceed
            if (newConnSocket.Connected)
            {
                Console.WriteLine("#" + numConn + " connected !");
                ChatMessage msg = new LoginRequest("Hi from #" + numConn + " !");

                try
                {
                    await SendToServerUntilCancelledAsync(newConnSocket, msg,  randomPeriodBetweenWritesInSeconds, cancellationToken);
                }   
                catch (OperationCanceledException ex)
                {
                    Console.WriteLine("Bye from #" + numConn + " ! -cancelled");
                }
                catch (SocketException ex)
                {
                    Console.WriteLine("Bye from #" + numConn + " ! -connection closed");
                }

                newConnSocket.Close();
            }
            else    //if we're here we aborted or reached max conn attempts
            {
                Console.WriteLine("Connection aborted !");
            }

            //after we're done we update the number of conn atomically
            Interlocked.Decrement(ref _numConnectionsSpawned);
        }
    
        static async Task SendToServerUntilCancelledAsync(Socket s, ChatMessage msgToSend, int randomPeriodBetweenWritesInSeconds, CancellationToken cancellationToken = default)
        {            
            var msgWriter = new SocketObjectWriter<ChatMessage>(s, _serializationMode, false);           
            int randomPeriodBetweenWritesInMillis = randomPeriodBetweenWritesInSeconds * 1000;
            
            while(true)
            {
                await Task.Delay(RandomNumberGenerator.GetInt32(randomPeriodBetweenWritesInMillis), cancellationToken);
                await msgWriter.WriteObjectAsync(msgToSend, cancellationToken);               
            }   
        }

    }
}