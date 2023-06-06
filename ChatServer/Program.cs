using System.Net;
using System.Net.Sockets;
using ChatMiscellaneous;
using System.Security;
using SerializationLibrary;
using SerializationLibrary.Sockets;
using SerializationLibrary.Exceptions;
using System.Security.Cryptography;
using System;
using System.Runtime.Serialization;
using System.Collections.Concurrent;
using SpinLock = AsyncStuff.SpinLock;
using AsyncStuff;


namespace ChatServer
{
    internal class Program
    {    
       
        //static private readonly SpinLock _loggedUsersLock = new SpinLock();
        //static private readonly ConcurrentDictionary<string, BlockingQueue<ChatMessage>> _loggedUsersQueues = new ConcurrentDictionary<string, BlockingQueue<ChatMessage>>();
        static private readonly AsyncReadWriteLock _mainReadWriteLock = new AsyncReadWriteLock();        
        
        //this Dictionary will keep trace of all the writers associated with a logged user... used when sending message from one user to another and on login/logout requests
        static private readonly Dictionary<string, HashSet<ConcurrentSocketObjectWriter<ChatMessage>>> _loggedUsersSocketWriters = new Dictionary<string, HashSet<ConcurrentSocketObjectWriter<ChatMessage>>>();
        //this Dictionary will keep trace of the loggedUsers associated with a SocketWriter... used when a connection closes to see if we need to logout a user and to authorize requests
        static private readonly Dictionary<ConcurrentSocketObjectWriter<ChatMessage>, string> _socketsAssociatedUser = new Dictionary<ConcurrentSocketObjectWriter<ChatMessage>, string>();           
        
        static private int _numConnCreated = 0;
        static private int _numMessagesFlying = 0;
        static private int _totNumMessages = 0;
        static private int _secondsElapsed = 0;
        static private readonly SerializationMode _serializationMode = SerializationMode.Custom;


        static void Main(string[] args)
        {          
            
            CancellationTokenSource cts = new CancellationTokenSource();
            IPEndPoint listSocketEndPoint = new IPEndPoint(IPAddress.Any, 2812);

            Socket listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listeningSocket.Bind(listSocketEndPoint);
            listeningSocket.Listen();

            //we start listening for connections on another thread
            //Task.Run(() => ListenForNewConnectionsAsync(listeningSocket, cts.Token));
            _ = ListenForNewConnectionsAsync(listeningSocket, cts.Token);

            //Timer msgNumberInASecTimer = new Timer(ShowMessageThroughput, null, 1000, 1000);
            
            _ = ShowMessageThroughputEverySecAsync(cts.Token);           //the Timer above doesn't work in Release... dunno why :|           
           
            

            bool exit = false;          
            String cmd;

            while (!exit)
            {                
                cmd = Console.ReadLine();                

                if (cmd == "x")
                {
                    cts.Cancel();
                    exit = true;
                }                          
            }
            
            Console.WriteLine("Server stopped... Press ENTER to close");
            Console.ReadLine();
            
        }

        static async Task ListenForNewConnectionsAsync(Socket listeningSocket, CancellationToken cancellationToken=default)
        {
            //we listen for new connections until cancellation            
                       
            
            try
            {
                while (true)
                {
                    Socket acceptedConnSocket = await listeningSocket.AcceptAsync();   //we need to define the Socket var inside the loop to capture it for the lambda
                    //Task.Run(() => ManageIncomingConnectionAsync(acceptedConnSocket, 10, cancellationToken));
                    //new Thread(() => ManageIncomingConnectionAsync(acceptedConnSocket, cancellationToken)).Start();
                    _ = ManageIncomingConnectionAsync(acceptedConnSocket, 0, 0, cancellationToken);     //thr
                    _numConnCreated++;
                    Console.WriteLine("Connection #" + _numConnCreated + " created !");
                }
            }
            catch (OperationCanceledException e)
            {
                Console.WriteLine("Op canceled");
            }
            catch (SocketException e)
            {
                Console.WriteLine("Socket error");
            }
        }

