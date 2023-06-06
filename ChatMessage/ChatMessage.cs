using Microsoft.VisualBasic;
using System.Runtime.Serialization;
using System.Text;
using SerializationLibrary;

namespace ChatMiscellaneous
{   
    public enum ChatMessageType
    {
        LoginRequest,
        LoginOk,
        LoginEvent,
        LoginFailed,
        LogoutOk,
        LogoutRequest,
        LogoutEvent,
        LogoutFailed,
        MessageFromUserToUser,
        SendMessageOk,
        SendMessageFailedRecipientNotLogged,
        SendMessageFailedSenderNotLogged
    }

    [DataContract]
    [KnownType(typeof(LoginRequest))]    
    [KnownType(typeof(LoginOk))]
    [KnownType(typeof(LoginEvent))]
    [KnownType(typeof(LoginFailed))]
    [KnownType(typeof(LogoutOk))]    
    [KnownType(typeof(LogoutRequest))]
    [KnownType(typeof(LogoutEvent))]
    [KnownType(typeof(LogoutFailed))]
    [KnownType(typeof(MessageFromUserToUser))]
    [KnownType(typeof(SendMessageOk))]
    [KnownType(typeof(SendMessageFailedRecipientNotLogged))]
    [KnownType(typeof(SendMessageFailedSenderNotLogged))]     
    [Serializable]  //for binary formatter
    public abstract class ChatMessage : ICustomSerializable<ChatMessage>       //<--- there's a class here !
    {
        public abstract byte[] Serialize();

        static public ChatMessage Deserialize(byte[] objByteBlob)
        {   
            if (objByteBlob == null)
                throw new ArgumentNullException();

            if (objByteBlob.Length == 0)
                throw new ArgumentException();

            ChatMessage toReturn;
            MemoryStream ms = new MemoryStream(objByteBlob);

            using(BinaryReader reader = new BinaryReader(ms))
            {
                ChatMessageType msgType = (ChatMessageType) reader.ReadByte();
                switch (msgType)
                {
                    case ChatMessageType.LoginRequest:
                        toReturn = new LoginRequest(reader.ReadString());
                        break;
                    case ChatMessageType.LogoutRequest:
                        toReturn = new LogoutRequest(reader.ReadString());
                        break;
                    case ChatMessageType.LoginOk:
                        toReturn = LoginOk.GetValue();
                        break;
                    case ChatMessageType.LogoutOk:
                        toReturn = LogoutOk.GetValue();
                        break;
                    case ChatMessageType.LoginFailed:
                        toReturn = LoginFailed.GetValue();
                        break;
                    case ChatMessageType.LoginEvent:
                        toReturn = new LoginEvent(reader.ReadString());
                        break;
                    case ChatMessageType.LogoutEvent:
                        toReturn = new LogoutEvent(reader.ReadString());
                        break;
                    case ChatMessageType.LogoutFailed:
                        toReturn = LogoutFailed.GetValue();
                        break;
                    case ChatMessageType.MessageFromUserToUser:
                        toReturn = new MessageFromUserToUser(reader.ReadString(), reader.ReadString(), reader.ReadString());
                        break;
                    case ChatMessageType.SendMessageOk:
                        toReturn = SendMessageOk.GetValue();
                        break;
                    case ChatMessageType.SendMessageFailedSenderNotLogged:
                        toReturn = SendMessageFailedSenderNotLogged.GetValue();
                        break;
                    case ChatMessageType.SendMessageFailedRecipientNotLogged:
                        toReturn = SendMessageFailedRecipientNotLogged.GetValue();
                        break;
                    default:
                        throw new ArgumentException();
                }
            }

            return toReturn;
        } 
    }

    [DataContract]
    [Serializable]  //for binary formatter
    public class LoginRequest : ChatMessage
    {
        [DataMember] private readonly string _user;

        public LoginRequest(string user)
        {
            _user = user;
        }

        public string UserName
        {
            get { return _user; }
        }

        public override string ToString()
        {
            return _user;
        }

        public override byte[] Serialize()
        {
            MemoryStream ms = new MemoryStream();
            
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write((byte) ChatMessageType.LoginRequest);     //message code
                //bw.Write(_user.Length); //with WriteString() used below we don't need to send the Length
                bw.Write(_user);
            }
              
