using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using AmongUs.GameOptions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using IVR;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

[assembly: AssemblyFileVersion(Main.PluginVersion)]
[assembly: AssemblyInformationalVersion(Main.PluginVersion)]
[assembly: AssemblyVersion(Main.PluginVersion)]

namespace IVR;

[BepInPlugin(PluginGuid, "IVR", PluginVersion)]
[BepInIncompatibility("jp.ykundesu.supernewroles")]
[BepInIncompatibility("MalumMenu")]
[BepInProcess("Among Us.exe")]
public class Main : BasePlugin
{
    private const string DebugKeyHash = "c0fd562955ba56af3ae20d7ec9e64c664f0facecef4b3e366e109306adeae29d";
    private const string DebugKeySalt = "59687b";
    private const string PluginGuid = "com.0-fold.improvedvanillaroles";
    public const string PluginVersion = "0.0.1";
    public const string PluginDisplayVersion = "0.0.1";
    public const string ImpostorColor = "#ff1919";
    public const string CrewmateColor = "#8cffff";

    public const float MinSpeed = 0.0001f;

    // == プログラム設定 / Program Config ==
    public const string ModName = "IVR";
    public const string ModColor = "#00ffff";
    public const bool AllowPublicRoom = true;
    public const string ForkId = "IVR";
    public const string SupportedAUVersion = "2024.6.18";
    public static readonly Version Version = Version.Parse(PluginVersion);
    public static ManualLogSource Logger;
    public static bool HasArgumentException;
    public static string CredentialsText;

    public static Dictionary<byte, PlayerVersion> PlayerVersion = [];
    public static bool ChangedRole = false;
    public static OptionBackupData RealOptionsData;
    public static string HostRealName = string.Empty;
    public static Dictionary<byte, float> KillTimers = [];
    public static Dictionary<byte, PlayerState> PlayerStates = [];
    public static Dictionary<byte, string> AllPlayerNames = [];
    public static Dictionary<int, string> AllClientRealNames = [];
    public static Dictionary<(byte, byte), string> LastNotifyNames;
    public static Dictionary<byte, Color32> PlayerColors = [];
    public static Dictionary<byte, PlayerState.DeathReason> AfterMeetingDeathPlayers = [];
    public static Dictionary<CustomRoles, string> RoleColors;
    public static Dictionary<byte, CustomRoles> SetRoles = [];
    public static Dictionary<byte, List<CustomRoles>> SetAddOns = [];
    public static readonly Dictionary<int, Dictionary<CustomRoles, List<CustomRoles>>> AlwaysSpawnTogetherCombos = [];
    public static readonly Dictionary<int, Dictionary<CustomRoles, List<CustomRoles>>> NeverSpawnTogetherCombos = [];
    public static Dictionary<byte, string> LastAddOns = [];
    public static List<RoleBase> AllRoleClasses;
    public static float RefixCooldownDelay;
    public static bool ProcessShapeshifts = true;
    public static readonly Dictionary<byte, (long START_TIMESTAMP, int TOTALCD)> AbilityCD = [];
    public static Dictionary<byte, float> AbilityUseLimit = [];
    public static List<byte> DontCancelVoteList = [];
    public static HashSet<byte> ResetCamPlayerList = [];
    public static List<byte> WinnerList = [];
    public static List<string> WinnerNameList = [];
    public static List<int> ClientIdList = [];
    public static Dictionary<byte, float> AllPlayerKillCooldown = [];
    public static Dictionary<byte, Vent> LastEnteredVent = [];
    public static Dictionary<byte, Vector2> LastEnteredVentLocation = [];
    public static readonly List<(string MESSAGE, byte RECEIVER_ID, string TITLE)> MessagesToSend = [];
    public static bool IsChatCommand;
    public static bool DoBlockNameChange;
    public static int UpdateTime;
    public static bool NewLobby;
    public static readonly Dictionary<int, int> SayStartTimes = [];
    public static readonly Dictionary<int, int> SayBanwordsTimes = [];
    public static Dictionary<byte, float> AllPlayerSpeed = [];
    public static readonly Dictionary<byte, int> GuesserGuessed = [];
    public static bool HasJustStarted;
    public static int AliveImpostorCount;
    public static Dictionary<byte, bool> CheckShapeshift = [];
    public static Dictionary<byte, byte> ShapeshiftTarget = [];
    public static bool VisibleTasksCount;
    public static string NickName = "";
    public static bool IntroDestroyed;
    public static float DefaultCrewmateVision;
    public static float DefaultImpostorVision;
    public static readonly bool IsAprilFools = DateTime.Now.Month == 4 && DateTime.Now.Day is 1;
    public static bool ResetOptions = true;
    public static string FirstDied = string.Empty;
    public static string ShieldPlayer = string.Empty;

