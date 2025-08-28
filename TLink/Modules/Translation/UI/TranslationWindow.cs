using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using TLink.Modules.Translation.Configuration;

namespace TLink.Modules.Translation.UI;

public class TranslationWindow : Window, IDisposable
{
    private readonly TranslationViewModel viewModel;
    private readonly TranslationConfig config;
    private readonly Action saveConfig;
    
    // Temporary values for editing
    private int selectedSourceLangIndex;
    private int selectedTargetLangIndex;
    private string filterText = string.Empty;
    
    public TranslationWindow(
        TranslationViewModel viewModel,
        TranslationConfig config,
        Action saveConfig)
        : base("Translation Hub##translation_hub", ImGuiWindowFlags.None)
    {
        this.viewModel = viewModel;
        this.config = config;
        this.saveConfig = saveConfig;
        
        Size = new Vector2(450, 350);
        SizeCondition = ImGuiCond.FirstUseEver;
        
        // Initialize temp values
        selectedSourceLangIndex = GetLanguageIndex(config.SourceLanguage);
        selectedTargetLangIndex = GetLanguageIndex(config.TargetLanguage);
    }
    
    public override void Draw()
    {
        if (ImGui.BeginTabBar("TranslationTabs"))
        {
            if (ImGui.BeginTabItem("Pipeline Handlers"))
            {
                DrawHandlers();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Configuration"))
            {
                DrawConfiguration();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Statistics"))
            {
                DrawStatistics();
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
    }
    
    public void DrawHandlers()
    {
        ImGui.Text($"Registered Handlers: {viewModel.HandlerCount} ({viewModel.EnabledHandlerCount} enabled)");
        ImGui.Separator();
        
        // Filter input
        ImGui.InputTextWithHint("##filter", "Filter handlers...", ref filterText, 256);
        
        ImGui.Spacing();
        
        // Handlers' table
        if (ImGui.BeginTable("HandlersTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable))
        {
            ImGui.TableSetupColumn("Priority", ImGuiTableColumnFlags.DefaultSort | ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Module", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Executions", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();
            
            foreach (var handler in viewModel.RegisteredHandlers
                .Where(h => string.IsNullOrEmpty(filterText) || 
                    h.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase)))
            {
                ImGui.TableNextRow();
                
                ImGui.TableNextColumn();
                ImGui.Text($"{handler.Priority}");
                
                ImGui.TableNextColumn();
                ImGui.Text(handler.Name);
                
                ImGui.TableNextColumn();
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), handler.ModuleName);
                
                ImGui.TableNextColumn();
                var enabled = handler.IsEnabled;
                if (ImGui.Checkbox($"##enabled_{handler.Name}", ref enabled))
                {
                    viewModel.EnableHandler(handler.Name, enabled);
                }
                
                ImGui.TableNextColumn();
                ImGui.Text($"{handler.ExecutionCount}");
                if (handler.LastError != null)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(1, 0, 0, 1), "[Error]");
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(handler.LastError);
                    }
                }
            }
            
            ImGui.EndTable();
        }
        
        ImGui.Spacing();
        
        if (ImGui.Button("Reset Statistics"))
        {
            viewModel.ResetStatistics();
        }
    }
    
    public void DrawConfiguration()
    {
        var changed = false;
        
        // Language Settings
        ImGui.Text("Language Settings");
        ImGui.Separator();
        
        var languages = viewModel.AllSupportedLanguages.ToArray();
        
        if (languages.Length == 0)
        {
            ImGui.TextColored(new Vector4(1, 1, 0, 1), "No translation handlers enabled");
            return;
        }
        
        if (ImGui.Combo("Source Language", ref selectedSourceLangIndex, languages, languages.Length))
        {
            if (selectedSourceLangIndex >= 0 && selectedSourceLangIndex < languages.Length)
            {
                config.SourceLanguage = languages[selectedSourceLangIndex];
                changed = true;
            }
        }
        
        if (ImGui.Combo("Target Language", ref selectedTargetLangIndex, languages, languages.Length))
        {
            if (selectedTargetLangIndex >= 0 && selectedTargetLangIndex < languages.Length)
            {
                config.TargetLanguage = languages[selectedTargetLangIndex];
                changed = true;
            }
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        ImGui.TextWrapped("Note: Individual handlers may have their own configuration. " +
            "Check each module's settings for handler-specific options.");
        
        ImGui.Spacing();
        
        // Save button
        if (changed)
        {
            if (ImGui.Button("Save Configuration"))
            {
                saveConfig.Invoke();
            }
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 1, 0, 1), "Configuration changed");
        }
    }
    
    private void DrawStatistics()
    {
        ImGui.Text("Pipeline Statistics");
        ImGui.Separator();
        
        // Current status
        if (viewModel.IsProcessing)
        {
            ImGui.TextColored(new Vector4(0, 1, 1, 1), $"● Processing ({viewModel.ActiveExecutions.Count} active)");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "● Idle");
        }
        
        ImGui.Spacing();
        
        // Statistics table
        if (ImGui.BeginTable("StatsTable", 2))
        {
            ImGui.TableSetupColumn("Metric", ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
            
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text("Total Executions");
            ImGui.TableNextColumn();
            ImGui.Text($"{viewModel.TotalExecutions}");
            
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text("Successful Executions");
            ImGui.TableNextColumn();
            ImGui.Text($"{viewModel.SuccessfulExecutions}");
            
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text("Failed Executions");
            ImGui.TableNextColumn();
            ImGui.Text($"{viewModel.FailedExecutions}");
            
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text("Success Rate");
            ImGui.TableNextColumn();
            ImGui.Text($"{viewModel.SuccessRate:P0}");
            
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text("Avg Pipeline Time");
            ImGui.TableNextColumn();
            ImGui.Text($"{viewModel.AveragePipelineTime:F0} ms");
            
            ImGui.EndTable();
        }
        
        ImGui.Spacing();
        
        // Active Executions
        ImGui.Text("Active Executions");
        ImGui.Separator();
        
        if (viewModel.ActiveExecutions.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "No active executions");
        }
        else
        {
            if (ImGui.BeginChild("ActiveExecutions", new Vector2(0, 150), true))
            {
                foreach (var execution in viewModel.ActiveExecutions)
                {
                    var duration = (DateTime.UtcNow - execution.StartTime).TotalMilliseconds;
                    ImGui.Text($"Request {execution.RequestId:B}");
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0, 1, 1, 1), $"[{duration:F0}ms]");
                    
                    ImGui.TextWrapped($"Message: {execution.Message}");
                    ImGui.Text($"Handlers executed: {execution.HandlersExecuted}");
                    ImGui.Separator();
                }
                ImGui.EndChild();
            }
        }
    }
    
    private int GetLanguageIndex(string language)
    {
        var languages = viewModel.AllSupportedLanguages.ToArray();
        var index = Array.IndexOf(languages, language);
        return index >= 0 ? index : 0;
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
