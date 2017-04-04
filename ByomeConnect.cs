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
     * Plugin Config Stuff
     */
    protected override void LoadDefaultConfig() {
      PrintWarning("Creating default ByomeConnect config file.");
      Config.Set("databaseURL", "https://us-central1-YOUR-FIREBASE-APP.cloudfunctions.net");
      Config.Set("apiKey", "your_api_key_here");
      Config.Set("serverId", "server_id_here");
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



    /**
     * Server Hooks
     */
    void OnServerInitialized() {
      var requestObject = new Dictionary<string, string> {
        { "apiKey", Convert.ToString(Config.Get("apiKey")) },
        { "event", "server_online" },
        { "serverId", Convert.ToString(Config.Get("serverId")) },
      };
      postRequest("serverOnline", JsonConvert.SerializeObject(requestObject));
    }

    bool OnServerMessage(string message, string name, string color, ulong id) {
      // Don't announce when player gets items from server.
      if (name == "SERVER" && String.Compare(message, 0, "SERVER gave", 0, 11, false) == 1) {
        return true;
      }
      return false;
    }

    void OnPlayerInit(BasePlayer player) {
      var playerObject = new Dictionary<string, string> {
        { "apiKey", Convert.ToString(Config.Get("apiKey")) },
        { "event", "player_connected" },
        { "serverId", Convert.ToString(Config.Get("serverId")) },
        { "playerId", player.UserIDString },
        { "playerName", player.displayName },
        { "playerIpAddress", Convert.ToString(player.net.connection.ipaddress) }
      };
      postRequest("playerConnected", JsonConvert.SerializeObject(playerObject));
    }

    void OnPlayerDisconnected(BasePlayer player, string reason) {
      var playerObject = new Dictionary<string, string> {
        { "apiKey", Convert.ToString(Config.Get("apiKey")) },
        { "event", "player_disconnected" },
        { "serverId", Convert.ToString(Config.Get("serverId")) },
        { "playerId", player.UserIDString },
        { "reason", reason },
      };
      postRequest("playerDisconnected", JsonConvert.SerializeObject(playerObject));
    }

    void OnPlayerSleep(BasePlayer player) {
      var playerObject = new Dictionary<string, string> {
        { "apiKey", Convert.ToString(Config.Get("apiKey")) },
        { "event", "player_sleep" },
        { "serverId", Convert.ToString(Config.Get("serverId")) },
        { "playerId", player.UserIDString },
      };
      postRequest("playerSleep", JsonConvert.SerializeObject(playerObject));
    }

    void OnPlayerSleepEnded(BasePlayer player) {
      var playerObject = new Dictionary<string, string> {
        { "apiKey", Convert.ToString(Config.Get("apiKey")) },
        { "event", "player_sleep_ended" },
        { "serverId", Convert.ToString(Config.Get("serverId")) },
        { "playerId", player.UserIDString },
      };
      postRequest("playerSleepEnded", JsonConvert.SerializeObject(playerObject));
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
        SendReply(player, "Kit has been redeemed!");
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
  }
}
