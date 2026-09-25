using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Expedition;
using Steamworks;
using RWCustom;

namespace BingoMode.BingoSteamworks
{
    using BingoMenu;

    public class SteamFinal
    {
        public const int PlayerUpkeepTime = 1200;
        public const int MaxHostUpKeepTime = 4000;
        public const int TryToReconnectTime = 50;
        public const int MaxUpkeepCounter = 200;

        public static List<SteamNetworkingIdentity> ConnectedPlayers = [];
        public static Dictionary<ulong, bool> ReceivedPlayerUpKeep = [];
        public static int SendUpKeepCounter = PlayerUpkeepTime;
        public static int HostUpkeep = MaxHostUpKeepTime;
        public static int ReconnectTimer = TryToReconnectTime;
        public static int UpkeepCounter = MaxUpkeepCounter;
        public static bool ReceivedHostUpKeep;
        public static bool TryToReconnect;

        public static void ReceiveMessagesUpdate()
        {
            if (!BingoData.BingoMode || !BingoData.BingoSaves.ContainsKey(ExpeditionData.slugcatPlayer) || BingoData.BingoSaves[ExpeditionData.slugcatPlayer].hostID.GetSteamID64() == default) goto gabagoogye;

            RainWorld rw = Custom.rainWorld;
            if (rw == null) return;
            if (BingoData.BingoSaves[ExpeditionData.slugcatPlayer].hostID.GetSteamID64() != SteamTest.selfIdentity.GetSteamID64())
            {
                if (TryToReconnect)
                {
                    ReconnectTimer--;
                    if (ReconnectTimer <= 0)
                    {
                        if (ReceivedHostUpKeep)
                        {
                            
                            TryToReconnect = false;
                            HostUpkeep = MaxHostUpKeepTime;
                            ReceivedHostUpKeep = true;
                            if (rw.processManager.currentMainLoop is RainWorldGame game)
                            {
                                game.paused = false;
                            }
                            if (rw.processManager.IsRunningAnyDialog)
                            {
                                rw.processManager.StopSideProcess(rw.processManager.dialog);
                            }
                        }
                        else
                        {
                            
                            InnerWorkings.SendMessage("H" + SteamTest.selfIdentity.GetSteamID64(), BingoData.BingoSaves[ExpeditionData.slugcatPlayer].hostID);
                            ReconnectTimer = TryToReconnectTime;
                        }
                    }
                }
                else
                {
                    HostUpkeep--;
                    if (HostUpkeep <= 0)
                    {
                        HostUpkeep = MaxHostUpKeepTime;
                        if (ReceivedHostUpKeep)
                        {
                            
                            ReceivedHostUpKeep = false;
                        }
                        else
                        {
                            
                            // Didnt receve host upkeep, so host is probably disconnected
                            if (rw.processManager.IsRunningAnyDialog) rw.processManager.StopSideProcess(rw.processManager.dialog);
                            rw.processManager.ShowDialog(new InfoDialog(rw.processManager, BingoData.globalMenu.Translate("Lost connection to host.")));
                            Plugin.logger.LogInfo("DISCONNECTED FROM HOST");
                        }
                    }
                }
            }
            else
            {
                SendUpKeepCounter--;
                if (SendUpKeepCounter <= 0)
                {
                    
                    SendUpKeepCounter = PlayerUpkeepTime;
                    List<ulong> ToRemove = [];
                    List<ulong> ToFalse = [];
                    int allReceived = 0;
                    foreach (var kvp in ReceivedPlayerUpKeep)
                    {
                        
                        if (ReceivedPlayerUpKeep[kvp.Key])
                        {
                            
                            RequestPlayerUpKeep(kvp.Key);
                            ToFalse.Add(kvp.Key);
                            allReceived++;
                            continue;
                        }

                        
                        // Player didnt send their upkeep, so theyre considered dead to the host
                        ConnectedPlayers.RemoveAll(x => x.GetSteamID64() == kvp.Key);
                        ToRemove.Add(kvp.Key);
                        Plugin.logger.LogInfo("Player disconnected " + kvp.Key);
                    }
                    if (SteamTest.CurrentLobby != default && allReceived == ReceivedPlayerUpKeep.Keys.Count)
                    {
                        SteamTest.LeaveLobby();
                        foreach (var player in ConnectedPlayers) InnerWorkings.SendMessage("L", player);
                    }

                gobabk:
                    if (ToRemove.Count > 0)
                    {
                        foreach (var r in ToRemove)
                        {
                            
                            ReceivedPlayerUpKeep.Remove(r);
                            ToRemove.Remove(r);
                            goto gobabk;
                        }
                    }

                    if (ToFalse.Count > 0)
                    {
                        foreach (var r in ToFalse)
                        {
                            
                            ReceivedPlayerUpKeep[r] = false;
                        }
                    }
                    BroadcastCurrentBoardState();
                }
                else
                {
                    UpkeepCounter--;
                    if (UpkeepCounter <= 0)
                    {
                        //List<ulong> ToFalse = [];
                        foreach (var kvp in ReceivedPlayerUpKeep)
                        {
                            if (!ReceivedPlayerUpKeep[kvp.Key])
                            {
                                RequestPlayerUpKeep(kvp.Key);
                                //ToFalse.Add(kvp.Key);
                            }
                        }

                        //if (ToFalse.Count > 0)
                        //{
                        //    foreach (var r in ToFalse)
                        //    {
                        //        
                        //        ReceivedPlayerUpKeep[r] = false;
                        //    }
                        //}
                        UpkeepCounter = MaxUpkeepCounter;
                    }
                }
            }

        gabagoogye:
            // How the fuck does this work
            IntPtr[] messges = new IntPtr[16];
            int messages = SteamNetworkingMessages.ReceiveMessagesOnChannel(0, messges, messges.Length);
            if (messages > 0)
            {
                for (int i = 0; i < messages; i++)
                {
                    // Ignore messages here after receiving to not queue them up
                    if (!BingoData.MultiplayerGame && !BingoData.BingoMode) continue; 

                    SteamNetworkingMessage_t netMessage = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messges[i]);

                    

                    byte[] data = new byte[netMessage.m_cbSize];
                    Marshal.Copy(netMessage.m_pData, data, 0, data.Length);
                    char[] chars = new char[data.Length / sizeof(char)];
                    Buffer.BlockCopy(data, 0, chars, 0, data.Length);
                    string message = new string(chars, 0, chars.Length);
                    
                    InnerWorkings.MessageReceived(message);
                }
            }
        }

