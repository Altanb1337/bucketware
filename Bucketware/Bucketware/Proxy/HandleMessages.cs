﻿using ENet.Managed;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Bucketware.Layouts;
using Bucketware.Natives;
using System.Windows.Forms;

namespace GrowbrewProxy
{
    public class HandleMessages
    {
        private delegate void SafeCallDelegate(string text);
        public PacketSending packetSender = new PacketSending();
        VariantList variant = new VariantList();

        public World worldMap = new World();

        bool isSwitchingServers = false;
        public bool enteredGame = false;
        public bool serverRelogReq = false;
        int checkPeerUsability(ENetPeer peer)
        {
            if (peer.IsNull) return -1;
            if (peer.State != ENetPeerState.Connected) return -3;

            return 0;
        }

        void LogDebugFile(string text)
        {
#if DEBUG
            File.AppendAllText("debuglogs.txt", text);
#endif
            
        }

        NetTypes.NetMessages GetMessageType(byte[] data)
        {
            uint messageType = uint.MaxValue - 1;
            if (data.Length > 4)
                messageType = BitConverter.ToUInt32(data, 0);
            return (NetTypes.NetMessages)messageType;
        }

        NetTypes.PacketTypes GetPacketType(byte[] packetData)
        {
            return (NetTypes.PacketTypes)packetData[0]; // additional data will be located at 1, 2, not required for packet type tho.
        }


