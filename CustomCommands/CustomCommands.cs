using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CustomCommands.Interfaces;
using Microsoft.Extensions.Logging;

namespace CustomCommands;

[MinimumApiVersion(213)]
public partial class CustomCommands : BasePlugin, IPluginConfig<CustomCommandsConfig>
{
    public override string ModuleName => "CustomCommands";
    public override string ModuleVersion => "3.0.0";
    public override string ModuleAuthor => "HerrMagic";
    public override string ModuleDescription => "Create your own commands per config";

    public CustomCommandsConfig Config { get; set; } = new();
    private readonly IRegisterCommands _registerCommands;
    private readonly IPluginGlobals _pluginGlobals;
    private readonly ILoadJson _loadJson;
    private readonly IEventManager _eventManager;
    private readonly IReplaceTagsFunctions _replaceTagsFunctions;

    public CustomCommands(IRegisterCommands RegisterCommands, ILogger<CustomCommands> Logger, 
                            IPluginGlobals PluginGlobals, ILoadJson LoadJson, IEventManager EventManager, IReplaceTagsFunctions ReplaceTagsFunctions)
    {
        this.Logger = Logger;
        _registerCommands = RegisterCommands;
        _pluginGlobals = PluginGlobals;
        _loadJson = LoadJson;
        _eventManager = EventManager;
        _replaceTagsFunctions = ReplaceTagsFunctions;
    }

    public void OnConfigParsed(CustomCommandsConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        if (!Config.IsPluginEnabled)
        {
            Logger.LogInformation($"{Config.LogPrefix} {ModuleName} is disabled");
            return;
        }
        
        Logger.LogInformation($"{ModuleName} 正在進行優化加載 (非同步模式)...");

        _pluginGlobals.Config = Config;
        Config.Prefix = _replaceTagsFunctions.ReplaceColorTags(Config.Prefix);

        // --- 改善重點：改用 Task.Run 配合 Server.NextFrame 避免阻塞 ---
        Task.Run(async () =>
        {
            // 在後台執行緒讀取 JSON 檔案，不影響遊戲運行
            var comms = await _loadJson.GetCommandsFromJsonFiles(ModuleDirectory);

            if (comms == null)
            {
                Logger.LogError("No commands found please create a config file");
                return;
            }

            // 讀取完成後，回到遊戲主執行緒安全地註冊指令
            Server.NextFrame(() =>
            {
                _eventManager.RegisterListeners();
                _pluginGlobals.CustomCommands = comms;

                _registerCommands.CheckForDuplicateCommands();
                _registerCommands.ConvertingCommandsForRegister();

                // 將指令註冊到伺服器
                foreach (var cmd in _pluginGlobals.CustomCommands)
                {
                    _registerCommands.AddCommands(cmd);
                }

                Logger.LogInformation($"{ModuleName} 已成功加載，未造成伺服器卡頓。");
            });
        });
    }