    public static List<PlayerControl> LoversPlayers = [];
    public static bool IsLoversDead = true;
    public static List<byte> CyberStarDead = [];
    public static List<byte> BaitAlive = [];
    public static Dictionary<byte, int> KilledDiseased = [];
    public static Dictionary<byte, int> KilledAntidote = [];
    public static List<byte> BrakarVoteFor = [];
    public static Dictionary<byte, string> SleuthMsgs = [];
    public static int MadmateNum;

    public static Main Instance;


    public static string OverrideWelcomeMsg = string.Empty;
    public static int HostClientId;

    public static readonly Dictionary<byte, List<int>> GuessNumber = [];

    public static readonly List<string> NameSnacksCn = ["冰激凌", "奶茶", "巧克力", "蛋糕", "甜甜圈", "可乐", "柠檬水", "冰糖葫芦", "果冻", "糖果", "牛奶", "抹茶", "烧仙草", "菠萝包", "布丁", "椰子冻", "曲奇", "红豆土司", "三彩团子", "艾草团子", "泡芙", "可丽饼", "桃酥", "麻薯", "鸡蛋仔", "马卡龙", "雪梅娘", "炒酸奶", "蛋挞", "松饼", "西米露", "奶冻", "奶酥", "可颂", "奶糖"];

    // ReSharper disable once StringLiteralTypo
    public static readonly List<string> NameSnacksEn = ["Ice cream", "Milk tea", "Chocolate", "Cake", "Donut", "Coke", "Lemonade", "Candied haws", "Jelly", "Candy", "Milk", "Matcha", "Burning Grass Jelly", "Pineapple Bun", "Pudding", "Coconut Jelly", "Cookies", "Red Bean Toast", "Three Color Dumplings", "Wormwood Dumplings", "Puffs", "Can be Crepe", "Peach Crisp", "Mochi", "Egg Waffle", "Macaron", "Snow Plum Niang", "Fried Yogurt", "Egg Tart", "Muffin", "Sago Dew", "panna cotta", "soufflé", "croissant", "toffee"];
    public Coroutines coroutines;

    private static HashAuth DebugKeyAuth { get; set; }
    private static ConfigEntry<string> DebugKeyInput { get; set; }

    private Harmony Harmony { get; } = new(PluginGuid);

    public static NormalGameOptionsV08 NormalOptions => GameOptionsManager.Instance.currentNormalGameOptions;

    // Client Options
    public static ConfigEntry<string> HideName { get; private set; }
    public static ConfigEntry<string> HideColor { get; private set; }
    public static ConfigEntry<int> MessageWait { get; private set; }
    public static ConfigEntry<bool> GM { get; private set; }
    public static ConfigEntry<bool> UnlockFps { get; private set; }
    public static ConfigEntry<bool> AutoStart { get; private set; }
    public static ConfigEntry<bool> ForceOwnLanguage { get; private set; }
    public static ConfigEntry<bool> ForceOwnLanguageRoleName { get; private set; }
    public static ConfigEntry<bool> SwitchVanilla { get; private set; }
    public static ConfigEntry<bool> DarkTheme { get; private set; }
    public static ConfigEntry<bool> ShowPlayerInfoInLobby { get; private set; }
    public static ConfigEntry<bool> LobbyMusic { get; private set; }