        static async Task ManageIncomingConnectionAsync(Socket acceptedConnSocket, int receiveTimeoutInSeconds, int sendTimeoutInSeconds, CancellationToken cancellationToken = default)
        {
            //Console.WriteLine("Connection accepted !!");                    
            //Guid connGuid = Guid.NewGuid();     //we associate each connection with a Guid to manage disconnections and logouts

            var socketReader = new SocketObjectReader<ChatMessage>(acceptedConnSocket, _serializationMode, false);
            //we use the socketWriter address instead of a Guid to identify the connection... the default GetHash() helps us
            var socketWriter = new ConcurrentSocketObjectWriter<ChatMessage>(acceptedConnSocket, _serializationMode, false);

            //we start the reading task and await it to end
            await ManageConnectionIncomingMessagesAsync(socketReader, socketWriter, acceptedConnSocket, receiveTimeoutInSeconds, cancellationToken);  
           
            //we finally close the socket (probably redundant in many cases)
            //it's up to us to close it because we've became the owner 
            acceptedConnSocket.Close();
        }

        static async Task ManageConnectionIncomingMessagesAsync(SocketObjectReader<ChatMessage> socketReader, ConcurrentSocketObjectWriter<ChatMessage> socketWriter, Socket acceptedConnSocket, int timeoutInSeconds, CancellationToken cancellationToken = default)
        {
            int timeoutInMillis = timeoutInSeconds * 1000;
            bool opCancelled = false;
            bool socketClosed = false;
            bool connectionClosedByPeer = false;
            Timer? timeoutTimer = null;                    

            //we create a Timer if needed (timeout > 0)
            if (timeoutInMillis > 0)
                timeoutTimer = new Timer((obj) => ((Socket)obj).Close(), acceptedConnSocket, Timeout.Infinite, Timeout.Infinite);

            //we listen for incoming reads and manage them until a socket closed by timeout/error or by peer
            while (!opCancelled && !socketClosed && !connectionClosedByPeer)
            {
                //we reset the Timer if it exists
                if (timeoutTimer != null)
                    timeoutTimer.Change(timeoutInMillis, Timeout.Infinite);

                try
                {
                    ChatMessage? receivedChatMsg = await socketReader.ReadObjectAsync(cancellationToken);
                    ManageChatMessage(receivedChatMsg, socketWriter);
                    Interlocked.Increment(ref _numMessagesFlying);
                    Interlocked.Increment(ref _totNumMessages);
                }
                catch (OperationCanceledException ex)
                {
                    Console.WriteLine("Connection cancelled @ " + DateTime.Now.ToString());
                    opCancelled = true;
                }
                catch (SocketException ex)  //timeout (or connection reset from peer or any other error)
                {
                    Console.WriteLine("Connection timeout @ " + DateTime.Now.ToString());
                    socketClosed = true;
                }
                catch (ConnectionClosedByRemoteHostException ex)    //connection gracefully closed by client
                {
                    Console.WriteLine("Connection closed by peer @ " + DateTime.Now.ToString());
                    connectionClosedByPeer = true;
                }
                catch (SerializationException ex)   //we keep reading if we receive a "strange" message
                {
                    //here we probably need to tell the client of the badly formatted message
                    Console.WriteLine("Serialization error @ " + DateTime.Now.ToString());
                }
            }

            //we check if through this connection we were logged, update the Dictionary and in case logout the user
            ManageClosedConnection(socketWriter);

            //we dispose the Timer (if used)
            if (timeoutTimer != null)
                timeoutTimer.Dispose();
        }

        static private void ManageChatMessage(ChatMessage? msg, ConcurrentSocketObjectWriter<ChatMessage> socketWriter)    
        {            
            if(msg != null)
            {
                Type msgType =msg.GetType();

                if (msgType == typeof(LoginRequest))
                {                   
                    string userName = ((LoginRequest)msg).UserName;
                    bool loginOk = TryLoginUser(userName, socketWriter);
                    
                    
                    if (loginOk)
                    {
                        socketWriter.WriteObject(LoginOk.GetValue());                       
                    }
                    else
                    {
                        socketWriter.WriteObject(LoginFailed.GetValue());
                    }
                    
                    
                }
                else if (msgType == typeof(MessageFromUserToUser))
                {
                    MessageFromUserToUser castMsg = msg as MessageFromUserToUser;
                    SendMessageResult sendMessageResult = SendMessageToUserFromUser(castMsg, socketWriter);

                    
                    if(sendMessageResult == SendMessageResult.Sent)
                        socketWriter.WriteObject(SendMessageOk.GetValue());      //actually the recipient could have logged out during the execution of the previous method 
                    else if(sendMessageResult == SendMessageResult.SenderNotLogged)
                        socketWriter.WriteObject(SendMessageFailedSenderNotLogged.GetValue());                    
                    else if(sendMessageResult == SendMessageResult.RecipientNotLogged)
                        socketWriter.WriteObject(SendMessageFailedRecipientNotLogged.GetValue());                    
                    
                }
                else if(msgType == typeof(LogoutRequest))
                {
                    LogoutRequest castMsg = msg as LogoutRequest;
                    bool logoutOk = TryLogoutUser(castMsg.UserName, socketWriter);    
                    
                    if (logoutOk)   
                    {                        
                        socketWriter.WriteObject(LogoutOk.GetValue());
                    }
                    else
                    {
                        socketWriter.WriteObject(LogoutFailed.GetValue());
                    }
                    
                }
            }
        }

