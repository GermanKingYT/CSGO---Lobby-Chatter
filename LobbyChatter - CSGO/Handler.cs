using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SteamKit2;
using SteamKit2.Internal;

namespace LobbyChatter___CSGO
{
    class Handler : ClientMsgHandler
    {
        public class MyCallback : CallbackMsg
        {
            public EResult Result { get; private set; }

            internal MyCallback(EResult res)
            {
                Result = res;
            }
        }

        public void JoinLobby()
        {
            var JoinLobby = new ClientMsgProtobuf<CMsgClientMMSJoinLobby>(EMsg.ClientMMSJoinLobby);
            JoinLobby.ProtoHeader.routing_appid = 730;
            JoinLobby.Body.app_id = 730;
            JoinLobby.Body.persona_name = "DEIN-NAME-HIER";
            JoinLobby.Body.steam_id_lobby = (ulong)109775243754032135; // Lobby link

            Console.WriteLine(JoinLobby.Body.steam_id_lobby);
            Client.Send(JoinLobby);

            //SendMessage(); Ausgeklammert weil, das noch nicht geht.



        }

        void SendMessage()
        {
            var SendMessage = new ClientMsgProtobuf<CMsgClientMMSSendLobbyChatMsg>(EMsg.ClientMMSSendLobbyChatMsg);

            string message = "Hallo.";
            byte[] array = Encoding.ASCII.GetBytes(message);

            //SendMessage.ProtoHeader.routing_appid = 730;
            SendMessage.Body.app_id = 730;
            SendMessage.Body.lobby_message = array;
            SendMessage.Body.steam_id_lobby = (ulong)109775243754032135;
            SendMessage.Body.steam_id_target = 0;


        }

        public override void HandleMsg(IPacketMsg packetMsg)
        {
            switch (packetMsg.MsgType)
            {
                case EMsg.ClientMMSJoinLobbyResponse:
                    HandleJoinLobbyResponse(packetMsg);
                    break;
                case EMsg.ClientMMSSendLobbyChatMsg: // Handled, die response und schickt unsere IPackets weiter
                    HandleMessageResponse(packetMsg);
                    break;
            }
        }



        void HandleJoinLobbyResponse(IPacketMsg packetMsg)
        {
            var JoinResp = new ClientMsgProtobuf<CMsgClientMMSJoinLobbyResponse>(packetMsg); // Packets werden ausgelesen und zugeordnet.

            EResult result = (EResult)JoinResp.Body.chat_room_enter_response;
            EResult app_id = (EResult)JoinResp.Body.app_id;
            EResult flags = (EResult)JoinResp.Body.lobby_flags;
            EResult type = (EResult)JoinResp.Body.lobby_type;
            EResult max_members = (EResult)JoinResp.Body.max_members;
            //EResult id_lobby = (EResult)JoinResp.Body.steam_id_lobby; <- Geht nicht weil EResult 32bit ist und nicht 64bit
            EResult owner = (EResult)JoinResp.Body.steam_id_owner;

            var id_lobby = JoinResp.Body.steam_id_lobby; // Das geht aber :)
            Console.WriteLine($"Join Response: {result}");
            Console.WriteLine($"app_id: {app_id}");
            Console.WriteLine($"Flags: {flags}");
            Console.WriteLine($"Type: {type}");
            Console.WriteLine($"Max Members: {max_members}");
            Console.WriteLine($"id_lobby: {id_lobby}");
            Console.WriteLine($"Owner: {owner}");
            Console.WriteLine($"FULL BODY: {JoinResp.Body.ToString()}");
            Client.PostCallback(new MyCallback(result));
        }

        void HandleMessageResponse(IPacketMsg packetMsg)
        {
            var resp = new ClientMsgProtobuf<CMsgClientMMSLobbyChatMsg>(packetMsg);

            Console.WriteLine($"Response: {resp.Body.lobby_message}");
        }




    }
}