    // Preset Name Options
    public static ConfigEntry<string> Preset1 { get; private set; }
    public static ConfigEntry<string> Preset2 { get; private set; }
    public static ConfigEntry<string> Preset3 { get; private set; }
    public static ConfigEntry<string> Preset4 { get; private set; }
    public static ConfigEntry<string> Preset5 { get; private set; }

    // Other Configs
    public static ConfigEntry<string> WebhookUrl { get; private set; }
    public static ConfigEntry<string> BetaBuildUrl { get; private set; }
    public static ConfigEntry<float> LastKillCooldown { get; private set; }
    public static ConfigEntry<float> LastShapeshifterCooldown { get; private set; }

    public static PlayerControl[] AllPlayerControls
    {
        get
        {
            int count = PlayerControl.AllPlayerControls.Count;
            var result = new PlayerControl[count];
            int i = 0;
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null) continue;
                result[i++] = pc;
            }

            if (i == 0) return [];

            Array.Resize(ref result, i);
            return result;
        }
    }

    public static PlayerControl[] AllAlivePlayerControls
    {
        get
        {
            int count = PlayerControl.AllPlayerControls.Count;
            var result = new PlayerControl[count];
            int i = 0;
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || !pc.IsAlive() || pc.Data.Disconnected || Pelican.IsEaten(pc.PlayerId)) continue;
                result[i++] = pc;
            }

            if (i == 0) return [];

            Array.Resize(ref result, i);
            return result;
        }
    }

    // ReSharper disable once InconsistentNaming
    public static string Get_TName_Snacks => TranslationController.Instance.currentLanguage.languageID is SupportedLangs.SChinese or SupportedLangs.TChinese ? NameSnacksCn.RandomElement() : NameSnacksEn.RandomElement();

    public static NetworkedPlayerInfo LastVotedPlayerInfo { get; set; }

    public static MapNames CurrentMap => (MapNames)NormalOptions.MapId;

    public override void Load()
    {
        Instance = this;

        //Client Options
        HideName = Config.Bind("Client Options", "Hide Game Code Name", "IVR");
        HideColor = Config.Bind("Client Options", "Hide Game Code Color", $"{ModColor}");
        DebugKeyInput = Config.Bind("Authentication", "Debug Key", string.Empty);
        AutoStart = Config.Bind("Client Options", "AutoStart", false);
        GM = Config.Bind("Client Options", "GM", false);
        UnlockFps = Config.Bind("Client Options", "UnlockFPS", false);
        AutoStart = Config.Bind("Client Options", "AutoStart", false);
        ForceOwnLanguage = Config.Bind("Client Options", "ForceOwnLanguage", false);
        ForceOwnLanguageRoleName = Config.Bind("Client Options", "ForceOwnLanguageRoleName", false);
        SwitchVanilla = Config.Bind("Client Options", "SwitchVanilla", false);
        DarkTheme = Config.Bind("Client Options", "DarkTheme", true);
        ShowPlayerInfoInLobby = Config.Bind("Client Options", "ShowPlayerInfoInLobby", false);
        LobbyMusic = Config.Bind("Client Options", "LobbyMusic", false);

        Logger = BepInEx.Logging.Logger.CreateLogSource("IVR");
        coroutines = AddComponent<Coroutines>();
        IVR.Logger.Enable();
        IVR.Logger.Disable("NotifyRoles");
        IVR.Logger.Disable("SwitchSystem");
        IVR.Logger.Disable("ModNews");
        IVR.Logger.Disable("CustomRpcSender");
        if (!DebugModeManager.AmDebugger)
        {
            IVR.Logger.Disable("2018k");
            IVR.Logger.Disable("Github");
            //IVR.Logger.Disable("ReceiveRPC");
            IVR.Logger.Disable("SendRPC");
            IVR.Logger.Disable("SetRole");
            IVR.Logger.Disable("Info.Role");
            IVR.Logger.Disable("TaskState.Init");
            //IVR.Logger.Disable("Vote");
            IVR.Logger.Disable("RpcSetNamePrivate");
            //IVR.Logger.Disable("SendChat");
            IVR.Logger.Disable("SetName");
            //IVR.Logger.Disable("AssignRoles");
            //IVR.Logger.Disable("RepairSystem");
            //IVR.Logger.Disable("MurderPlayer");
            //IVR.Logger.Disable("CheckMurder");
            IVR.Logger.Disable("PlayerControl.RpcSetRole");
            IVR.Logger.Disable("SyncCustomSettings");
        }
        //IVR.Logger.isDetail = true;

        // Authentication related - Initialization
        DebugKeyAuth = new(DebugKeyHash, DebugKeySalt);

        DebugModeManager.Auth(DebugKeyAuth, DebugKeyInput.Value);

        Preset1 = Config.Bind("Preset Name Options", "Preset1", "Preset_1");
        Preset2 = Config.Bind("Preset Name Options", "Preset2", "Preset_2");
        Preset3 = Config.Bind("Preset Name Options", "Preset3", "Preset_3");
        Preset4 = Config.Bind("Preset Name Options", "Preset4", "Preset_4");
        Preset5 = Config.Bind("Preset Name Options", "Preset5", "Preset_5");
        WebhookUrl = Config.Bind("Other", "WebhookURL", "none");
        BetaBuildUrl = Config.Bind("Other", "BetaBuildURL", string.Empty);
        MessageWait = Config.Bind("Other", "MessageWait", 0);
        LastKillCooldown = Config.Bind("Other", "LastKillCooldown", (float)30);
        LastShapeshifterCooldown = Config.Bind("Other", "LastShapeshifterCooldown", (float)30);

        HasArgumentException = false;
        try
        {
            RoleColors = new()
            {
                // Vanilla
                { CustomRoles.Crewmate, "#8cffff" },
                { CustomRoles.Engineer, "#FF6A00" },
                { CustomRoles.Scientist, "#8ee98e" },
                { CustomRoles.GuardianAngel, "#77e6d1" },
                { CustomRoles.Tracker, "#34ad50" },
                { CustomRoles.Noisemaker, "#ff4a62" },
                // Vanilla Remakes
                { CustomRoles.CrewmateIVR, "#8cffff" },
                { CustomRoles.EngineerIVR, "#FF6A00" },
                { CustomRoles.ScientistIVR, "#8ee98e" },
                { CustomRoles.GuardianAngelIVR, "#77e6d1" },
                { CustomRoles.TrackerIVR, "#34ad50" },
                { CustomRoles.NoisemakerIVR, "#ff4a62" },
                // Ghost roles
                { CustomRoles.GA, "#8cffff" },
                // GM
                { CustomRoles.GM, "#ff5b70" },

                // FFA
                { CustomRoles.Killer, "#00ffff" },
                // Hide And Seek
                { CustomRoles.Seeker, "#ff1919" },
                { CustomRoles.Hider, "#345eeb" },
                { CustomRoles.Fox, "#00ff00" },
                { CustomRoles.Troll, "#ff00ff" },
                { CustomRoles.Jumper, "#ddf542" },
                { CustomRoles.Detector, "#42ddf5" },
                { CustomRoles.Jet, "#42f54b" },
                { CustomRoles.Dasher, "#f542b0" },
                { CustomRoles.Locator, "#f59e42" },
                { CustomRoles.Venter, "#694141" },
                { CustomRoles.Agent, "#ff8f8f" },
                { CustomRoles.Taskinator, "#561dd1" }
            };
            Enum.GetValues<CustomRoles>().Where(x => x.GetCustomRoleTypes() == CustomRoleTypes.Impostor).Do(x => RoleColors.TryAdd(x, "#ff1919"));
        }
        catch (ArgumentException ex)
        {
            IVR.Logger.Error("错误：字典出现重复项", "LoadDictionary");
            IVR.Logger.Exception(ex, "LoadDictionary");
            HasArgumentException = true;
        }
        catch (Exception ex)
        {
            IVR.Logger.Fatal(ex.ToString(), "Main");
        }

        CustomWinnerHolder.Reset();
        ServerAddManager.Init();
        Translator.Init();
        BanManager.Init();
        TemplateManager.Init();
        SpamManager.Init();
        DevManager.Init();
        Cloud.Init();

        IRandom.SetInstance(new NetRandomWrapper());

        IVR.Logger.Info($"{Application.version}", "AmongUs Version");

        var handler = IVR.Logger.Handler("GitVersion");
        handler.Info($"{nameof(ThisAssembly.Git.BaseTag)}: {ThisAssembly.Git.BaseTag}");
        handler.Info($"{nameof(ThisAssembly.Git.Commit)}: {ThisAssembly.Git.Commit}");
        handler.Info($"{nameof(ThisAssembly.Git.Commits)}: {ThisAssembly.Git.Commits}");
        handler.Info($"{nameof(ThisAssembly.Git.IsDirty)}: {ThisAssembly.Git.IsDirty}");
        handler.Info($"{nameof(ThisAssembly.Git.Sha)}: {ThisAssembly.Git.Sha}");
        handler.Info($"{nameof(ThisAssembly.Git.Tag)}: {ThisAssembly.Git.Tag}");

        ClassInjector.RegisterTypeInIl2Cpp<ErrorText>();

        Harmony.PatchAll();

        if (!DebugModeManager.AmDebugger) ConsoleManager.DetachConsole();
        else ConsoleManager.CreateConsole();

        IVR.Logger.Msg("========= IVR loaded! =========", "Plugin Load");
    }

    public static void LoadRoleClasses()
    {
        AllRoleClasses = [];
        try
        {
            AllRoleClasses.AddRange(Assembly.GetAssembly(typeof(RoleBase))!
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(RoleBase)))
                .Select(t => (RoleBase)Activator.CreateInstance(t, null)));
            AllRoleClasses.Sort();
        }
        catch (Exception e)
        {
            Utils.ThrowException(e);
        }
    }

    public void StartCoroutine(System.Collections.IEnumerator coroutine)
    {
        if (coroutine == null)
        {
            return;
        }

        coroutines.StartCoroutine(coroutine.WrapToIl2Cpp());
    }

    public void StopCoroutine(System.Collections.IEnumerator coroutine)
    {
        if (coroutine == null)
        {
            return;
        }

        coroutines.StopCoroutine(coroutine.WrapToIl2Cpp());
    }

    public void StopAllCoroutines()
    {
        coroutines.StopAllCoroutines();
    }
}

