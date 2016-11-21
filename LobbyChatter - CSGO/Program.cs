using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using SteamKit2;
using SteamKit2.GC.CSGO;
using SteamKit2.GC.CSGO.Internal;
using SteamKit2.GC;
using SteamKit2.Internal;

namespace LobbyChatter___CSGO
{
    static class Program
    {
        static SteamClient steamClient;
        static CallbackManager manager;

        static SteamGameCoordinator gameCoordinator;

        static SteamUser steamUser;
        
        static string username, password;
        static string authCode, twoFactorAuth;

        static ulong lobbyid;

        static bool isRunning;

        [STAThread]
        static void Main()
        {
            Console.WriteLine("CSGO Lobby Chatter ©Radat.");
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("Login with your Steam Account.");
            Console.WriteLine("Username:");
            username = Console.ReadLine();
            Console.WriteLine("Password:");
            password = Console.ReadLine();

            steamClient = new SteamClient();
            manager = new CallbackManager(steamClient);
            steamUser = steamClient.GetHandler<SteamUser>();
            gameCoordinator = steamClient.GetHandler<SteamGameCoordinator>();

            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);

            manager.Subscribe<SteamGameCoordinator.MessageCallback>(OnMessageCall);

            isRunning = true;


            Console.WriteLine("LobbyID to Chat?");
            lobbyid = ulong.Parse(Console.ReadLine());

            Console.WriteLine("Connecting to Steam...");
            
            steamClient.Connect();
            
            while (isRunning)
            {
                manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
        
        }

        static void OnMessageCall(SteamGameCoordinator.MessageCallback callback)
        {

            if (callback.EMsg == 4004)
            {
                new ClientGCMsgProtobuf<CMsgClientWelcome>(callback.Message);
                Console.WriteLine("GameCoordinator is welcoming us!");
                Thread.Sleep(1000);

                CMsgClientMMSSendLobbyChatMsg send = new CMsgClientMMSSendLobbyChatMsg()
                {
                    app_id = 720,
                    steam_id_target = 0,
                    steam_id_lobby = lobbyid,
                    lobby_message = 
                }; 
            }
        }
    
        static void OnConnected(SteamClient.ConnectedCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable to connect to Steam: {0}", callback.Result);

                isRunning = false;
                return;
            }

            Console.WriteLine("Connected to Steam! Logging in '{0}'...", username);

            byte[] sentryHash = null;
            if (File.Exists("sentry.bin"))
            {
                // if we have a saved sentry file, read and sha-1 hash it
                byte[] sentryFile = File.ReadAllBytes("sentry.bin");
                sentryHash = CryptoHelper.SHAHash(sentryFile);
            }

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = username,
                Password = password,

                // in this sample, we pass in an additional authcode
                // this value will be null (which is the default) for our first logon attempt
                AuthCode = authCode,

                // if the account is using 2-factor auth, we'll provide the two factor code instead
                // this will also be null on our first logon attempt
                TwoFactorCode = twoFactorAuth,

                // our subsequent logons use the hash of the sentry file as proof of ownership of the file
                // this will also be null for our first (no authcode) and second (authcode only) logon attempts
                SentryFileHash = sentryHash,
            });
        }

        static void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            // after recieving an AccountLogonDenied, we'll be disconnected from steam
            // so after we read an authcode from the user, we need to reconnect to begin the logon flow again

            Console.WriteLine("Disconnected from Steam, reconnecting in 5...");

            Thread.Sleep(TimeSpan.FromSeconds(5));

            steamClient.Connect();
        }

        static void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            bool isSteamGuard = callback.Result == EResult.AccountLogonDenied;
            bool is2FA = callback.Result == EResult.AccountLoginDeniedNeedTwoFactor;

            if (isSteamGuard || is2FA)
            {
                Console.WriteLine("This account is SteamGuard protected!");

                if (is2FA)
                {
                    Console.Write("Please enter your 2 factor auth code from your authenticator app: ");
                    twoFactorAuth = Console.ReadLine();
                }
                else
                {
                    Console.Write("Please enter the auth code sent to the email at {0}: ", callback.EmailDomain);
                    authCode = Console.ReadLine();
                }

                return;
            }

            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable to logon to Steam: {0} / {1}", callback.Result, callback.ExtendedResult);

                isRunning = false;
                return;
            }

            Console.WriteLine("Successfully logged on!");

            // at this point, we'd be able to perform actions on Steam
            ClientMsgProtobuf<CMsgClientGamesPlayed> msg = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed, 64);
            CMsgClientGamesPlayed.GamePlayed item = new CMsgClientGamesPlayed.GamePlayed
            {
                game_id = (ulong)new GameID(730)
            };
            msg.Body.games_played.Add(item);
            steamClient.Send(msg);
            Thread.Sleep(5000);
            ClientGCMsgProtobuf<CMsgClientHello> protobuf2 = new ClientGCMsgProtobuf<CMsgClientHello>(4006, 64);
            gameCoordinator.Send(protobuf2, 730);
        }

        static void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine("Logged off of Steam: {0}", callback.Result);
        }

        static void OnMachineAuth(SteamUser.UpdateMachineAuthCallback callback)
        {
            Console.WriteLine("Updating sentryfile...");

            // write out our sentry file
            // ideally we'd want to write to the filename specified in the callback
            // but then this sample would require more code to find the correct sentry file to read during logon
            // for the sake of simplicity, we'll just use "sentry.bin"

            int fileSize;
            byte[] sentryHash;
            using (var fs = File.Open("sentry.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                fs.Seek(callback.Offset, SeekOrigin.Begin);
                fs.Write(callback.Data, 0, callback.BytesToWrite);
                fileSize = (int)fs.Length;

                fs.Seek(0, SeekOrigin.Begin);
                using (var sha = new SHA1CryptoServiceProvider())
                {
                    sentryHash = sha.ComputeHash(fs);
                }
            }

            // inform the steam servers that we're accepting this sentry file
            steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
            {
                JobID = callback.JobID,

                FileName = callback.FileName,

                BytesWritten = callback.BytesToWrite,
                FileSize = fileSize,
                Offset = callback.Offset,

                Result = EResult.OK,
                LastError = 0,

                OneTimePassword = callback.OneTimePassword,

                SentryFileHash = sentryHash,
            });

            Console.WriteLine("Done!");
        }
    }
}