        /*
         **ONSENDTOSERVER INDEXES/VALUE LOCATIONS**
            port = 1
            token = 2
            userId = 3
            IPWithExtraData = 4
            lmode = 5 (Used for determining how client should behave when leaving, and could also influence the connection after.
            */
        private int OperateVariant(VariantList.VarList vList, object botPeer)
        {                      
            switch (vList.FunctionName)
            {
                case "OnConsoleMessage":
                    {
                        string m = (string)vList.functionArgs[1];
                        if ((m.Contains("lagged out,") || m.Contains("experiencing high load")) && !m.Contains("<") && !m.Contains("["))
                        {
                            GamePacketProton variantPacket2 = new GamePacketProton();
                            variantPacket2.AppendString("OnReconnect");
                            packetSender.SendData(variantPacket2.GetBytes(), Proxyhelper.proxyPeer);
                        }


                        break;
                    }
                case "OnRequestWorldSelectMenu":
                    {
                        if (Proxyhelper.globalUserData.autoEnterWorld != "")
                        {
                            packetSender.SendPacket(3, "action|join_request\nname|" + Proxyhelper.globalUserData.autoEnterWorld, Proxyhelper.realPeer);
                        }
                        break;
                    }
                case "OnSuperMainStartAcceptLogonHrdxs47254722215a":
                    {
                        
                        if (Proxyhelper.skipCache && botPeer == null)
                        {
                            
                            GamePacketProton gp = new GamePacketProton(); // variant list
                            gp.AppendString("OnRequestWorldSelectMenu");
                            packetSender.SendData(gp.GetBytes(), Proxyhelper.proxyPeer);
                        }
                        if (botPeer != null)
                        {
                            Console.WriteLine("BOT PEER IS ENTERING THE GAME...");
                            packetSender.SendPacket(3, "action|enter_game\n", (ENetPeer)botPeer);
                        }
                        return -1;
                    }
                case "OnZoomCamera":
                    {
                        
                        return -1;
                    }
                case "onShowCaptcha":
                    ((string)vList.functionArgs[1]).Replace("PROCESS_LOGON_PACKET_TEXT_42", "");// make captcha completable
                    try
                    {
                        string[] lines = ((string)vList.functionArgs[1]).Split('\n');
                        foreach (string line in lines)
                        {
                            if (line.Contains("+"))
                            {
                                string line2 = line.Replace(" ", "");
                                int a1, a2;
                                string[] splitByPipe = line2.Split('|');
                                string[] splitByPlus = splitByPipe[1].Split('+');
                                a1 = int.Parse(splitByPlus[0]);
                                a2 = int.Parse(splitByPlus[1]);
                                int result = a1 + a2;
                                string resultingPacket = "action|dialog_return\ndialog_name|captcha_submit\ncaptcha_answer|" + result.ToString() + "\n";
                                packetSender.SendPacket(2, resultingPacket, Proxyhelper.realPeer);
                            }
                        }
                        return -1;
                    }
                    catch
                    {
                        return -1; // Give this to user.
                    }
                case "OnDialogRequest":
                    if (!((string)vList.functionArgs[1]).ToLower().Contains("captcha")) return -1; // Send Client Dialog
                    ((string)vList.functionArgs[1]).Replace("PROCESS_LOGON_PACKET_TEXT_42", "");// make captcha completable
                    try
                    {
                        string[] lines = ((string)vList.functionArgs[1]).Split('\n');
                        foreach (string line in lines)
                        {
                            if (line.Contains("+"))
                            {
                                string line2 = line.Replace(" ", "");
                                int a1, a2;
                                string[] splitByPipe = line2.Split('|');
                                string[] splitByPlus = splitByPipe[1].Split('+');
                                a1 = int.Parse(splitByPlus[0]);
                                a2 = int.Parse(splitByPlus[1]);
                                int result = a1 + a2;
                                string resultingPacket = "action|dialog_return\ndialog_name|captcha_submit\ncaptcha_answer|" + result.ToString() + "\n";
                                packetSender.SendPacket(2, resultingPacket, Proxyhelper.realPeer);
                            }
                        }
                        return -1;
                    }
                    catch
                    {
                        return -1; // Give this to user.
                    }
            
                case "OnSendToServer":
                    {
                        // TODO FIX THIS AND MIRROR ALL PACKETS AND SOME BUG FIXES.
                        
                        string ip = (string)vList.functionArgs[4];
                        string doorid = "";

                        if (ip.Contains("|")) {
                            doorid = ip.Substring(ip.IndexOf("|") + 1);
                            ip = ip.Substring(0, ip.IndexOf("|"));
                        }
                        
                        int port = (int)vList.functionArgs[1];
                        int userID = (int)vList.functionArgs[3];
                        int token = (int)vList.functionArgs[2];
                        GamePacketProton variantPacket = new GamePacketProton();
                        variantPacket.AppendString("OnConsoleMessage");
                        variantPacket.AppendString("`6(PROXY)`o Switching subserver...``");
                        packetSender.SendData(variantPacket.GetBytes(), Proxyhelper.proxyPeer);


                        Proxyhelper.globalUserData.Growtopia_IP = token < 0 ? Proxyhelper.globalUserData.Growtopia_Master_IP : ip;
                        Proxyhelper.globalUserData.Growtopia_Port = token < 0 ? Proxyhelper.globalUserData.Growtopia_Master_Port : port;
                        Proxyhelper.globalUserData.isSwitchingServer = true;
                        Proxyhelper.globalUserData.token = token;
                        Proxyhelper.globalUserData.lmode = 1;
                        Proxyhelper.globalUserData.userID = userID;
                        Proxyhelper.globalUserData.doorid = doorid;

                        packetSender.SendPacket(3, "action|quit", Proxyhelper.realPeer);
                        Proxyhelper.realPeer.Disconnect(0);

                        return -1;
                    }
                case "OnSpawn":
                    {
                        worldMap.playerCount++;
                        string onspawnStr = (string)vList.functionArgs[1];
                        //MessageBox.Show(onspawnStr);
                        string[] tk = onspawnStr.Split('|');
                        Player p = new Player();
                        string[] lines = onspawnStr.Split('\n');

                        bool localplayer = false;

                        foreach (string line in lines)
                        {
                            string[] lineToken = line.Split('|');
                            if (lineToken.Length != 2) continue;
                            switch (lineToken[0])
                            {
                                case "netID":
                                    p.netID = Convert.ToInt32(lineToken[1]);
                                    break;
                                case "userID":
                                    p.userID = Convert.ToInt32(lineToken[1]);
                                    break;
                                case "name":
                                    p.name = lineToken[1];
                                    break;
                                case "country":
                                    p.country = lineToken[1];
                                    break;
                                case "invis":
                                    p.invis = Convert.ToInt32(lineToken[1]);
                                    break;
                                case "mstate":
                                    p.mstate = Convert.ToInt32(lineToken[1]);
                                    break;
                                case "smstate":
                                    p.mstate = Convert.ToInt32(lineToken[1]);
                                    break;
                                case "posXY":
                                    if (lineToken.Length == 3) // exactly 3 not more not less
                                    {
                                        p.X = Convert.ToInt32(lineToken[1]);
                                        p.Y = Convert.ToInt32(lineToken[2]);
                                    }
                                    break;
                                case "type":
                                    if (lineToken[1] == "local") localplayer = true;
                                    break;
                                    
                            }
                        }
                        //MainForm.LogText += ("[" + DateTime.UtcNow + "] (PROXY): " + onspawnStr);
                        worldMap.players.Add(p);


                        /*if (p.name.Contains(MainForm.tankIDName))
                        {
                           
                        }*/ //crappy code

                        if (p.mstate > 0 || p.smstate > 0 || p.invis > 0)
                        {
                            if (Proxyhelper.globalUserData.cheat_autoworldban_mod) banEveryoneInWorld();
                        }

                        if (localplayer)
                        {
                            string lestring = (string)vList.functionArgs[1];

                            string[] avatardata = lestring.Split('\n');
                            string modified_avatardata = string.Empty;
                           
                            foreach (string av in avatardata)
                            {
                                if (av.Length <= 0) continue;

                                string key = av.Substring(0, av.IndexOf('|'));
                                string value = av.Substring(av.IndexOf('|') + 1);

                                switch (key)
                                {
                                    case "mstate": // unlimited punch/place range edit smstate
                                        value = "1";
                                        break;
                                }

                                modified_avatardata += key + "|" + value + "\n";
                            }

                            //lestring = lestring.Replace("mstate|0", "mstate|1");

                            if (Proxyhelper.globalUserData.unlimitedZoom)
                            {
                                GamePacketProton gp = new GamePacketProton();
                                gp.AppendString("OnSpawn");
                                gp.AppendString(modified_avatardata);
                                gp.delay = (int)vList.delay;
                                gp.NetID = vList.netID;

                                packetSender.SendData(gp.GetBytes(), Proxyhelper.proxyPeer);
                            }

                            
                            worldMap.netID = p.netID;
                            worldMap.userID = p.userID;
                            return -2;
                        }
                        else
                        {
                            return p.netID;
                        }
                    }
                case "OnRemove":
                    {
                        int netID = -1;

                        string onremovestr = (string)vList.functionArgs[1];
                        string[] lineToken = onremovestr.Split('|');
                        if (lineToken[0] != "netID") break;

                        int.TryParse(lineToken[1], out netID);
                        for (int i = 0; i < worldMap.players.Count; i++)
                        {
                            if (worldMap.players[i].netID == netID)
                            {
                                worldMap.players.RemoveAt(i);
                                break;
                            }
                        }
                    
                        return netID;
                    }
                default:
                    return -1;
            }
            return 0;
        }