        public static void RequestPlayerUpKeep(ulong playerID)
        {
            SteamNetworkingIdentity playerIdentity = new();
            playerIdentity.SetSteamID64(playerID);
            InnerWorkings.SendMessage("U", playerIdentity);
        }

        public static bool IsSaveMultiplayer(BingoData.BingoSaveData saveData)
        {
            //
            return saveData.hostID.GetSteamID64() != default;
        }

        public static SteamNetworkingIdentity GetHost()
        {
            return BingoData.BingoSaves[ExpeditionData.slugcatPlayer].hostID;
        }

        public static List<SteamNetworkingIdentity> PlayersFromString(string text)
        {
            List<SteamNetworkingIdentity> players = [];
            if (text == "") return players;
            foreach (string player in Regex.Split(text, "bPlR"))
            {
                SteamNetworkingIdentity playerIdentity = new();
                playerIdentity.SetSteamID64(ulong.Parse(player, NumberStyles.Any));
                players.Add(playerIdentity);
            }
            return players;
        }

        public static void ChallengeStateChangeToHost(Challenge ch, bool failed)
        {
            int x = -1;
            int y = -1;
            for (int j = 0; j < BingoHooks.GlobalBoard.challengeGrid.GetLength(1); j++)
            {
                bool b = false;
                for (int i = 0; i < BingoHooks.GlobalBoard.challengeGrid.GetLength(0); i++)
                {
                    if (BingoHooks.GlobalBoard.challengeGrid[i, j] == ch)
                    {
                        x = i;
                        y = j;
                        b = true;
                        break;
                    }
                }
                if (b) break;
            }
            Plugin.logger.LogInfo("ChallengeStateChangeToHost message being send to host: " + $"{(failed ? "^" : "#")}{x};{y};{SteamTest.team}" + " for challenge " + ch.ToString());
            InnerWorkings.SendMessage($"{(failed ? "^" : "#")}{x};{y};{SteamTest.team}", GetHost()); //;{SteamTest.selfIdentity.GetSteamID64()}
        }

        public static void BroadcastCurrentBoardState()
        {
            string state = BingoHooks.GlobalBoard.GetBingoState();
            foreach (var player in ConnectedPlayers)
            {
                InnerWorkings.SendMessage("B" + state.Replace('3', '1'), player);
            }
        }

        public static void BroadcastTallyUpOrder()
        {
            foreach (var player in ConnectedPlayers)
            {
                InnerWorkings.SendMessage("T", player);
            }
        }
    }
}