            return ms.ToArray();
        }
    }

    [DataContract]
    [Serializable]  //for binary formatter
    public class LogoutRequest : ChatMessage
    {
        [DataMember] private readonly string _user;

        public LogoutRequest(string user)
        {
            _user = user;
        }

        public string UserName
        {
            get { return _user; }
        }

        public override string ToString()
        {
            return _user;
        }

        public override byte[] Serialize()
        {
            MemoryStream ms = new MemoryStream();

            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write((byte) ChatMessageType.LogoutRequest);     //message code
                //bw.Write(_user.Length); //for objects with only 1 string we could avoid specifying the length
                bw.Write(_user);
            }

            return ms.ToArray();
        }
    }

    [DataContract]
    [Serializable]  //for binary formatter
    public class LoginOk : ChatMessage
    {
        static private readonly LoginOk _singleton = new LoginOk();

        private LoginOk() { }

        static public LoginOk GetValue()
        {
            return _singleton;
        }

        public override byte[] Serialize()
        {
            return new byte[] { (byte) ChatMessageType.LoginOk };
        }
    }

    [DataContract]
    [Serializable]  //for binary formatter
    public class LogoutOk : ChatMessage
    {
        static private readonly LogoutOk _singleton = new LogoutOk();

        private LogoutOk() { }

        static public LogoutOk GetValue()
        {
            return _singleton;
        }

        public override byte[] Serialize()
        {
            return new byte[] { (byte) ChatMessageType.LogoutOk };
        }
    }

    [DataContract]
    [Serializable]  //for binary formatter
    public class LoginFailed : ChatMessage
    {
        static private readonly LoginFailed _singleton = new LoginFailed();

        private LoginFailed() { }

        static public LoginFailed GetValue()
        {
            return _singleton;
        }

        public override byte[] Serialize()
        {
            return new byte[] { (byte)ChatMessageType.LoginFailed };
        }
    }

    [DataContract]
    [Serializable]  //for binary formatter
    public class LoginEvent : ChatMessage
    {
        [DataMember] private readonly string _user;

        public LoginEvent(string user)
        {
            _user = user;
        }

        public string UserName
        {
            get { return _user; }
        }

        public override byte[] Serialize()
        {
            MemoryStream ms = new MemoryStream();

            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write((byte)ChatMessageType.LoginEvent);     //message code
                //bw.Write(_user.Length);
                bw.Write(_user);
            }

            return ms.ToArray();
        }
    }

    [DataContract]
    [Serializable]  //for binary formatter
    public class LogoutEvent : ChatMessage
    {
        [DataMember] private readonly string _user;

        public LogoutEvent(string user)
        {
            _user = user;
        }

        public string UserName
        {
            get { return _user; }
        }

        public override byte[] Serialize()
        {
            MemoryStream ms = new MemoryStream();

            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write((byte)(byte)ChatMessageType.LogoutEvent);     //message code
                //bw.Write(_user.Length);
                bw.Write(_user);
            }

            return ms.ToArray();
        }
    }

    [DataContract]
    [Serializable]  //for binary formatter
    public class LogoutFailed : ChatMessage
    {
        static private readonly LogoutFailed _singleton = new LogoutFailed();

        private LogoutFailed() { }

        static public LogoutFailed GetValue()
        {
            return _singleton;
        }

        public override byte[] Serialize()
        {
            return new byte[] { (byte)ChatMessageType.LogoutFailed };
        }
    }

    [DataContract]
    [Serializable]  //for binary formatter
    public class MessageFromUserToUser : ChatMessage
    {
        [DataMember] private readonly string _senderUserName;
        [DataMember] private readonly string _recipientUserName;
        [DataMember] private readonly string _msg;

        public MessageFromUserToUser(string sender, string receiver, string msg)
        {
            _senderUserName = sender;
            _recipientUserName = receiver;
            _msg = msg;
        }

        public string Sender { get => _senderUserName; }
        public string Recipient { get => _recipientUserName; }
        public string Message { get => _msg; }

        public override byte[] Serialize()
        {
            MemoryStream ms = new MemoryStream();

            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write((byte)ChatMessageType.MessageFromUserToUser);     //message code
                //bw.Write(_senderUserName.Length);
                bw.Write(_senderUserName);
                //bw.Write(_recipientUserName.Length);
                bw.Write(_recipientUserName);
                //bw.Write(_msg.Length);
                bw.Write(_msg);
            }

            return ms.ToArray();
        }
    }

    [DataContract]
    [Serializable]  //for binary formatter
    public class SendMessageOk : ChatMessage
    {
        static private readonly SendMessageOk _singleton = new SendMessageOk();

        private SendMessageOk() { }

        static public SendMessageOk GetValue()
        {
            return _singleton;
        }

        public override byte[] Serialize()
        {
            return new byte[] { (byte)ChatMessageType.SendMessageOk };
        }
    }

    [DataContract]
    [Serializable]  //for binary formatter
    public class SendMessageFailedSenderNotLogged : ChatMessage
    {
        static private readonly SendMessageFailedSenderNotLogged _singleton = new SendMessageFailedSenderNotLogged();

        private SendMessageFailedSenderNotLogged() { }

        static public SendMessageFailedSenderNotLogged GetValue()
        {
            return _singleton;
        }

        public override byte[] Serialize()
        {
            return new byte[] { (byte)ChatMessageType.SendMessageFailedSenderNotLogged };
        }
    }

    [DataContract]
    [Serializable]  //for binary formatter
    public class SendMessageFailedRecipientNotLogged : ChatMessage
    {
        static private readonly SendMessageFailedRecipientNotLogged _singleton = new SendMessageFailedRecipientNotLogged();

        private SendMessageFailedRecipientNotLogged() { }

        static public SendMessageFailedRecipientNotLogged GetValue()
        {
            return _singleton;
        }

        public override byte[] Serialize()
        {
            return new byte[] { (byte)ChatMessageType.SendMessageFailedRecipientNotLogged };
        }
    }

}
