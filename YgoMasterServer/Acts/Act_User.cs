﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace YgoMaster
{
    partial class GameServer
    {
        void Act_UserEntry(GameServerWebRequest request)
        {
            DuelRoom duelRoom = request.Player.DuelRoom;
            if (duelRoom != null && !duelRoom.Disbanded)
            {
                duelRoom.ResetTableStateIfMatchingOrDueling(request.Player);
            }

            /*// NOTE: To handle tutorial properly the following needs to be done:
            // - Handle "User.first_name_entry"
            // - Handle "Structure.first"
            // - Handle "User.complete_home_guide"
            // - Send "Master.Structure" here ("User.entry")
            // - Keep track of tutorial completion state
            // This can be used to create "Welcome to Yu-Gi-Oh!" / tutorial intro
            request.Response["Tutorial"] = new Dictionary<string, object>()
            {
                { "step", 0 },
                { "structure_ids", new List<int>() {
                    1120002,
                    1120003,
                    1120004
                }},
            };*/

            WriteUser(request);
            WriteDeck(request);
            WriteCards(request);
            WriteItem(request);
            request.Response["Craft"] = Craft.ToDictionary();

            // This is required to get duel room spectator watching options (live / not live)
            request.GetOrCreateDictionary("Server")["rapidwatch"] = true;

            WriteRoomInfo(request, request.Player.DuelRoom);
            Dictionary<string, object> roomData = request.GetOrCreateDictionary("Room");
            Dictionary<string, object> roomSettingDefData = Utils.GetOrCreateDictionary(roomData, "setting_def");
            roomSettingDefData["default_time_id"] = DuelRoomDefaultTimeIndex + 1;
            Dictionary<string, object> times = Utils.GetOrCreateDictionary(roomSettingDefData, "time");
            for (int i = 0; i < DuelRoomTimes.Count; i++)
            {
                Dictionary<string, object> timeData = new Dictionary<string, object>();
                timeData["name"] = DuelRoomTimes[i].Name;
                timeData["add_time"] = DuelRoomTimes[i].AddTimeAtStartOfTurn;
                timeData["add_time_after"] = DuelRoomTimes[i].AddTimeAtEndOfTurn;
                timeData["rest_time"] = DuelRoomTimes[i].Time;
                timeData["sort"] = i + 1;
                times[(i + 1).ToString()] = timeData;
            }

            request.Remove(
                "Room.room_info",
                "Master",
                "User",
                "Deck",
                "Cards",
                "Item",
                "Tutorial",
                "Solo");
        }

        void Act_UserNameEntry(GameServerWebRequest request)
        {
            object newName;
            if (request.ActParams.TryGetValue("name", out newName))
            {
                request.Player.Name = newName.ToString();
                SavePlayer(request.Player);
            }
            request.Response["User"] = new Dictionary<string, object>()
            {
                { "profile", new Dictionary<string, object>() {
                    { "name", request.Player.Name },
                }}
            };
        }

        void Act_UserSetProfile(GameServerWebRequest request)
        {
            Dictionary<string, object> args;
            if (Utils.TryGetValue(request.ActParams, "param", out args))
            {
                foreach (KeyValuePair<string, object> arg in args)
                {
                    switch (arg.Key)
                    {
                        case "icon_id":
                            request.Player.IconId = (int)Convert.ChangeType(arg.Value, typeof(int));
                            break;
                        case "icon_frame_id":
                            request.Player.IconFrameId = (int)Convert.ChangeType(arg.Value, typeof(int));
                            break;
                        case "avatar_id":
                            request.Player.AvatarId = (int)Convert.ChangeType(arg.Value, typeof(int));
                            break;
                        case "wallpaper":
                            request.Player.Wallpaper = (int)Convert.ChangeType(arg.Value, typeof(int));
                            break;
                        case "tag":
                            request.Player.TitleTags.Clear();
                            foreach (object tagObj in arg.Value as List<object>)
                            {
                                request.Player.TitleTags.Add((int)Convert.ChangeType(tagObj, typeof(int)));
                            }
                            break;
                        default:
                            Utils.LogWarning("Unhandled player profile arg '" + arg.Key + "' = " + MiniJSON.Json.Serialize(arg.Value));
                            break;
                    }
                }
                SavePlayer(request.Player);
            }
            // The server normally only writes back what was requested... but writing everything will do
            WriteUser(request);
        }

        void Act_UserHome(GameServerWebRequest request)
        {
            DuelRoom duelRoom = request.Player.DuelRoom;
            if (duelRoom != null && !duelRoom.Disbanded)
            {
                duelRoom.ResetTableStateIfMatchingOrDueling(request.Player);
            }

            // Room / Master.Regulation are required to create decks / view public decks without breaking the client
            Dictionary<string, object> structure = new Dictionary<string, object>();
            foreach (DeckInfo deck in StructureDecks.Values)
            {
                structure[deck.Id.ToString()] = deck.ToDictionaryStructureDeck();
            }
            request.Response["Master"] = new Dictionary<string, object>()
            {
                { "CardRare", GetCardRarities(request.Player) },// Card rarities
                { "CardCr", GetCraftableCards(request.Player) },// Craftable cards
                { "Structure", structure },// All structure decks
                { "Regulation", Regulation },// Forbidden / limited cards
                { "RegulationIcon", RegulationIcon },
            };
            request.Response["Regulation"] = RegulationInfo;
            request.Response["DuelMenu"] = new Dictionary<string, object>()
            {
                { "Standard", new Dictionary<string, object>() {
                    { "regulation_id", DeckInfo.DefaultRegulationId }
                }}
            };
            request.Response["Room"] = new Dictionary<string, object>()
            {
                { "rule_list", RegulationInfo["rule_list"] },
                { "holding_rule_list", RegulationInfo["rule_list"] },
                { "common", DeckInfo.DefaultRegulationId }
            };
            WriteDeck(request);
            request.Response["DeckList"] = null;
            request.Response["TDeck"] = new object[0];// Tournament deck list?
            request.Response["TDeckList"] = null;// Tournament deck list?
            request.Response["EXHDeck"] = new object[0];// Exhibition deck list?
            request.Response["EXHDeckList"] = null;// Exhibition deck list?
            // TODO: Move this information into a data file
            string lang = !string.IsNullOrEmpty(request.Player.Lang) ? request.Player.Lang : "en_US";
            Dictionary<string, object> body = new Dictionary<string,object>()
            {
                { "contents", new List<object>() {
                    new Dictionary<string, object>()
                    {
                        { "tp", "H1" },
                        { "text", new Dictionary<string, object>() { { lang,  "About" } } },
                    },
                    new Dictionary<string, object>()
                    {
                        { "tp", "Text" },
                        { "text", new Dictionary<string, object>() { { lang,  "Play \"Yu-Gi-Oh! Master Duel\" without an internet connection.\n\nProgress is not shared with the live game." } } },
                        { "indent", -1 },
                    },
                    new Dictionary<string, object>()
                    {
                        { "tp", "H1" },
                        { "text", new Dictionary<string, object>() { { lang,  "Documentation" } } },
                    },
                    new Dictionary<string, object>()
                    {
                        { "tp", "Text" },
                        { "text", new Dictionary<string, object>() { { lang,  "https://github.com/pixeltris/YgoMaster" } } },
                        { "indent", -1 },
                    },
                    new Dictionary<string, object>()
                    {
                        { "tp", "H1" },
                        { "text", new Dictionary<string, object>() { { lang,  "Issues" } } },
                    },
                    new Dictionary<string, object>()
                    {
                        { "tp", "Text" },
                        { "text", new Dictionary<string, object>() { { lang,  "https://github.com/pixeltris/YgoMaster/issues" } } },
                        { "indent", -1 },
                    },
                }}
            };
            if (ShowTopics)
            {
                request.Response["Topics"] = new List<object>()
                {
                    new Dictionary<string, object>() {
                        { "sort", 1591200 },// 0 / 1591200 / omitted
                        { "body", MiniJSON.Json.Serialize(body) },
                        { "banner", new Dictionary<string, object>() {
                            { "prefPath", "Prefabs/Notification/Topics/BannerNotify" },
                            { "pattern", TopicsBannerPatern.NOTIFY },
                            { "prefArgsJson", new Dictionary<string, object>() {
                                { "BackImage", "Images/Notification/System/Notice001" },
                                { "Title", "YgoMaster" },//"<color=#EEE>YgoMaster</color>",
                            }}
                        }},
                    }
                    /*new Dictionary<string, object>() {
                        { "sort", 1591200 },// 0 / 1591200 / omitted
                        { "body", @"{""buttons"":[{""label"":{""en_US"":""Purchase Gems""},""url"":""duel:push?GameMode=9"",""shortcut"":""Sub1""}],""contents"":[{""tp"":""H1"",""text"":{""en_US"":""To commemorate the game's release, we're currently having a special Gems sale!""}},{""tp"":""Text"",""text"":{""en_US"":""Check out the Purchase Gems page for an exclusive Gem Pack.nEach player can purchase up to 3.nThis is a deal you do not want to miss!""},""indent"":-1},{""tp"":""Spacer"",""size"":""M"",""indent"":-1},{""tp"":""Text"",""text"":{""en_US"":""<color=#00D2FF>*You can also go to the Purchase Gems page by pressing the Gem icon in the center of the top of the Home screen.</color>""},""indent"":-1}]}" },
                        { "banner", new Dictionary<string, object>() {
                            { "image", "Images/Notification/Shop/Gem001" },
                            //{ "image", "" },
                            { "pattern", TopicsBannerPatern.GEM },
                            //{ "image_text", "" }
                            { "image_text", new string[] {// Either null or 4 entries
                                "Sale underway!\nDon't miss out!",
                                "Jan 19 01:00 - Mar 31 04:59",
                                "",
                                ""
                            }},
                            { "is_coming_soon", false }
                        }},
                    }*/
                };
            }
            else
            {
                request.Response["Topics"] = new object[0];// Required
            }
            /*request.Response["Duelpass"] = new Dictionary<string, object>()
            {
                { "season_id", 1 },
                { "grade", 1 },
                { "percent", 0 },
                { "is_goldpass", false },
                { "received", new object[0] }
            };*/
            request.Remove(
                "Master.CardRare",
                "Master.CardCr",
                "Master.Structure",
                "Master.Tournament",
                "Master.Exhibition",
                "Master.Regulation",
                "Master.RegulationIcon",
                "Master.PeriodItem",
                "Master.Duelpass",
                "Regulation",
                "Deck",
                "DeckList",
                "TDeck",
                "TDeckList",
                "EXHDeck",
                "EXHDeckList",
                "Topic",
                "Notification",
                "Duelpass",
                "Room.rule_list",
                "Room.holding_rule_list",
                "Room.common");
        }

        void Act_EventNotifyGetList(GameServerWebRequest request)
        {
            // All support "!" and some support displaying a number
            // 0 = invalid
            // 1 = missions (!/num)
            // 2 = friend (!/num)
            // 3 = duel (!)
            // 4 = shop (!)
            // 5 = gift box (!/num)
            // 6 = notifications (!/num)
            // 7 = solo (!)
            const int friendType = 2;
            const int shopType = 4;
            Dictionary<string, object> notificationBadges = new Dictionary<string, object>();
            if (request.Player.ShopState.HasNew())
            {
                notificationBadges[shopType.ToString()] = new Dictionary<string, object>()
                {
                    { "type", shopType },
                    { "num", 0 }
                };
            }
            lock (request.Player.DuelRoomInvitesByFriendId)
            {
                if (request.Player.DuelRoomInvitesByFriendId.Count > 0)
                {
                    notificationBadges[friendType.ToString()] = new Dictionary<string, object>()
                    {
                        { "type", friendType },
                        { "num", request.Player.DuelRoomInvitesByFriendId.Count }
                    };
                }
            }
            request.Response["EventNotify"] = new Dictionary<string, object>()
            {
                { "Notify", new object[0] },// These are popup notifications on the bottom right of the screen
                { "Badge", notificationBadges }
            };
            request.Remove("EventNotify");
        }

        void Act_UserProfile(GameServerWebRequest request)
        {
            uint pcode = Utils.GetValue<uint>(request.ActParams, "pcode");
            if (pcode == 0 || pcode == request.Player.Code)
            {
                Dictionary<string, object> userData = request.GetOrCreateDictionary("User");
                Dictionary<string, object> profileData = Utils.GetOrCreateDictionary(userData, "profile");
                GetCommonProfileData(request.Player, profileData);

                // TODO: Item.sort { "1000012": { "category": 3, "item_id": 1000012, "sort_order": 7 }, ... }
            }
            else
            {
                Player player;
                lock (playersLock)
                {
                    playersById.TryGetValue(pcode, out player);
                }
                if (player != null)
                {
                    Dictionary<string, object> friendData = request.GetOrCreateDictionary("Friend");
                    Dictionary<string, object> profileData = Utils.GetOrCreateDictionary(friendData, "profile");
                    GetCommonProfileData(player, profileData);

                    FriendState friendState;
                    GetFriends(request.Player).TryGetValue(player.Code, out friendState);
                    profileData["is_follow"] = friendState.HasFlag(FriendState.Following);

                    profileData["is_block"] = false;
                    profileData["is_ps_block"] = false;
                    profileData["is_xbox_block"] = false;

                    request.Remove("Friend.profile");
                }
                else
                {
                    // TODO: Find correct error code to give
                    request.ResultCode = (int)ResultCodes.UserCode.ERR_ACCOUNT_NOT_EXIST;
                }
            }
        }

        void GetCommonProfileData(Player player, Dictionary<string, object> profileData)
        {
            Dictionary<uint, FriendState> friends = GetFriends(player);
            profileData["name"] = player.Name;
            profileData["rank"] = player.Rank;
            profileData["rate"] = player.Rate;
            profileData["level"] = player.Level;
            profileData["exp"] = player.Exp;
            //profileData["need_exp"] = player.NeedExp;
            profileData["icon_id"] = player.IconId;
            profileData["icon_frame_id"] = player.IconFrameId;
            profileData["avatar_id"] = player.AvatarId;
            profileData["wallpaper"] = player.Wallpaper;
            profileData["tag"] = player.TitleTags.ToArray();
            profileData["follow_num"] = friends.Count(x => x.Value.HasFlag(FriendState.Following));
            profileData["follower_num"] = friends.Count(x => x.Value.HasFlag(FriendState.Follower));
        }

        void Act_UserRecord(GameServerWebRequest request)
        {
            // User data / stats page. We just create an empty dictionary as otherwise the client bugs out
            Dictionary<string, object> userData = request.GetOrCreateDictionary("User");
            Utils.GetOrCreateDictionary(userData, "record");
        }
    }
}