        string GetProperGenericText(byte[] data)
        {
            string growtopia_text = string.Empty;
            if (data.Length > 5)
            {
                int len = data.Length - 5;
                byte[] croppedData = new byte[len];
                Array.Copy(data, 4, croppedData, 0, len);
                growtopia_text = Encoding.ASCII.GetString(croppedData);
            }
            return growtopia_text;
        }

        private void SwitchServers(ref ENetPeer peer, string ip, int port, int lmode = 0, int userid = 0, int token = 0)
        {
            Proxyhelper.globalUserData.Growtopia_IP = token < 0 ? Proxyhelper.globalUserData.Growtopia_Master_IP : ip;
            Proxyhelper.globalUserData.Growtopia_Port = token < 0 ? Proxyhelper.globalUserData.Growtopia_Master_Port : port;
            isSwitchingServers = true;

            Proxyhelper.ConnectToServer(ref peer, Proxyhelper.globalUserData);
        }

        void banEveryoneInWorld()
        {
            foreach (Player p in worldMap.players)
            {
                string pName = p.name.Substring(2);
                pName = pName.Substring(0, pName.Length - 2);
                packetSender.SendPacket((int)NetTypes.NetMessages.GENERIC_TEXT, "action|input\n|text|/ban " + pName, Proxyhelper.realPeer);
            }
        }

        bool IsBitSet(int b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }



