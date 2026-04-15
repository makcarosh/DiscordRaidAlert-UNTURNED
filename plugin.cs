using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Rocket.API;
using Rocket.API.Collections;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Logger = Rocket.Core.Logging.Logger;
using Color = UnityEngine.Color;

namespace DiscordRaidAlerts
{
    public class Config : IRocketPluginConfiguration
    {
        public string DiscordBotToken;
        public string ServerName;
        public string DiscordWebHook;

        public void LoadDefaults()
        {
            DiscordBotToken = "YOUR_DISCORD_BOT_TOKEN";
            ServerName = "My Unturned Server";
            DiscordWebHook = "https://discord.com/api/webhooks/...";
        }
    }

    [XmlRoot("PlayersData")]
    public class PlayersData
    {
        [XmlElement("Player")]
        public List<PlayerEntry> Players { get; set; } = new List<PlayerEntry>();

        public class PlayerEntry
        {
            [XmlAttribute("SteamID")]
            public ulong SteamID { get; set; }

            [XmlAttribute("DiscordID")]
            public ulong DiscordID { get; set; }
        }
    }

    public class CommandSetDiscord : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "setdiscord";
        public string Help => "Привязывает ваш Discord ID для получения уведомлений о рейдах.";
        public string Syntax => "<discord_id>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "setdiscord" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var player = (UnturnedPlayer)caller;

            if (command.Length != 1)
            {
                UnturnedChat.Say(player, "Использование: /setdiscord <discord_id>", Color.red);
                return;
            }

            if (!ulong.TryParse(command[0], out ulong discordId))
            {
                UnturnedChat.Say(player, "Discord ID должен быть числом, например: 123456789012345678", Color.red);
                return;
            }

