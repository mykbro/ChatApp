# ChatApp

## Disclaimer
This codebase is made for self-teaching and educational purposes only.
Many features like input validation, object disposed checks, some exception handling, etc... are mostly missing.
As such this codebase cannot be considered production ready.

## What's this ?
This solution implements a simple chat application using async methods for managing each server connection, 
greatly reducing the need to continuosly spawn new threads and the resources needed to maintain them.

The server supports same user logins from multiple devices.
The client supports connection management and exception handling.

The solution consists of 3 projects:
* A server program
* A driving program which can be used as a chat client or as a load test
* A shared library consisting of all the exchangable strong typed ChatMessage(s)

## How does it work ?
This solution needs both AsyncStuff and SerializationLibrary (both repositories are publicly available from my github):
* For the server we had to use async concurrent constructs in order to manage its shared internal state. AsyncStuff contains AsyncReadWriteLock which is used here.
* The client and the server exchanges strong typed ChatMessage objects using SocketObjectWriter\<ChatMessage\> and SocketObjectReader\<ChatMessage\> which internally use an ObjectWriter\<ChatMessage\> and an ObjectReader\<ChatMessage\> respectevily.
All these classes can be found inside SerializationLibrary. Please check its documentation for more info.

ChatMessage is the base abstract class from which all the "real" chat messages should derive. It implements ICustomSerializable\<ChatMessage\> from SerializationLibrary to leverage our Custom serialization.

### How does the server works ?
The server works by spawning a Task where a Socket keeps waiting for incoming connections until the server is shutdown.
When a new connection arrives a new Task is spawned that'll manage the connection through its socket, wrapping around it both a new SocketObjectReader and a new ConcurrentSocketObjectWriter.
The task will then await for a new ChatMessage from the SocketObjectReader and take appropriate action.

The server will need to keep a dictionary of all the logged user associated with set of all their ConcurrentSocketObjectWriter. A set is necessary because we're dealing with multi-device logins.
A reverse dictionary of ConcurrentSocketObjectWriter (identified by their hash) associated with its logged username is also needed to manage disconnections.
The server dictionaries are not ConcurrentDictionary because all of the concurrency is managed through an external ReadWriteLock. 

Multiple connection handling tasks can safely send message to the same client concurrently thanks to the usage of a ConcurrentSocketObjectWriter. Another approach would have been to use a msg AsyncBlockingQueue with an outgoing message handling task for each connection.


### How does the client works ?
The client program can be used both as a single interactive client or as a server load testing tool. In the latter case it can spawn any number of clients generating the desired amount of traffic.

On a 6C/12T Core i5-12400F with 16GB of RAM, in Release mode with no debugger attached and using Custom serialization for message exchange, a load test averaging 9000 clients connected to a localhost server with an average traffic of 82000 msg/sec
resulted in a server avg CPU load of 10-15%, an avg thread nr of 30-40 and an avg memory footprint of 200MB.


## How should I use this ?
As said previously you'll need both AsyncStuff and SerializationLibrary. Both repositories are publicly available from my github.
Build them and link the required assemblies for each project.

Please note that the client and the server both have to use the same port and serialization method for exchanging messages.
The currently implemented serialization methods are through BinaryFormatter, XML DataContract and Custom defined.

If you want to add more ChatMessage(s) just follow the template of the implemented ones. Remember to:
* add a new case inside the ChatMessage.Deserialize(...) static method 
* add a new value to the ChatMessageType enum
* decorate the new subclass with the correct attributes
* add a new [KnownType] attribute to the base class (for the DataContract serialization)

To test the whole system as a normal chat app just start the server and then launch multiple clients choosing the ManageUserCommands() mode (please see below in the 'How should I use the client ?' section)

### How should I use the server ?
Start the server. It binds by default to port 2812 on every local interface. If you want to change this you can do it modifying the line: 
	
	IPEndPoint listSocketEndPoint = new IPEndPoint(IPAddress.Any, 2812);
in the Main method of Program.

To change the serialization method you can do it in the Program class changing the line:

	static private readonly SerializationMode _serializationMode = SerializationMode.Custom;

To stop the server type 'x' and press ENTER.

### How should I use the client ?
You can change the server IP, port and the client serialization mode in the Program class through these lines:

	static private readonly SerializationMode _serializationMode = SerializationMode.Custom;
    static private readonly int _serverPort = 2812;
    static private IPAddress _serverIp = new IPAddress(new byte[] { 192, 168, 1, 25 });

The Main method can call 3 functions. Just choose one and comment the rest out.

    static void Main(string[] args)
    {
        //TestConnectionsWithRetries();
        SpawnMultipleConnectionsMain(args, 10000); 
        //ManageUserCommands();            
    }

* TestConnectionsWithRetries() is an interactive mode. By entering a number N != 0, N connections will be spawned trying to connect and generate random traffic.
    By typing 'x' all the established connections are terminated. By typing 0 the program ends. 

    In order to change the ConnMaxNrOfAttempts, ConnTimeoutInSecs and RandomTimeBetweenMsgs please modify this line inside Program.TestConnectionsWithRetries()

      SpawnNewConnectionAsync(_serverIp, _serverPort, 5, 2, 10, cts.Token);

* SpawnMultipleConnectionsMain(args, numClients) is the load test mode. It spawns a numClients amount of clients each generating random traffic like logging in, loggin out or sending a msg to a peer.
    The random traffic can be tuned by changing this line inside Program.SpawnMultipleConnectionsMain
  
       _ = clients[i].CreateRandomLoginLogoutTrafficAsync(0, 100, 1000, 100, numClients); 

    The first parameter is not used, the second is the AvgNumberOfMsgsPerLogin, the third the AvgDelayBetweenLogins, the fourth the AvgDelayBetweenMsgs and the last one ofc the mumber of clients to spawn.

* ManageUserCommands() is the "normal" interactive client mode where a user can write commands and the client will show any incoming message from other peers.
    The commands are:
  * connect //this must be used first
  * disconnect
  * x //this will abort any ongoing connection attempt
  * login \<name> //used as 'login michele'
  * logout \<name //used as 'logout michele'
  * msg \<from> \<to> \<content> //used as 'msg michele mario Hi!'

  Please check the Program.ManageUserCommands() method for more info.