        public string HandlePacketFromClient(ref ENetPeer peer, ENetPacket packet) // Why string? Oh yeah, it's the best thing to also return a string response for anything you want!
        {

            if (peer.IsNull) return "";
            if (peer.State != ENetPeerState.Connected) return "";
            if (Proxyhelper.realPeer.IsNull) return "";
            if (Proxyhelper.realPeer.State != ENetPeerState.Connected) return "";

            bool respondToBotPeers = true;
            byte[] data = packet.Data.ToArray();

            string log = string.Empty;

           

            switch ((NetTypes.NetMessages)data[0])
            {
                case NetTypes.NetMessages.GENERIC_TEXT:
                    string str = GetProperGenericText(data);

                    if (str.StartsWith("action|"))
                    {
                        string actionExecuted = str.Substring(7, str.Length - 7);
                        string inputPH = "input\n|text|";
                        if (actionExecuted.StartsWith("enter_game"))
                        {
                            respondToBotPeers = true;
                            if (Proxyhelper.globalUserData.blockEnterGame) return "Blocked enter_game packet!";
                            enteredGame = true;
                        }
                        else if (actionExecuted.StartsWith(inputPH))
                        {

                            string text = actionExecuted.Substring(inputPH.Length);

                            if (text.Length > 0)
                            {
                                if (text.StartsWith("/")) // bAd hAcK - but also lazy, so i'll be doing this.
                                {

                                    switch (text)
                                    {
                                        case "/banworld":
                                            {
                                                banEveryoneInWorld();
                                                return "called /banworld, attempting to ban everyone who is in world (requires admin/owner)";
                                            }
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // for (int i = 0; i < 1000; i++) packetSender.SendPacket(2, "action|refresh_item_data\n", MainForm.realPeer);
                        respondToBotPeers = false;
                        string[] lines = str.Split('\n');

                        string tankIDName = "";
                        foreach (string line in lines)
                        {
                            string[] lineToken = line.Split('|');
                            if (lineToken.Length != 2) continue;
                            switch (lineToken[0])
                            {
                                case "tankIDName":
                                    tankIDName = lineToken[1];
                                    break;
                                case "tankIDPass":
                                    Proxyhelper.globalUserData.tankIDPass = lineToken[1];
                                    break;
                                case "requestedName":
                                    Proxyhelper.globalUserData.requestedName = lineToken[1];
                                    break;
                                case "token":
                                    Proxyhelper.globalUserData.token = int.Parse(lineToken[1]);
                                    break;
                                case "user":
                                    Proxyhelper.globalUserData.userID = int.Parse(lineToken[1]);
                                    break;
                                case "lmode":
                                    Proxyhelper.globalUserData.lmode = int.Parse(lineToken[1]);
                                    break;

                            }
                        }
                        Proxyhelper.globalUserData.tankIDName = tankIDName;

                        bool hasAcc = false;

                        packetSender.SendPacket((int)NetTypes.NetMessages.GENERIC_TEXT, Proxyhelper.CreateLogonPacket(), Proxyhelper.realPeer);
                        return "Sent logon packet!"; // handling logon over proxy
                    }
                    break;
                case NetTypes.NetMessages.GAME_MESSAGE:
                    string str2 = GetProperGenericText(data);
                    
                    if (str2.StartsWith("action|"))
                    {
                        string actionExecuted = str2.Substring(7);
                        if (actionExecuted.StartsWith("quit") && !actionExecuted.StartsWith("quit_to_exit"))
                        {
                            
                            // super multibotting will not mirror all packets in here (the "quit" action), cuz i found it unnecessary, although, you can enable that by pasting the code that does it.
                            respondToBotPeers = true;
                            Proxyhelper.globalUserData.token = -1;
                            Proxyhelper.globalUserData.Growtopia_IP = Proxyhelper.globalUserData.Growtopia_Master_IP;
                            Proxyhelper.globalUserData.Growtopia_Port = Proxyhelper.globalUserData.Growtopia_Master_Port;

                            if (Proxyhelper.realPeer != null)
                            {
                                if (!Proxyhelper.realPeer.IsNull)
                                    if (Proxyhelper.realPeer.State != ENetPeerState.Disconnected) Proxyhelper.realPeer.Disconnect(0);
                            }
                            if (Proxyhelper.proxyPeer != null)
                            {
                                if (!Proxyhelper.proxyPeer.IsNull)
                                    if (Proxyhelper.proxyPeer.State == ENetPeerState.Connected) Proxyhelper.proxyPeer.Disconnect(0);
                            }
                        }
                        else if (actionExecuted.StartsWith("join_request\nname|")) // ghetto fetching of worldname
                        {
                            string[] rest = actionExecuted.Substring(18).Split('\n');
                            string joinWorldName = rest[0];
                            Console.WriteLine($"Joining world {joinWorldName}...");
                        }
                    }
                    break;
                case NetTypes.NetMessages.GAME_PACKET:
                    {
                        TankPacket p = TankPacket.UnpackFromPacket(data);

                        switch ((NetTypes.PacketTypes)(byte)p.PacketType)
                        {
                            case NetTypes.PacketTypes.APP_INTEGRITY_FAIL:  /*rn definitely just blocking autoban packets, 
                                usually a failure of an app integrity is never good 
                                and usually used for security stuff*/
                                return "Possible autoban packet with id (25) from your GT Client has been blocked."; // remember, returning anything will interrupt sending this packet. To Edit packets, load/parse them and you may just resend them like normally after fetching their bytes.
                            case NetTypes.PacketTypes.PLAYER_LOGIC_UPDATE:
                                if (p.PunchX > 0 || p.PunchY > 0)
                                {
                                    Proxyhelper.LogText += ("[" + DateTime.UtcNow + "] (PROXY): PunchX/PunchY detected, pX: " + p.PunchX.ToString() + " pY: " + p.PunchY.ToString() + "\n");
                                }
                                Proxyhelper.globalUserData.isFacingSwapped = IsBitSet(p.CharacterState, 4);

                                worldMap.player.X = (int)p.X;
                                worldMap.player.Y = (int)p.Y;
                                break;
                            case NetTypes.PacketTypes.PING_REPLY:
                                {
                                    //SpoofedPingReply(p);
                                    return "Blocked ping reply!";
                                }
                            case NetTypes.PacketTypes.TILE_CHANGE_REQ:
                                respondToBotPeers = true;

                                if (p.MainValue == 32)
                                {
                                    /*MessageBox.Show("Log of potentially wanted received GAME_PACKET Data:" +
                    "\npackettype: " + data[4].ToString() +
                    "\npadding byte 1|2|3: " + data[5].ToString() + "|" + data[6].ToString() + "|" + data[7].ToString() +
                    "\nnetID: " + p.NetID +
                    "\nsecondnetid: " + p.SecondaryNetID +
                    "\ncharacterstate (prob 8): " + p.CharacterState +
                    "\nwaterspeed / offs 16: " + p.Padding +
                    "\nmainval: " + p.MainValue +
                    "\nX|Y: " + p.X + "|" + p.Y +
                    "\nXSpeed: " + p.XSpeed +
                    "\nYSpeed: " + p.YSpeed +
                    "\nSecondaryPadding: " + p.SecondaryPadding +
                    "\nPunchX|PunchY: " + p.PunchX + "|" + p.PunchY);*/

                                    Proxyhelper.globalUserData.lastWrenchX = (short)p.PunchX;
                                    Proxyhelper.globalUserData.lastWrenchY = (short)p.PunchY;
                                }
                                else if (p.MainValue == 18 && Proxyhelper.globalUserData.redDamageToBlock)
                                {
                                    // playingo
                                    p.SecondaryPadding = -1;
                                    p.ExtDataMask |= 1 << 27; // 28
                                    p.Padding = 1;
                                    packetSender.SendPacketRaw(4, p.PackForSendingRaw(), Proxyhelper.realPeer);
                                    return "";
                                }
                                break;
                            case NetTypes.PacketTypes.ITEM_ACTIVATE_OBJ: // just incase, to keep better track of items incase something goes wrong
                                worldMap.dropped_ITEMUID = p.MainValue;
                                if (Proxyhelper.globalUserData.blockCollecting) return "";
                                break;
                            default:
                                //MainForm.LogText += ("[" + DateTime.UtcNow + "] (CLIENT): Got Packet Type: " + p.PacketType.ToString() + "\n");
                                break;
                        }

                        if (data[4] > 23)
                        {
                            log = $"(CLIENT) Log of potentially wanted received GAME_PACKET Data:" +
                        "\npackettype: " + data[4].ToString() +
                        "\npadding byte 1|2|3: " + data[5].ToString() + "|" + data[6].ToString() + "|" + data[7].ToString() +
                        "\nnetID: " + p.NetID +
                        "\nsecondnetid: " + p.SecondaryNetID +
                        "\ncharacterstate (prob 8): " + p.CharacterState +
                        "\nwaterspeed / offs 16: " + p.Padding +
                        "\nmainval: " + p.MainValue +
                        "\nX|Y: " + p.X + "|" + p.Y +
                        "\nXSpeed: " + p.XSpeed +
                        "\nYSpeed: " + p.YSpeed +
                        "\nSecondaryPadding: " + p.SecondaryPadding +
                        "\nPunchX|PunchY: " + p.PunchX + "|" + p.PunchY;
                            
                        }
                    }
                    
                    break;
                case NetTypes.NetMessages.TRACK:
                    return "Packet with messagetype used for tracking was blocked!";
                case NetTypes.NetMessages.LOG_REQ:
                    return "Log request packet from client was blocked!";
                default:
                    break;
            }

            packetSender.SendData(data, Proxyhelper.realPeer);
            
            return log;

        
        }

        private void SpoofedPingReply(TankPacket tPacket)
        {
            if (worldMap == null) return;
            TankPacket p = new TankPacket();
            p.PacketType = (int)NetTypes.PacketTypes.PING_REPLY;
            p.PunchX = (int)1000.0f;
            p.PunchY = (int)250.0f;
            p.X = 64.0f;
            p.Y = 64.0f;
            p.MainValue = tPacket.MainValue; // GetTickCount()
            p.SecondaryNetID = (int)Proxyhelper.HashBytes(BitConverter.GetBytes(tPacket.MainValue)); // HashString of it

            // rest is 0 by default to not get detected by ac.
            packetSender.SendPacketRaw((int)NetTypes.NetMessages.GAME_PACKET, p.PackForSendingRaw(), Proxyhelper.realPeer);
        }

        public string HandlePacketFromServer(ref ENetPeer peer, ENetPacket packet)
        {

            if (Proxyhelper.proxyPeer.IsNull) return "HandlePacketFromServer() -> Proxy peer is null!";
            if (Proxyhelper.proxyPeer.State != ENetPeerState.Connected) return $"HandlePacketFromServer() -> proxyPeer is not connected: state = {Proxyhelper.proxyPeer.State}";
            if (peer.IsNull) return "HandlePacketFromServer() -> peer.IsNull is true!";
            if (peer.State != ENetPeerState.Connected) return "HandlePacketFromServer() -> peer.State was not ENetPeerState.Connected!";

            byte[] data = packet.Data.ToArray();


            NetTypes.NetMessages msgType = (NetTypes.NetMessages)data[0]; // more performance.
            switch (msgType)
            {
                case NetTypes.NetMessages.SERVER_HELLO:
                    {
                        Proxyhelper.UserData ud;

                        if (peer.TryGetUserData(out ud))
                            packetSender.SendPacket(2, Proxyhelper.CreateLogonPacket(ud.tankIDName, ud.tankIDPass, ud.userID, ud.token, ud.doorid), peer);
                        
                        break;
                    }
                case NetTypes.NetMessages.GAME_MESSAGE:

                    string str = GetProperGenericText(data);
                    Proxyhelper.LogText += ("[" + DateTime.UtcNow + "] (SERVER): A game_msg packet was sent: " + str + "\n");

                    if (str.Contains("Server requesting that you re-logon"))
                    {
                        Proxyhelper.globalUserData.token = -1;
                        Proxyhelper.globalUserData.Growtopia_IP = Proxyhelper.globalUserData.Growtopia_Master_IP;
                        Proxyhelper.globalUserData.Growtopia_Port = Proxyhelper.globalUserData.Growtopia_Master_Port;
                        Proxyhelper.globalUserData.isSwitchingServer = true;

                        Proxyhelper.realPeer.Disconnect(0);
                    }

                    break;
                case NetTypes.NetMessages.GAME_PACKET:

                    byte[] tankPacket = VariantList.get_struct_data(data);
                    if (tankPacket == null) break;

                    byte tankPacketType = tankPacket[0];
                    NetTypes.PacketTypes packetType = (NetTypes.PacketTypes)tankPacketType;
                    if (Proxyhelper.logallpackettypes)
                    {
                        GamePacketProton gp = new GamePacketProton();
                        gp.AppendString("OnConsoleMessage");
                        gp.AppendString("`6(PROXY) `wPacket TYPE: " + tankPacketType.ToString());
                        packetSender.SendData(gp.GetBytes(), Proxyhelper.proxyPeer);

                        if (tankPacketType > 18) File.WriteAllBytes("newpacket.dat", tankPacket);
                    }

                    switch (packetType)
                    {
                       
                        case NetTypes.PacketTypes.PLAYER_LOGIC_UPDATE:
                            {
                                TankPacket p = TankPacket.UnpackFromPacket(data);
                                foreach (Player pl in worldMap.players)
                                {
                                    if (pl.netID == p.NetID)
                                    {
                                        pl.X = (int)p.X;
                                        pl.Y = (int)p.Y;
                                        break;
                                    }
                                }
                                break;
                            }
                        case NetTypes.PacketTypes.INVENTORY_STATE:
                            {
                                if (!Proxyhelper.globalUserData.dontSerializeInventory) 
                                    worldMap.player.SerializePlayerInventory(VariantList.get_extended_data(tankPacket));
                                break;
                            }
                        case NetTypes.PacketTypes.TILE_CHANGE_REQ:
                            {
                                TankPacket p = TankPacket.UnpackFromPacket(data);

                                if (worldMap == null)
                                {
                                    Proxyhelper.LogText += ("[" + DateTime.UtcNow + "] (PROXY): (ERROR) World map was null." + "\n");
                                    break;
                                }
                                byte tileX = (byte)p.PunchX;
                                byte tileY = (byte)p.PunchY;
                                ushort item = (ushort)p.MainValue;


                                if (tileX >= worldMap.width) break;
                                else if (tileY >= worldMap.height) break;

                                ItemDatabase.ItemDefinition itemDef = ItemDatabase.GetItemDef(item);

                                

                                if (ItemDatabase.isBackground(item))
                                {
                                    worldMap.tiles[tileX + (tileY * worldMap.width)].bg = item;
                                }
                                else
                                {
                                    worldMap.tiles[tileX + (tileY * worldMap.width)].fg = item;
                                }

                                break;
                            }
                        case NetTypes.PacketTypes.CALL_FUNCTION:
                            VariantList.VarList VarListFetched = VariantList.GetCall(VariantList.get_extended_data(tankPacket));
                            VarListFetched.netID = BitConverter.ToInt32(tankPacket, 4); // add netid
                            VarListFetched.delay = BitConverter.ToUInt32(tankPacket, 20); // add keep track of delay modifier

                            bool isABot = false;
                            Proxyhelper.UserData ud = null;
                            
                            int netID = OperateVariant(VarListFetched, isABot ? (object)peer : null); // box enetpeer obj to generic obj
                            string argText = string.Empty;

                            for (int i = 0; i < VarListFetched.functionArgs.Count(); i++)
                            {
                                argText += " [" + i.ToString() + "]: " + (string)VarListFetched.functionArgs[i].ToString();
                            }

                            Proxyhelper.LogText += ("[" + DateTime.UtcNow + "] (SERVER): A function call was requested, see log infos below:\nFunction Name: " + VarListFetched.FunctionName + " parameters: " + argText + " \n");

                            if (VarListFetched.FunctionName == "OnSendToServer") return "Server switching forced, not continuing as Proxy Client has to deal with this.";
                            if (VarListFetched.FunctionName == "onShowCaptcha") return "Received captcha solving request, instantly bypassed it so it doesnt show up on client side.";
                            if (VarListFetched.FunctionName == "OnDialogRequest" && ((string)VarListFetched.functionArgs[1]).ToLower().Contains("captcha")) return "Received captcha solving request, instantly bypassed it so it doesnt show up on client side.";
                            if (VarListFetched.FunctionName == "OnDialogRequest" && ((string)VarListFetched.functionArgs[1]).ToLower().Contains("gazette")) return "Received gazette, skipping it...";
                            if (VarListFetched.FunctionName == "OnSetPos" && Proxyhelper.globalUserData.ignoreonsetpos && netID == worldMap.netID) return "Ignored position set by server, may corrupt doors but is used so it wont set back. (CAN BE BUGGY WITH SLOW CONNECTIONS)";
                            if (VarListFetched.FunctionName == "OnSpawn" && netID == -2)
                            {
                                if (Proxyhelper.globalUserData.unlimitedZoom)
                                    return "Modified OnSpawn for unlimited zoom (mstate|1)"; // only doing unlimited zoom and not unlimited punch/place to be sure that no bans occur due to this. If you wish to use unlimited punching/placing as well, change the smstate in OperateVariant function instead.
                            }
                           

                            break;
                        case NetTypes.PacketTypes.SET_CHARACTER_STATE:
                            {
                               
                                /*TankPacket p = TankPacket.UnpackFromPacket(data);

                                return "Log of potentially wanted received GAME_PACKET Data:" +
                         "\nnetID: " + p.NetID +
                         "\nsecondnetid: " + p.SecondaryNetID +
                         "\ncharacterstate (prob 8): " + p.CharacterState +
                         "\nwaterspeed / offs 16: " + p.Padding +
                         "\nmainval: " + p.MainValue +
                         "\nX|Y: " + p.X + "|" + p.Y +
                         "\nXSpeed: " + p.XSpeed +
                         "\nYSpeed: " + p.YSpeed +
                         "\nSecondaryPadding: " + p.SecondaryPadding +
                         "\nPunchX|PunchY: " + p.PunchX + "|" + p.PunchY;*/
                                break;
                            }
                        case NetTypes.PacketTypes.PING_REQ:
                            SpoofedPingReply(TankPacket.UnpackFromPacket(data));
                            break;
                        case NetTypes.PacketTypes.LOAD_MAP:
                            if (Proxyhelper.LogText.Length >= 32678) Proxyhelper.LogText = string.Empty;

                            worldMap = worldMap.LoadMap(tankPacket);
                            worldMap.player.didCharacterStateLoad = false;
                            worldMap.player.didClothingLoad = false;


                            Proxyhelper.realPeer.Timeout(1000, 2800, 3400);

                            break;
                        case NetTypes.PacketTypes.MODIFY_ITEM_OBJ:
                            {
                                TankPacket p = TankPacket.UnpackFromPacket(data);
                                if (p.NetID == -1)
                                {
                                    if (worldMap == null)
                                    {
                                        break;
                                    }

                                    worldMap.dropped_ITEMUID++;

                                    DroppedObject dItem = new DroppedObject();
                                    dItem.id = p.MainValue;
                                    dItem.itemCount = data[16];
                                    dItem.x = p.X;
                                    dItem.y = p.Y;
                                    dItem.uid = worldMap.dropped_ITEMUID;
                                    worldMap.droppedItems.Add(dItem);

                                    if (Proxyhelper.globalUserData.cheat_magplant)
                                    {


                                        TankPacket p2 = new TankPacket();
                                        p2.PacketType = (int)NetTypes.PacketTypes.ITEM_ACTIVATE_OBJ;
                                        p2.NetID = p.NetID;
                                        p2.X = (int)p.X;
                                        p2.Y = (int)p.Y;
                                        p2.MainValue = dItem.uid;

                                        packetSender.SendPacketRaw((int)NetTypes.NetMessages.GAME_PACKET, p2.PackForSendingRaw(), Proxyhelper.realPeer);
                                        //return "Blocked dropped packet due to magplant hack (auto collect/pickup range) tried to collect it instead, infos of dropped item => uid was " + worldMap.dropped_ITEMUID.ToString() + " id: " + p.MainValue.ToString();
                                    }
                                }
                            }
                            break;
                        default:
                            break;
                    }
                    break;
                case NetTypes.NetMessages.TRACK:
                    {
                        return "Track message:\n" + GetProperGenericText(data);
                        break;
                    }
                case NetTypes.NetMessages.LOG_REQ:
                case NetTypes.NetMessages.ERROR:
                    return "Blocked LOG_REQUEST/ERROR message from server";
                default:
                    //return "(SERVER): An unknown event occured. Message Type: " + msgType.ToString() + "\n";
                    break;

            }

            packetSender.SendData(data, Proxyhelper.proxyPeer);
            if (msgType == NetTypes.NetMessages.GAME_PACKET && data[4] > 39) // customizable on which packets you wanna log, for speed im just gonna do this!
            {
                TankPacket p = TankPacket.UnpackFromPacket(data);
                uint extDataSize = BitConverter.ToUInt32(data, 56);
                byte[] actualData = data.Skip(4).Take(56).ToArray();
                byte[] extData = data.Skip(60).ToArray();

                string extDataStr = "";
                string extDataStrShort = "";
                string extDataString = Encoding.UTF8.GetString(extData);
                for (int i = 0; i < extDataSize; i++)
                {
                    //ushort pos = BitConverter.ToUInt16(extData, i);
                    extDataStr += extData[i].ToString() + "|";
                }


                return "Log of potentially wanted received GAME_PACKET Data:" +
                    "\npackettype: " + actualData[0].ToString() +
                    "\npadding byte 1|2|3: " + actualData[1].ToString() + "|" + actualData[2].ToString() + "|" + actualData[3].ToString() +
                    "\nnetID: " + p.NetID +
                    "\nsecondnetid: " + p.SecondaryNetID +
                    "\ncharacterstate (prob 8): " + p.CharacterState +
                    "\nwaterspeed / offs 16: " + p.Padding +
                    "\nmainval: " + p.MainValue +
                    "\nX|Y: " + p.X + "|" + p.Y +
                    "\nXSpeed: " + p.XSpeed +
                    "\nYSpeed: " + p.YSpeed +
                    "\nSecondaryPadding: " + p.SecondaryPadding +
                    "\nPunchX|PunchY: " + p.PunchX + "|" + p.PunchY +
                    "\nExtended Packet Data Length: " + extDataSize.ToString() +
                    "\nExtended Packet Data:\n" + extDataStr + "\n";
                return string.Empty;
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
