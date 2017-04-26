using System;
using System.Collections.Generic;

using Newtonsoft.Json;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using Oxide.Game.Rust.Libraries.Covalence;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins {
  [Info("ByomeConnect", "byome, inc", "0.0.1")]
  [Description("Send and receive information to byome.io")]

  class ByomeConnect : RustPlugin {

    /**
     * Plugins
     */
    [PluginReference] Plugin Slack;

    /**
     * Config
     */
    protected override void LoadDefaultConfig() {
      PrintWarning("Creating default ByomeConnect config file.");
      Config.Set("databaseURL", "https://us-central1-YOUR-FIREBASE-APP.cloudfunctions.net");
      Config.Set("apiKey", "your_api_key_here");
      Config.Set("serverId", "server_id_here");
      Config.Set("serverName", "Server Name Here");
      Config.Set("SserverSlug", "server-slug-here");
      SaveConfig();
    }

    void Init() {

    }



    /**
     * Firebase requests and connection methods
     */
    private string serverEndpoint() {
      return "servers" + Config["serverId"] + "/";
    }

    private string requestEndpoint(string path) {
      return Config["databaseURL"] + "/" + path;
    }

    private void postRequest(string path, string body) {
      var headers = new Dictionary<string, string> { { "Content-Type", "application/json" } };
      webrequest.EnqueuePost(requestEndpoint(path), body, (code, response) => postRequestCallback(code, response), this, headers);
    }

    private void postRequestCallback(int code, string response) {
      if (response == null || code != 200) {
        Puts($"Error: {code} - {response}");
        return;
      }
    }

    private void sendRequest(Dictionary<String, String> req, String apiEvent, String apiEndpoint) {
      req.Add("apiKey", Convert.ToString(Config.Get("apiKey")));
      req.Add("serverId", Convert.ToString(Config.Get("serverId")));
      req.Add("event", apiEvent);
      postRequest(apiEndpoint, JsonConvert.SerializeObject(req));
    }

    private void notifySlack(String message) {
      String serverSlug = Convert.ToString(Config.Get("serverSlug"));
      Slack?.Call("Message", message, serverSlug);
    }



    /**
     * Server Hooks
     */
    void OnServerInitialized() {
      sendRequest(new Dictionary<String, String>(), "server_online", "serverOnline");
    }

    bool OnServerMessage(string message, string name, string color, ulong id) {
      // Don't announce when player gets items from server.
      if (name == "SERVER" && String.Compare(message, 0, "SERVER gave", 0, 11, false) == 1) {
        return true;
      }
      Slack?.Call("Message", message, Convert.ToString(Config.Get("serverSlug")));
      return false;
    }

    void OnPlayerInit(BasePlayer player) {
      var req = new Dictionary<string, string> {
        { "playerId", player.UserIDString },
        { "playerName", player.displayName },
        { "playerIpAddress", Convert.ToString(player.net.connection.ipaddress) }
      };
      sendRequest(req, "player_connected", "playerConnected");
      notifySlack($"Player connected: {player.displayName}");
    }

    void OnPlayerDisconnected(BasePlayer player, string reason) {
      var req = new Dictionary<string, string> {
        { "playerId", player.UserIDString },
        { "reason", reason },
      };
      sendRequest(req, "player_disconnected", "playerDisconnected");
      notifySlack($"Player disconnected: {player.displayName}");
    }

    void OnPlayerSleep(BasePlayer player) {
      var req = new Dictionary<string, string> {
        { "playerId", player.UserIDString },
      };
      sendRequest(req, "player_sleep", "playerSleep");
    }

    void OnPlayerSleepEnded(BasePlayer player) {
      var req = new Dictionary<string, string> {
        { "playerId", player.UserIDString },
      };
      sendRequest(req, "player_sleep_ended", "playerSleepEnded");
    }


    /**
     * Link Account
     */
    void linkAccountCallback(int code, string response, BasePlayer player) {
      if (response == null || code != 200) {
        Puts($"Error: {code} - {response}");
        SendReply(player, "Failed to link accounts. Please make sure your association code is correct.");
      } else {
        SendReply(player, "Accounts successfully linked! Your player is now linked on all byome servers");
        Server.Command($"inventory.giveto {player.UserIDString} supply.signal 2");
      }
    }

    [ChatCommand("associate")]
    void LinkAccount(BasePlayer player, string command, string[] args) {
      SendReply(player, "Attempting to link accounts...");
      var requestObject = new Dictionary<string, string> {
        { "apiKey", Convert.ToString(Config.Get("apiKey")) },
        { "event", "link_account" },
        { "playerId", player.UserIDString },
        { "associationToken", args[0] }
      };
      var headers = new Dictionary<string, string> { { "Content-Type", "application/json" } };
      webrequest.EnqueuePost(
        requestEndpoint("linkAccount"),
        JsonConvert.SerializeObject(requestObject),
        (code, response) => linkAccountCallback(code, response, player),
        this,
        headers
      );
    }


    /**
     * Byome Kit
     */
    void byomeKitCallback(int code, string response, BasePlayer player, string kit) {
      if (response == null || code != 200) {
        Puts($"Error: {code} - {response}");
        SendReply(player, "Failed to redeem kit. Please ensure correct spelling, and that you are able to redeem the kit.");
      } else {
        SendReply(player, "Kit has been redeemed! Type \"/byomekit (kitslughere)\" in chat to use.");
        Server.Command($"inv.giveplayer {player.UserIDString} {kit} 1");
      }
    }

    [ChatCommand("byomekit")]
    void ByomeKit(BasePlayer player, string command, string[] args) {
      SendReply(player, "Your requested kit will be here shortly!");
      var requestObject = new Dictionary<string, string> {
        { "apiKey", Convert.ToString(Config.Get("apiKey")) },
        { "event", "redeem_kit" },
        { "playerId", player.UserIDString },
        { "serverId", Convert.ToString(Config.Get("serverId")) },
        { "kitId", args[0] }
      };
      var headers = new Dictionary<string, string> { { "Content-Type", "application/json" } };
      webrequest.EnqueuePost(
        requestEndpoint("redeemKit"),
        JsonConvert.SerializeObject(requestObject),
        (code, response) => byomeKitCallback(code, response, player, args[0]),
        this,
        headers
      );
    }


    /**
     * Messaging
     */
    void OnPlayerDie(BasePlayer player, HitInfo info) {
      var req = new Dictionary<string, string> {
        { "playerId", player.UserIDString },
        { "perpetratorId", info.InitiatorPlayer.UserIDString },
      };
      sendRequest(req, "player_death", "playerDeath");
      notifySlack($"Player killed: {player.displayName}");
    }

    void OnPlayerChat(ConsoleSystem.Arg arg) {
      BasePlayer player = (BasePlayer)arg.Connection.player;
      string message = arg.GetString(0, "text");
      var req = new Dictionary<string, string> {
        { "playerId", player.UserIDString },
        { "content", message }
      };
      sendRequest(req, "player_chat", "playerChat");
      notifySlack($"{player.displayName}: {message}");
    }

    // void OnCollectiblePickup(Item item, BasePlayer player) {
    //   var req = new Dictionary<string, string> {
    //     { "playerId", player.UserIDString },
    //     { "item", item.name }
    //   };
    //   sendRequest(req, "player_collectible_pickup", "playerCollectiblePickup");
    // }
  }
}