        //in this version we use multiple socket for a login but up to one login for a Socket            
        static private bool TryLoginUser(string username, ConcurrentSocketObjectWriter<ChatMessage> socketWriter)
        {
            HashSet<ConcurrentSocketObjectWriter<ChatMessage>>? userSockets;
            bool firstLogin;        //with multiple logins we need to check if this was the first, to send LoginEvents

            _mainReadWriteLock.TryAcquireWriteLock();

            bool connNotAssociated = _socketsAssociatedUser.TryAdd(socketWriter, username);              //we check if there already was a login for this connection
            
            if (connNotAssociated)
            {                
                bool userAlreadyLogged = _loggedUsersSocketWriters.TryGetValue(username, out userSockets);
                
                if(userAlreadyLogged) 
                {
                    userSockets!.Add(socketWriter);      //if we're here it's not null
                    firstLogin = false;
                }
                else
                {
                    userSockets = new HashSet<ConcurrentSocketObjectWriter<ChatMessage>>() { socketWriter };
                    _loggedUsersSocketWriters.Add(username, userSockets);
                    firstLogin = true;
                }         
            }   
            else
            {                
                firstLogin = false;
            }

            
            _mainReadWriteLock.ReleaseWriteLock();

            /*
            //if the login went fine and was the first login, we tell every logged user (except the one who just logged) the news !            
            //should we made all of this method atomic and not release the write lock and try to acquire another read lock ?
            if (firstLogin)          
            {
                BroadcastLoginEvent(username);
            }
            */
            

            return connNotAssociated;
        }

        

        static private bool TryLogoutUser(string username, ConcurrentSocketObjectWriter<ChatMessage> socketWriter)
        {
            //we can logout a connection linked to a user only if the request came from one of the associated connection (and we'll logout THAT connection as we can have multiple connections)

            HashSet<ConcurrentSocketObjectWriter<ChatMessage>>? userSockets;
            string? socketAssociatedUser;            
            bool lastLogout;    //with multiple logouts we need to check if this was the last, to send LogoutEvents

            _mainReadWriteLock.TryAcquireWriteLock();
            bool connWasAssociated = _socketsAssociatedUser.Remove(socketWriter, out socketAssociatedUser);
            //we also check that if we found an associated user it matches the one we are loggin out
            bool canLogout = connWasAssociated && (socketAssociatedUser == username);     //we use the left-to-right operand evaluation to assure that socketAssociatedUser isn't null

            if (canLogout)
            {
                _loggedUsersSocketWriters.TryGetValue(username, out userSockets);   //if we're here there is an entry
                userSockets!.Remove(socketWriter);                                   //if we're here userSockets contains this socketWriter
                if(userSockets.Count == 0)
                {
                    //if we're here we delete the whole entry
                    _loggedUsersSocketWriters.Remove(username);
                    lastLogout = true;
                }
                else
                {
                    lastLogout = false;
                }
               
            }
            else
            { 
                lastLogout = false;
            }
            _mainReadWriteLock.ReleaseWriteLock();

            /*
            //if last logout occured we tell every remaining logged user.. here, outside the lock, a new login of the same user could already have occured
            if (lastLogout)
            {
                BroadcastLogoutEvent(username);
            }
            */
            

            return canLogout;
        }