[Flags]
public enum Team
{
    None = 0,
    Impostor = 1,
    Neutral = 2,
    Crewmate = 4
}

#pragma warning disable IDE0079 // Remove unnecessary suppression
[SuppressMessage("ReSharper", "UnusedMember.Global")]
#pragma warning restore IDE0079 // Remove unnecessary suppression
public enum CustomWinner
{
    Draw = -1,
    Default = -2,
    None = -3,
    Error = -4,
    Neutrals = -5,

    // CTA
    CustomTeam = -6,

    // Hide And Seek
    Hider = CustomRoles.Hider,
    Seeker = CustomRoles.Seeker,
    Troll = CustomRoles.Troll,
    Taskinator = CustomRoles.Taskinator,

    // Standard
    Impostor = CustomRoles.Impostor,
    Crewmate = CustomRoles.Crewmate,

}

public enum AdditionalWinners
{
    None = -1,

    // Hide And Seek
    Fox = CustomRoles.Fox,

    // -------------
    FFF = CustomRoles.FFF,
}

public enum SuffixModes
{
    None = 0,
    IVR,
    Streaming,
    Recording,
    RoomHost,
    OriginalName,
    DoNotKillMe,
    NoAndroidPlz,
    AutoHost
}

public enum VoteMode
{
    Default,
    Suicide,
    SelfVote,
    Skip
    NoVote
}

public enum TieMode
{
    Default,
    All,
    Random
}

public class Coroutines : MonoBehaviour
{
}
