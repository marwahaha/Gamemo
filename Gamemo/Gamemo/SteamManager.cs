﻿using Fluxter.SteamWebAPI;
using Fluxter.SteamWebAPI.Interfaces.General.IPlayerService.GetOwnedGames;
using Fluxter.SteamWebAPI.Interfaces.General.ISteamApps.GetAppList;
using Fluxter.SteamWebAPI.Interfaces.General.ISteamUserStats.GetPlayerAchievements;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Gamemo
{
    public static class SteamManager
    {
        private static SteamIdentity User;
        private static long SteamID;
        private static List<App> AppList;
        public static bool IsSteamProfile = true;

        public static void Init(long steamID) {
            if (steamID == 0) {
                IsSteamProfile = false;
                return;
            }

            SteamWebAPI.SetGlobalKey("1E5E3956484C372C2D9AE6D58EFA4F69");
            var appListResponse = SteamWebAPI.General().ISteamApps().GetAppList().GetResponse();
            AppList = appListResponse.Data.Apps;
            SteamID = steamID;
            User = SteamIdentity.FromSteamID(steamID);
        }

        public static void SetSteamUser(long steamID) {
            SteamID = steamID;
            User = SteamIdentity.FromSteamID(steamID);
        }

        public static List<Game> GetOwnedGames() {
            List<Game> gamesList = new List<Game>();

            var gameListResponse = SteamWebAPI.General().IPlayerService().GetOwnedGames(User).GetResponse();
            List<GetOwnedGamesResponseGame> gameList = gameListResponse.Data.Games;
            foreach (GetOwnedGamesResponseGame g in gameList) {
                string gameName = AppList.Find(x => x.AppID == g.AppID).Name;
                if (gameName != null) {
                    gamesList.Add(new Game(g.AppID, gameName));
                }
            }

            return gamesList;
        }

        public static Dictionary<string, float> GetGlobalAchievePercentagesByHTML(int appID) {
            Dictionary<string, float> globalAchievePerc = new Dictionary<string, float>();
            string url = @"https://steamcommunity.com/stats/"+ appID +"/achievements/";
            HtmlWeb web = new HtmlWeb();
            var htmlDoc = web.Load(url);
            var nodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class,'achieveRow')]");

            foreach (var achieve in nodes) {
                string percentText = achieve.Descendants("div").Where(x => x.HasClass("achievePercent")).First().InnerHtml;
                float percent = float.Parse(percentText.Remove(percentText.Length - 1));
                string name = achieve.Descendants("h3").First().InnerHtml;
                globalAchievePerc[name] = percent;
            }

            return globalAchievePerc;
        }

        public static List<Achievement> GetGameAchievements(int appID){
            Dictionary<string, float> globalAchievePerc = GetGlobalAchievePercentagesByHTML(appID);
            try {
                return GetAchieveByXML(appID, globalAchievePerc);
            } catch {
                return GetAchieveByHTML(appID, globalAchievePerc); ;
            }
        }

        private static List<Achievement> GetAchieveByHTML(int appID, Dictionary<string, float> globalAchievePerc) {
            List<Achievement> achievementList = new List<Achievement>();
            var html = @"https://steamcommunity.com/profiles/" + SteamID + "/stats/" + appID;
            HtmlWeb web = new HtmlWeb();
            var htmlDoc = web.Load(html);
            var nodes = htmlDoc.DocumentNode.SelectNodes("//html[1]/body[1]/div[1]/div[7]/div[2]/div[1]/div[2]/div[1]/div[4]");

            if (nodes != null && nodes.First().Id == "personalAchieve") {
                foreach (var achieve in nodes.First().Elements("div")) {
                    if (achieve.Descendants("div").Where(x => x.HasClass("achieveUnlockTime")).Count() == 1) {
                        continue;
                    }
                    if (achieve.Descendants("img").Count() != 0) {
                        var img = achieve.Descendants("img").First().Attributes["src"].Value;
                        var name = achieve.Descendants("h3").First().InnerHtml;
                        var desc = achieve.Descendants("h5").First().InnerHtml;
                        achievementList.Add(new Achievement(img, name, desc, globalAchievePerc[name]));
                    }
                }
            }

            return achievementList;
        }

        private static List<Achievement> GetAchieveByXML(int appID, Dictionary<string, float> globalAchievePerc)
        {
            List<Achievement> achievementList = new List<Achievement>();
            String URLString = "https://steamcommunity.com/profiles/" + SteamID + "/stats/" + appID + "/achievements/?xml=1";
            XmlTextReader reader = new XmlTextReader(URLString);
            bool readingAchievement = false;
            Achievement achievement = new Achievement();

            while (reader.Read()) {
                switch (reader.NodeType) {
                    case XmlNodeType.Element: // The node is an element.
                        if (readingAchievement) {
                            if (reader.Name == "iconClosed") {
                                achievement.Icon = reader.ReadString();
                            } else if (reader.Name == "name") {
                                achievement.Name = reader.ReadString();
                            } else if (reader.Name == "description") {
                                achievement.Description = reader.ReadString();
                            }
                        } else if (reader.Name == "achievement") {
                            while (reader.MoveToNextAttribute()) { // Read the attributes.
                                if (reader.Name == "closed" && reader.Value == "0") {
                                    readingAchievement = true;
                                    achievement = new Achievement();
                                }
                            }
                        }
                        break;
                    case XmlNodeType.EndElement: //Display the end of the element.
                        if (reader.Name == "achievement" && readingAchievement) {
                            readingAchievement = false;
                            achievement.GlobalPercentage = globalAchievePerc[achievement.Name];
                            achievementList.Add(achievement);
                        }
                        break;
                }
            }

            return achievementList;
        }
    }
}
