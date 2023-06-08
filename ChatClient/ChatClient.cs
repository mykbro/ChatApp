using ChatMiscellaneous;
using SerializationLibrary;
using SerializationLibrary.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Security;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace ChatClient
{
    internal class ChatClient
    {        
        private Socket? _connectedSocket;
        private SocketObjectWriter<ChatMessage>? _socketWriter;
        private SocketObjectReader<ChatMessage>? _socketReader;        
        //private bool _connecting;       
        private readonly IPAddress _serverIp;
        private readonly int _serverPort;
        private readonly string _id;
        //private readonly Queue<ChatMessage> _receivedMessagesQueue;
        //private readonly ConcurrentQueue<ChatMessage> _sentMessagesQueue;        
        private readonly int _queueSize;
        private readonly SerializationMode _serializationMode;
        
        public ChatClient(IPAddress serverAddress, int portNr, string id, int queueSize, SerializationMode serializationMode)
        {
            _connectedSocket = null;
            _socketWriter = null;
            _socketReader = null;
            _serverIp = serverAddress;
            _serverPort = portNr;
            _id = id;
            //_receivedMessagesQueue = new Queue<ChatMessage>(queueSize);        //we keep the last X messages            
            //_sentMessagesQueue = new ConcurrentQueue<ChatMessage>();
            _queueSize = queueSize;
            _serializationMode = serializationMode;
        }

        public bool Connected
        {
            get { return _connectedSocket != null; }
        }

        public async Task<bool> ConnectAsync(int maxConnAttempts, int connTimeoutInSeconds, CancellationToken cancellationToken = default)
        {
            int connTimeoutInMillis = connTimeoutInSeconds * 1000;
            bool maxConnectAttemptsReached = false;
            int failedConnAttempts = 0;
            Socket? candidateSocket = null;

            //if we're already connected we do not proceed
            if (Connected)
                throw new ClientAlreadyConnectedException();

            //we try to connect until cancellation or attempts exceeded                               
            while (candidateSocket == null || (!candidateSocket.Connected && !cancellationToken.IsCancellationRequested && !maxConnectAttemptsReached))
            {
                //Console.WriteLine("Connecting... ({0})", failedConnAttempts + 1);
                candidateSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
               
                using (Timer stopConnectionTimer = new Timer(CloseSocket, candidateSocket, connTimeoutInMillis, Timeout.Infinite))
                {
                    try
                    {
                        await candidateSocket.ConnectAsync(_serverIp, _serverPort, cancellationToken);
                    }
                    catch (SocketException ex)
                    {
                        candidateSocket.Close();    //probably redundant as a cancelled Connect will close the Socket
                        failedConnAttempts++;
                        if (failedConnAttempts >= maxConnAttempts)
                            maxConnectAttemptsReached = true;
                    }
                }
                
            }

            if (candidateSocket.Connected)
            {
                _connectedSocket = candidateSocket;
                _socketWriter = new SocketObjectWriter<ChatMessage>(_connectedSocket, _serializationMode, false);
                _socketReader = new SocketObjectReader<ChatMessage>(_connectedSocket, _serializationMode, false);                
                //Console.WriteLine(_id + " connected !");
            }
            //else
                //Console.WriteLine(_id + " connection failed !");

            return Connected;
        }

        public bool Disconnect()
        {
            bool toReturn;
            
            if (Connected)
            {
                _connectedSocket.Close();
                _connectedSocket = null;
                _socketWriter = null;
                _socketReader = null;
                toReturn = true;
            }
            else
                toReturn = false;

            return toReturn;    
        }

        public async Task CreateTrafficAsync(int numberOfLogins, int avgNumberOfMsgsPerLogin, int avgDelayBetweenLogins, int avgDelayBetweenMsgs, int numPeers)
        {
            ChatMessage loginMessage = new LoginRequest(_id);
            ChatMessage logoutMessage = new LogoutRequest(_id);
            
            ChatMessage msgForPeer;
            string recipientId;
            string msg;
            int numberOfMsgsToSend;
            bool connectionOk = true;

            //for (int i = 0; i < numberOfLogins; i++)
            while(connectionOk)
            {
                try
                {
                    await Task.Delay(RandomNumberGenerator.GetInt32(avgDelayBetweenLogins * 2));
                    await _socketWriter.WriteObjectAsync(loginMessage);
                    //_sentMessagesQueue.Enqueue(loginMessage);
                    //Console.WriteLine(_id + " logging in...");
                    //receivedMessage = await _socketReader.ReadObjectAsync();                
                    //Console.WriteLine(_id + " logged in !");
                    numberOfMsgsToSend = RandomNumberGenerator.GetInt32(avgNumberOfMsgsPerLogin * 2);
                    for (int j = 0; j < numberOfMsgsToSend; j++)
                    {
                        await Task.Delay(RandomNumberGenerator.GetInt32(avgDelayBetweenMsgs * 2));
                        recipientId = RandomNumberGenerator.GetInt32(numPeers).ToString();
                        msg = String.Format("Hi {0} ! I'm {1}... how are you ?", recipientId, _id);
                        msgForPeer = new MessageFromUserToUser(_id, recipientId, msg);
                        await _socketWriter.WriteObjectAsync(msgForPeer);
                        //_sentMessagesQueue.Enqueue(msgForPeer);
                        //Console.WriteLine(msg);
                    }
                    await _socketWriter.WriteObjectAsync(logoutMessage);
                    //_sentMessagesQueue.Enqueue(logoutMessage);
                    //Console.WriteLine(_id + " logging out...");
                    //receivedMessage = await _socketReader.ReadObjectAsync();
                    //Console.WriteLine(_id + " logged out !");
                }
                catch(Exception ex)
                {
                    connectionOk = false;
                }
            }

            Console.WriteLine(_id + " connection lost !");
        }

        public async Task CreateRandomLoginLogoutTrafficAsync(int numberOfLogins, int avgNumberOfMsgsPerLogin, int avgDelayBetweenLogins, int avgDelayBetweenMsgs, int numPeers)
        {
            ChatMessage loginMessage;
            ChatMessage logoutMessage;

            ChatMessage msgForPeer;
            string loginId;            
            string recipientId;
            string msg;
            int numberOfMsgsToSend;
            bool connectionOk = true;

            //for (int i = 0; i < numberOfLogins; i++)
            while (connectionOk)
            {
                try
                {
                    await Task.Delay(RandomNumberGenerator.GetInt32(avgDelayBetweenLogins * 2));
                    loginId = RandomNumberGenerator.GetInt32(numPeers).ToString();  
                    loginMessage = new LoginRequest(loginId);
                    await _socketWriter.WriteObjectAsync(loginMessage);
                    //_sentMessagesQueue.Enqueue(loginMessage);
                    //Console.WriteLine(_id + " logging in...");
                    //receivedMessage = await _socketReader.ReadObjectAsync();                
                    //Console.WriteLine(_id + " logged in !");
                    numberOfMsgsToSend = RandomNumberGenerator.GetInt32(avgNumberOfMsgsPerLogin * 2);
                    for (int j = 0; j < numberOfMsgsToSend; j++)
                    {
                        await Task.Delay(RandomNumberGenerator.GetInt32(avgDelayBetweenMsgs * 2));
                        recipientId = RandomNumberGenerator.GetInt32(numPeers).ToString();
                        msg = String.Format("Hi {0} ! I'm {1}... how are you ?", recipientId, loginId);
                        msgForPeer = new MessageFromUserToUser(_id, recipientId, msg);
                        await _socketWriter.WriteObjectAsync(msgForPeer);
                        //_sentMessagesQueue.Enqueue(msgForPeer);
                        //Console.WriteLine(msg);
                    }
                    logoutMessage = new LogoutRequest(loginId);
                    await _socketWriter.WriteObjectAsync(logoutMessage);
                    //_sentMessagesQueue.Enqueue(logoutMessage);
                    //Console.WriteLine(_id + " logging out...");
                    //receivedMessage = await _socketReader.ReadObjectAsync();
                    //Console.WriteLine(_id + " logged out !");
                }
                catch (Exception ex)
                {
                    connectionOk = false;
                }
            }

            Console.WriteLine(_id + " connection lost !");
        }

        public async Task LogResponsesAsync()
        {
            ChatMessage receivedMessage;
            bool connectionOpen = true;

            while (connectionOpen)
            {
                try
                {
                    receivedMessage = await _socketReader.ReadObjectAsync();
                    //_receivedMessagesQueue.Enqueue(receivedMessage);
                    //if (_receivedMessagesQueue.Count > _queueSize)
                        //_receivedMessagesQueue.Dequeue();
                }
                catch(SocketException ex)
                {
                    connectionOpen = false;
                }
            }
            Console.WriteLine(_id + " connection error !");
        }

        public async Task LoginUserAsync(string username)
        {
            if (!Connected)
                throw new ClientNotConnectedException();

            await _socketWriter.WriteObjectAsync(new LoginRequest(username));
        }

        public async Task LogoutUserAsync(string username)
        {
            if (!Connected)
                throw new ClientNotConnectedException();

            await _socketWriter.WriteObjectAsync(new LogoutRequest(username));
        }

        public async Task SendMessageFromUserToUserAsync(string senderId, string recipientId, string msg)
        {
            if (!Connected)
                throw new ClientNotConnectedException();

            await _socketWriter.WriteObjectAsync(new MessageFromUserToUser(senderId, recipientId, msg));
        }

        public async Task<ChatMessage> ReceiveMessageAsync(CancellationToken ct = default)
        {
            if (!Connected)
                throw new ClientNotConnectedException();

            return await _socketReader.ReadObjectAsync(ct);
        }


        static private void CloseSocket(object socketToClose)
        {
            ((Socket)socketToClose).Close();
        }

    }

    public class ClientNotConnectedException : Exception { }
    public class ClientAlreadyConnectedException : Exception { }

}