        //right now a user can send msgs to itself
        static private SendMessageResult SendMessageToUserFromUser(MessageFromUserToUser msg, ConcurrentSocketObjectWriter<ChatMessage> socketWriter)
        {

            HashSet<ConcurrentSocketObjectWriter<ChatMessage>>? recipientWriters;
            string? socketAssociatedUser; 
            SendMessageResult toReturn;            

            _mainReadWriteLock.TryAcquireReadLock();
            bool connAssociated = _socketsAssociatedUser.TryGetValue(socketWriter, out socketAssociatedUser);    //we check if the connection we're using has a logged user...   
            if (connAssociated && socketAssociatedUser == msg.Sender)                                       //...we also check if that user matches the Sender
            {
                bool recipientLogged = _loggedUsersSocketWriters.TryGetValue(msg.Recipient, out recipientWriters);
                if (recipientLogged)
                {                    
                    foreach(ConcurrentSocketObjectWriter<ChatMessage> recipientWriter in recipientWriters)
                    {
                        try
                        {
                            recipientWriter.WriteObject(msg);            //we send the same message without instantiating a new one
                        }
                        catch
                        {
                            //we ignore the fail but we need to catch here otherwise we never release the ReadLock (we can also do it differently and release it in a finally block)
                        }                        
                    }                        
                    toReturn = SendMessageResult.Sent;
                }
                else 
                {
                    toReturn = SendMessageResult.RecipientNotLogged;
                }               
            }
            else
            {
                toReturn = SendMessageResult.SenderNotLogged;
            }
            _mainReadWriteLock.TryReleaseReadLock();

            return toReturn;
        }

        static private void ManageClosedConnection(ConcurrentSocketObjectWriter<ChatMessage> socketWriter)
        {
            string? loggedUserForThisConnection;

            //we prefer a split check-update approach that can take up to 2 lock because:
            //1- we avoid a WriteLock if the connection had no user logged
            //2- we can use TryLogout() without re-entrancy
            _mainReadWriteLock.TryAcquireReadLock();       
            bool connWasAssociated = _socketsAssociatedUser.TryGetValue(socketWriter, out loggedUserForThisConnection);
            _mainReadWriteLock.TryReleaseReadLock();
            
            //we don't care if the found user logout in another away in the meantime... the TryLogout will just fail
            if(connWasAssociated)
                TryLogoutUser(loggedUserForThisConnection, socketWriter);
            
        }

        static private void BroadcastLoginEvent(string username)
        {
            ChatMessage loginMsg = new LoginEvent(username);

            _mainReadWriteLock.TryAcquireReadLock();
            foreach (var kvPair in _loggedUsersSocketWriters)       //for each user we get the KV pair
            {
                if (kvPair.Key != username)                         //if the user != logged user, we send to each of its socketWriter the msg 
                {
                    foreach(var socketWriter in kvPair.Value)
                    {
                        socketWriter.WriteObject(loginMsg);
                    }
                }                   
            }
            _mainReadWriteLock.TryReleaseReadLock();
        }

        static private void BroadcastLogoutEvent(string username)
        {
            ChatMessage logoutMsg = new LogoutEvent(username);
            
            _mainReadWriteLock.TryAcquireReadLock();            
            foreach (var loggedUserSocketWriters in _loggedUsersSocketWriters.Values)       //for each user we get its socketWriters
            {
                foreach (var socketWriter in loggedUserSocketWriters)                       //and we send the msg to each socketWriter
                { 
                    socketWriter.WriteObject(logoutMsg);
                }
            }
            _mainReadWriteLock.TryReleaseReadLock();
        }

        static private void ShowMessageThroughput(object _)
        {
            int avg;

            if (_totNumMessages != 0)
                _secondsElapsed++;
            if (_secondsElapsed > 0)
                avg = _totNumMessages / _secondsElapsed;
            else
                avg = 0;

            Console.WriteLine("{0} msg/sec | avg: {2} msg/sec ({1} logged users | {3} logged connections )", _numMessagesFlying, _loggedUsersSocketWriters.Count, avg, _socketsAssociatedUser.Count);
            
            Interlocked.Exchange(ref _numMessagesFlying, 0);    //reset to 0
        }

        static private async Task ShowMessageThroughputEverySecAsync(CancellationToken cancToken)
        {
            object o = new object();
            while (true)
            {
                await Task.Delay(1000, cancToken);               
                ShowMessageThroughput(o);
            }
            
        }
    }

    internal enum SendMessageResult { Sent, SenderNotLogged, RecipientNotLogged, SenderEqualsRecipient }
}

