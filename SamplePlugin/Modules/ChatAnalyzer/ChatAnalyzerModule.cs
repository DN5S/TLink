using System;
using Microsoft.Extensions.DependencyInjection;
using SamplePlugin.Core.Module;
using SamplePlugin.Modules.Chat.Models;

namespace SamplePlugin.Modules.ChatAnalyzer;

[ModuleInfo("ChatAnalyzer", "1.0.0", 
    Dependencies = ["Chat"], 
    Description = "Analyzes chat patterns and provides statistics",
    Author = "Sample Author",
    Priority = 10)] // Higher priority, loads after Chat
public class ChatAnalyzerModule : ModuleBase
{
    private ChatAnalyzerWindow? window;
    private ChatAnalyzerViewModel? viewModel;
    private ChatAnalyzerModuleConfiguration? moduleConfig;
    
    public override string Name => "ChatAnalyzer";
    public override string Version => "1.0.0";
    public override string[] Dependencies => ["Chat"]; // Depends on Chat module
    
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<ChatAnalyzerViewModel>();
    }
    
    protected override void LoadConfiguration()
    {
        moduleConfig = GetModuleConfig<ChatAnalyzerModuleConfiguration>();
    }
    
    public override void Initialize()
    {
        viewModel = Services.GetRequiredService<ChatAnalyzerViewModel>();
        viewModel.Initialize(moduleConfig!);
        
        window = new ChatAnalyzerWindow(viewModel, moduleConfig!, () => 
        {
            SetModuleConfig(moduleConfig!);
        });
        
        // Listen to chat messages from the Chat module via EventBus
        Subscriptions.Add(
            EventBus.Listen<ChatMessageReceived>()
                .Subscribe(msg => viewModel.AnalyzeMessage(msg.Message))
        );
        
        Logger.Information("ChatAnalyzer module initialized");
    }
    
    public override void DrawUI()
    {
        window?.Draw();
    }
    
    public override void DrawConfiguration()
    {
        window?.DrawConfiguration();
    }
    
    public override void Dispose()
    {
        window?.Dispose();
        viewModel?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