            Plugin.Instance.SetPlayerDiscord(player.CSteamID.m_SteamID, discordId);
            UnturnedChat.Say(player, $"Ваш Discord ID {discordId} успешно привязан.", Color.green);
        }
    }

    public class CommandRemoveDiscord : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "removediscord";
        public string Help => "Отключает уведомления о рейдах, удаляя привязку Discord.";
        public string Syntax => "";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "removediscord" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var player = (UnturnedPlayer)caller;

            if (Plugin.Instance.RemovePlayerDiscord(player.CSteamID.m_SteamID))
            {
                UnturnedChat.Say(player, "Уведомления отключены. Ваш Discord ID удалён.", Color.green);
            }
            else
            {
                UnturnedChat.Say(player, "У вас не была привязана учётная запись Discord.", Color.yellow);
            }
        }
    }

    public class Plugin : RocketPlugin<Config>
    {
        public static Plugin Instance { get; private set; }

        private PlayersData _playersData;
        private string _dataFilePath;

        public override TranslationList DefaultTranslations => new TranslationList
        {
            { "message", "**Постройка уничтожена!**\n**Ближайшая локация:** {7}\n**Сервер:** {8}\n" },
            { "messageToServer", "**Постройка уничтожена!**\n**Владелец:** [{0}]({1})\n**Уничтожил:** [{2}]({3})\n**Оружие:** {4}\n**Постройка:** {5} (ID: {6})\n**Ближайшая локация:** {7}\n**Сервер:** {8}\n" }
        };

        protected override void Load()
        {
            Instance = this;

            _dataFilePath = Path.Combine(Directory, "players_data.xml");
            LoadPlayersData();

            BarricadeManager.onDamageBarricadeRequested += OnDamageBarricadeRequested;
            StructureManager.onDamageStructureRequested += OnDamageStructureRequested;

            Logger.Log("DiscordRaidAlerts is load!\nDeveloper discord: makcarosh");
        }

        protected override void Unload()
        {
            BarricadeManager.onDamageBarricadeRequested -= OnDamageBarricadeRequested;
            StructureManager.onDamageStructureRequested -= OnDamageStructureRequested;

            SavePlayersData();

            Logger.Log("DiscordRaidAlerts is unload!\nDeveloper discord: makcarosh");
        }

        private void LoadPlayersData()
        {
            if (File.Exists(_dataFilePath))
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(PlayersData));
                    using (FileStream fs = new FileStream(_dataFilePath, FileMode.Open))
                    {
                        _playersData = (PlayersData)serializer.Deserialize(fs);
                    }

                    if (_playersData == null)
                        _playersData = new PlayersData();

                    Logger.Log($"Загружено {_playersData.Players.Count} записей игроков.");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Ошибка загрузки players_data.xml: {ex}");
                    _playersData = new PlayersData();
                }
            }
            else
            {
                _playersData = new PlayersData();
                Logger.Log("Файл players_data.xml не найден, создаём новый.");
                SavePlayersData();
            }
        }

        private void SavePlayersData()
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(PlayersData));
                using (FileStream fs = new FileStream(_dataFilePath, FileMode.Create))
                {
                    serializer.Serialize(fs, _playersData);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка сохранения players_data.xml: {ex}");
            }
        }

        public void SetPlayerDiscord(ulong steamId, ulong discordId)
        {
            var entry = _playersData.Players.FirstOrDefault(p => p.SteamID == steamId);

            if (entry != null)
            {
                entry.DiscordID = discordId;
            }
            else
            {
                _playersData.Players.Add(new PlayersData.PlayerEntry
                {
                    SteamID = steamId,
                    DiscordID = discordId
                });
            }

            SavePlayersData();
        }

        public bool RemovePlayerDiscord(ulong steamId)
        {
            var entry = _playersData.Players.FirstOrDefault(p => p.SteamID == steamId);
            if (entry != null)
            {
                _playersData.Players.Remove(entry);
                SavePlayersData();
                return true;
            }

            return false;
        }

        private ulong? GetPlayerDiscord(ulong steamId)
        {
            var entry = _playersData.Players.FirstOrDefault(p => p.SteamID == steamId);
            return entry?.DiscordID;
        }

        private string GetNearestLocation(Vector3 position)
        {
            var nodeSystem = LocationDevkitNodeSystem.Get();
            if (nodeSystem == null)
                return "неизвестно";

            var nodes = nodeSystem.GetAllNodes();
            if (nodes == null || nodes.Count == 0)
                return "неизвестно";

            LocationDevkitNode nearestNode = null;
            float minDistanceSqr = float.MaxValue;

            foreach (var node in nodes)
            {
                float distanceSqr = (node.transform.position - position).sqrMagnitude;
                if (distanceSqr < minDistanceSqr)
                {
                    minDistanceSqr = distanceSqr;
                    nearestNode = node;
                }
            }

            return nearestNode?.locationName ?? "неизвестно";
        }
        
        private void OnDamageBarricadeRequested(
            CSteamID instigatorSteamID,
            Transform barricadeTransform,
            ref ushort pendingTotalDamage,
            ref bool shouldAllow,
            EDamageOrigin damageOrigin)
        {

            if (!shouldAllow || barricadeTransform == null)
                return;

            BarricadeDrop barricade = BarricadeManager.FindBarricadeByRootTransform(barricadeTransform);
            if (barricade == null || barricade.asset.type != EItemType.FARM || barricade.asset.type != EItemType.CHARGE || barricade.asset.type != EItemType.LIBRARY || barricade.asset.type != EItemType.TRAP)
                return;

            uint currentHealth = barricade.GetServersideData().barricade.health;

            if (pendingTotalDamage >= currentHealth)
            {
                CSteamID ownerID = new CSteamID(barricade.GetServersideData().owner);
                HandleDestroyed(barricade.asset, ownerID, instigatorSteamID, barricade.model.position);
            } 
        }

        private void OnDamageStructureRequested(
            CSteamID instigatorSteamID,
            Transform structureTransform,
            ref ushort pendingTotalDamage,
            ref bool shouldAllow,
            EDamageOrigin damageOrigin)
        {
            if (!shouldAllow || structureTransform == null)
                return;

            StructureDrop structure = StructureManager.FindStructureByRootTransform(structureTransform);
            if (structure == null)
                return;

            uint currentHealth = structure.GetServersideData().structure.health;

            if (pendingTotalDamage >= currentHealth)
            {
                CSteamID ownerID = new CSteamID(structure.GetServersideData().owner);
                HandleDestroyed(structure.asset, ownerID, instigatorSteamID, structure.model.position);
            }
        }

        private void HandleDestroyed(Asset asset, CSteamID ownerID, CSteamID instigatorID, Vector3 position)
        {
            try
            {
                if (ownerID == CSteamID.Nil)
                    return;

                ulong? discordId = GetPlayerDiscord(ownerID.m_SteamID);
                if (discordId == null)
                    return;

                string ownerName = ownerID.m_SteamID.ToString();
                string ownerProfile = $"https://steamcommunity.com/profiles/{ownerID.m_SteamID}";

                string destroyerName = "Unknown";
                string destroyerProfile = "Неизвестно";
                string weaponName = "Unknown";

                if (ownerID != CSteamID.Nil)
                {
                    var ownerPlayer = UnturnedPlayer.FromCSteamID(ownerID);
                    if (ownerPlayer != null)
                    {
                        ownerName = ownerPlayer.DisplayName;
                    }
                }

                if (instigatorID != CSteamID.Nil)
                {
                    var destroyerPlayer = UnturnedPlayer.FromCSteamID(instigatorID);
                    destroyerProfile = $"https://steamcommunity.com/profiles/{instigatorID.m_SteamID}";

                    if (destroyerPlayer != null)
                    {
                        destroyerName = destroyerPlayer.DisplayName;

                        var equipment = destroyerPlayer.Player?.equipment;
                        if (equipment?.asset != null)
                            weaponName = equipment.asset.itemName;
                    }
                    else
                    {
                        destroyerName = instigatorID.m_SteamID.ToString();
                    }
                }

                string buildName = asset != null ? asset.FriendlyName : "Unknown";
                string buildId = asset != null ? asset.id.ToString() : "Unknown";

                string locationInfo = GetNearestLocation(position);

                string message = Translate(
                    "message",
                    ownerName,
                    ownerProfile,
                    destroyerName,
                    destroyerProfile,
                    weaponName,
                    buildName,
                    buildId,
                    locationInfo,
                    Configuration.Instance.ServerName
                );

                string messageToServer = Translate(
                    "messageToServer",
                    ownerName,
                    ownerProfile,
                    destroyerName,
                    destroyerProfile,
                    weaponName,
                    buildName,
                    buildId,
                    locationInfo,
                    Configuration.Instance.ServerName
                );

                _ = SendDiscordMessage(discordId.Value, message, messageToServer);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка в HandleDestroyed: {ex}");
            }
        }

        private async Task SendDiscordMessage(ulong discordId, string message, string messageToServer)
        {
            try
            {
                string token = Configuration.Instance.DiscordBotToken;
                if (string.IsNullOrWhiteSpace(token))
                {
                    Logger.LogError("Discord токен не настроен.");
                    return;
                }

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bot", token);
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DiscordBot (RocketMod Plugin)");

                    string createChannelJson = $"{{\"recipient_id\":\"{discordId}\"}}";
                    using (var createChannelContent = new StringContent(createChannelJson, Encoding.UTF8, "application/json"))
                    {
                        HttpResponseMessage createChannelResponse =
                            await httpClient.PostAsync("https://discord.com/api/v9/users/@me/channels", createChannelContent);

                        string createChannelResult =
                            await createChannelResponse.Content.ReadAsStringAsync();

                        if (!createChannelResponse.IsSuccessStatusCode)
                        {
                            Logger.LogError($"Не удалось создать DM канал для {discordId}: {createChannelResponse.StatusCode} | {createChannelResult}");
                            return;
                        }

                        var jsonDoc = JObject.Parse(createChannelResult);
                        string channelId = jsonDoc["id"]?.Value<string>();

                        if (string.IsNullOrEmpty(channelId))
                        {
                            Logger.LogError($"Не удалось получить ID канала из ответа Discord для {discordId}. Ответ: {createChannelResult}");
                            return;
                        }
                        //------------
                        var messagePayload = new
                        {
                            content = message
                        };

                        string messageJson = JsonConvert.SerializeObject(messagePayload);

                        using (var messageContent = new StringContent(messageJson, Encoding.UTF8, "application/json"))
                        {
                            HttpResponseMessage sendMessageResponse =
                                await httpClient.PostAsync($"https://discord.com/api/v9/channels/{channelId}/messages", messageContent);

                            string sendMessageResult =
                                await sendMessageResponse.Content.ReadAsStringAsync();

                            if (!sendMessageResponse.IsSuccessStatusCode)
                            {
                                Logger.LogError($"Не удалось отправить сообщение в DM {discordId}: {sendMessageResponse.StatusCode} | {sendMessageResult}");
                                return;
                            }
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(Configuration.Instance.DiscordWebHook))
                {
                    Logger.LogWarning("Webhook не указан в конфиге.");
                    return;
                }

                using (var webhookClient = new HttpClient())
                {
                    var payload = new
                    {
                        content = messageToServer
                    };

                    string json = JsonConvert.SerializeObject(payload);
                    using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                    {
                        HttpResponseMessage response = await webhookClient.PostAsync(Configuration.Instance.DiscordWebHook, content);

                        if (response.IsSuccessStatusCode)
                        {
                            Logger.Log("Сообщение отправлено в WebHook.");
                        }
                        else
                        {
                            string responseText = await response.Content.ReadAsStringAsync();
                            Logger.LogWarning($"Ошибка отправки WebHook: {response.StatusCode} | {responseText}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка отправки в Discord: {ex}");
            }
        }
    }
}