using InnerNet;
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static TOHE.Translator;

namespace TOHE;

public static class BanManager
{
    private static readonly string DENY_NAME_LIST_PATH = @"./TOHE-DATA/DenyName.txt";
    private static readonly string BAN_LIST_PATH = @"./TOHE-DATA/BanList.txt";
    private static readonly string MODERATOR_LIST_PATH = @"./TOHE-DATA/Moderators.txt";
    private static readonly string VIP_LIST_PATH = @"./TOHE-DATA/VIP-List.txt";
    private static readonly string WHITE_LIST_LIST_PATH = @"./TOHE-DATA/WhiteList.txt";
    //private static List<string> EACList = []; // Don't make it read-only
    public static List<string> TempBanWhiteList = []; //To prevent writing to ban list
    public static List<Dictionary<string, System.Text.Json.JsonElement>> EACDict = [];
    public static void Init()
    {
        try
        {
            Directory.CreateDirectory("TOHE-DATA");

            if (!File.Exists(BAN_LIST_PATH))
            {
                Logger.Warn("Create a new BanList.txt file", "BanManager");
                File.Create(BAN_LIST_PATH).Close();
            }
            if (!File.Exists(DENY_NAME_LIST_PATH))
            {
                Logger.Warn("Create a new DenyName.txt file", "BanManager");
                File.Create(DENY_NAME_LIST_PATH).Close();
                File.WriteAllText(DENY_NAME_LIST_PATH, GetResourcesTxt("TOHE.Resources.Config.DenyName.txt"));
            }
            if (!File.Exists(MODERATOR_LIST_PATH))
            {
                Logger.Warn("Creating a new Moderators.txt file", "BanManager");
                File.Create(MODERATOR_LIST_PATH).Close();
            }
            if (!File.Exists(VIP_LIST_PATH))
            {
                Logger.Warn("Creating a new VIP-List.txt file", "BanManager");
                File.Create(VIP_LIST_PATH).Close();
            }
            if (!File.Exists(WHITE_LIST_LIST_PATH))
            {
                Logger.Warn("Creating a new WhiteList.txt file", "BanManager");
                File.Create(WHITE_LIST_LIST_PATH).Close();
            }

            // Read EAC List
            //var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TOHE.Resources.Config.EACList.txt");
            //stream.Position = 0;
            //using StreamReader sr = new(stream, Encoding.UTF8);
            //string line;
            //while ((line = sr.ReadLine()) != null)
            //{
            //    if (line == "" || line.StartsWith("#")) continue;
            //    EACList.Add(line);
            //}

        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "BanManager");
        }
    }
    private static string GetResourcesTxt(string path)
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
        stream.Position = 0;
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
    public static string GetHashedPuid(this ClientData player)
    {
        if (player == null) return "";
        string puid = player.ProductUserId;
        using SHA256 sha256 = SHA256.Create();
        
        // get sha-256 hash
        byte[] sha256Bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(puid));
        string sha256Hash = BitConverter.ToString(sha256Bytes).Replace("-", "").ToLower();

        // pick front 5 and last 4
        return string.Concat(sha256Hash.AsSpan(0, 5), sha256Hash.AsSpan(sha256Hash.Length - 4));
    }
    public static void AddBanPlayer(InnerNet.ClientData player)
    {
        if (!AmongUsClient.Instance.AmHost || player == null) return;
        if (!CheckBanList(player?.FriendCode, player?.GetHashedPuid()) && !TempBanWhiteList.Contains(player?.GetHashedPuid()))
        {
            if (player?.GetHashedPuid() != "" && player?.GetHashedPuid() != null && player?.GetHashedPuid() != "e3b0cb855")
            {
                var additionalInfo = "";
                if (CheckEACList(player?.FriendCode, player?.GetHashedPuid())) additionalInfo = " //added by EAC";
                File.AppendAllText(BAN_LIST_PATH, $"{player?.FriendCode},{player?.GetHashedPuid()},{player.PlayerName.RemoveHtmlTags()}{additionalInfo}\n");
                Logger.SendInGame(string.Format(GetString("Message.AddedPlayerToBanList"), player.PlayerName));
            }
            else Logger.Info($"Failed to add player {player?.PlayerName.RemoveHtmlTags()}/{player?.FriendCode}/{player?.GetHashedPuid()} to ban list!", "AddBanPlayer");
        }
    }
    
    public static bool CheckDenyNamePlayer(PlayerControl player, string name)
    {
        if (!AmongUsClient.Instance.AmHost || !Options.ApplyDenyNameList.GetBool()) return false;

        try
        {
            Directory.CreateDirectory("TOHE-DATA");
            if (!File.Exists(DENY_NAME_LIST_PATH)) File.Create(DENY_NAME_LIST_PATH).Close();
            using StreamReader sr = new(DENY_NAME_LIST_PATH);
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (line == "") continue;
                if (line.Contains("Amogus"))
                {
                    AmongUsClient.Instance.KickPlayer(player.OwnerId, false);
                    Logger.SendInGame(string.Format(GetString("Message.KickedByDenyName"), name, line));
                    Logger.Info($"{name}は名前が「{line}」に一致したためキックされました。", "Kick");
                    return true;
                }
                if (line.Contains("Amogus V"))
                {
                    AmongUsClient.Instance.KickPlayer(player.OwnerId, false);
                    Logger.SendInGame(string.Format(GetString("Message.KickedByDenyName"), name, line));
                    Logger.Info($"{name}は名前が「{line}」に一致したためキックされました。", "Kick");
                    return true;
                }

                if (Regex.IsMatch(name, line))
                {
                    AmongUsClient.Instance.KickPlayer(player.OwnerId, false);
                    Logger.SendInGame(string.Format(GetString("Message.KickedByDenyName"), name, line));
                    Logger.Info($"{name}は名前が「{line}」に一致したためキックされました。", "Kick");
                    return true;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "CheckDenyNamePlayer");
            return true;
        }
    }
    public static void CheckBanPlayer(InnerNet.ClientData player)
    {
        if (!AmongUsClient.Instance.AmHost || !Options.ApplyBanList.GetBool()) return;

        string friendcode = player?.FriendCode;

        if (CheckBanList(friendcode, player?.GetHashedPuid()))
        {
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            Logger.SendInGame(string.Format(GetString("Message.BannedByBanList"), player.PlayerName));
            Logger.Info($"{player.PlayerName}は過去にBAN済みのためBANされました。", "BAN");
            return;
        }
        if (CheckEACList(friendcode, player?.GetHashedPuid()))
        {
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            Logger.SendInGame(string.Format(GetString("Message.BannedByEACList"), player.PlayerName));
            Logger.Info($"{player.PlayerName}存在于EAC封禁名单", "BAN");
            return;
        }
        if (TempBanWhiteList.Contains(player?.GetHashedPuid()))
        {
            AmongUsClient.Instance.KickPlayer(player.Id, true);
            //This should not happen
            Logger.Info($"{player.PlayerName} was in temp ban list", "BAN");
            return;
        }
    }
    public static bool CheckBanList(string code, string hashedpuid = "")
    {
        bool OnlyCheckPuid = false;
        if (code == "" && hashedpuid != "") OnlyCheckPuid = true;
        else if (code == "") return false;

        string noDiscrim = "";
        if (code.Contains('#'))
        {
            int index = code.IndexOf('#');
            noDiscrim = code[..index];
        }

        try
        {
            Directory.CreateDirectory("TOHE-DATA");
            if (!File.Exists(BAN_LIST_PATH)) File.Create(BAN_LIST_PATH).Close();
            using StreamReader sr = new(BAN_LIST_PATH);
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (line == "") continue;
                if (!OnlyCheckPuid)
                {
                    if (line.Contains(code)) return true;
                    if (!string.IsNullOrEmpty(noDiscrim) && !line.Contains('#') && line.Contains(noDiscrim)) return true;
                }
                if (line.Contains(hashedpuid)) return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "CheckBanList");
        }
        return false;
    }
    public static bool CheckEACList(string code, string hashedPuid)
    {
        var splitCode = code.Split("#")[0].ToLower().Trim();
        if (string.IsNullOrEmpty(splitCode) && string.IsNullOrEmpty(hashedPuid)) return false;

        foreach (var user in EACDict)
        {
            var splitUser = user["friendcode"].ToString().Split('#')[0].ToLower().Trim();

            if ((!string.IsNullOrEmpty(splitCode) && (splitCode == splitUser))
                || (user["hashPUID"].ToString().ToLower().Trim() == hashedPuid.ToLower().Trim()))
            {
                Logger.Warn($"friendcode : {code}, hashedPUID : {hashedPuid} banned by EAC reason : {user["reason"]}", "CheckEACList");
                return true;
            }
        }

        return false;
        //bool OnlyCheckPuid = false;
        //if (code == "" && hashedPuid == "") OnlyCheckPuid = true;
        //else if (code == "") return false;
        //return (EACList.Any(x => x.Contains(code) && !OnlyCheckPuid) || EACList.Any(x => x.Contains(hashedPuid) && hashedPuid != ""));
    }
    public static bool CheckAllowList(string friendcode)
    {
        if (friendcode == "") return false;
        var allowListFilePath = @"./TOHE-DATA/WhiteList.txt";
        if (!File.Exists(allowListFilePath)) File.Create(allowListFilePath).Close();
        var friendcodes = File.ReadAllLines(allowListFilePath);
        return friendcodes.Any(x => x == friendcode || x.Contains(friendcode));
    }
}
[HarmonyPatch(typeof(BanMenu), nameof(BanMenu.Select))]
class BanMenuSelectPatch
{
    public static void Postfix(BanMenu __instance, int clientId)
    {
        InnerNet.ClientData recentClient = AmongUsClient.Instance.GetRecentClient(clientId);
        if (recentClient == null) return;
        if (!BanManager.CheckBanList(recentClient?.FriendCode, recentClient?.GetHashedPuid())) __instance.BanButton.GetComponent<ButtonRolloverHandler>().SetEnabledColors();
    }
}