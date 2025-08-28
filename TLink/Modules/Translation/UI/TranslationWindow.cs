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
    private int selectedProviderIndex = -1;
    private int selectedSourceLangIndex;
    private int selectedTargetLangIndex;
    private bool tempCacheEnabled;
    private int tempCacheSize;
    
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
        tempCacheEnabled = config.EnableCache;
        tempCacheSize = config.CacheSize;
        selectedSourceLangIndex = Array.IndexOf(viewModel.SupportedLanguages, config.SourceLanguage);
        selectedTargetLangIndex = Array.IndexOf(viewModel.SupportedLanguages, config.TargetLanguage);
    }
    
    public override void Draw()
    {
        if (ImGui.BeginTabBar("TranslationTabs"))
        {
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
    
    public void DrawConfiguration()
    {
        var changed = false;
        
        // Provider Selection
        ImGui.Text("Translation Provider");
        ImGui.Separator();
        
        if (viewModel.AvailableProviders.Count == 0)
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "No translation providers registered");
            ImGui.TextWrapped("Provider modules must be installed separately");
        }
        else
        {
            // Update selected index based on current provider
            if (!string.IsNullOrEmpty(viewModel.ActiveProvider))
            {
                var currentIndex = viewModel.AvailableProviders
                    .Select((p, i) => new { Provider = p, Index = i })
                    .FirstOrDefault(x => x.Provider == viewModel.ActiveProvider)?.Index ?? -1;
                if (currentIndex != selectedProviderIndex)
                {
                    selectedProviderIndex = currentIndex;
                }
            }
            
            var providers = viewModel.AvailableProviders.ToArray();
            if (ImGui.Combo("Active Provider", ref selectedProviderIndex, providers, providers.Length))
            {
                if (selectedProviderIndex >= 0 && selectedProviderIndex < providers.Length)
                {
                    config.ActiveProvider = providers[selectedProviderIndex];
                    changed = true;
                }
            }
            
            if (viewModel.ProviderSupportsFormatting)
            {
                ImGui.TextColored(new Vector4(0, 1, 0, 1), "✓ Provider supports formatting preservation");
            }
            else
            {
                ImGui.TextColored(new Vector4(1, 1, 0, 1), "⚠ Provider uses plain text only");
            }
        }
        
        ImGui.Spacing();
        
        // Language Settings
        ImGui.Text("Language Settings");
        ImGui.Separator();
        
        var languages = viewModel.SupportedLanguages;
        
        if (ImGui.Combo("Source Language", ref selectedSourceLangIndex, languages, languages.Length))
        {
            config.SourceLanguage = languages[selectedSourceLangIndex];
            changed = true;
        }
        
        if (ImGui.Combo("Target Language", ref selectedTargetLangIndex, languages, languages.Length))
        {
            config.TargetLanguage = languages[selectedTargetLangIndex];
            changed = true;
        }
        
        ImGui.Spacing();
        
        // Cache Settings
        ImGui.Text("Cache Settings");
        ImGui.Separator();
        
        if (ImGui.Checkbox("Enable Cache", ref tempCacheEnabled))
        {
            config.EnableCache = tempCacheEnabled;
            viewModel.UpdateCacheSettings(tempCacheEnabled, config.CacheSize);
            changed = true;
        }
        
        if (tempCacheEnabled)
        {
            if (ImGui.SliderInt("Cache Size", ref tempCacheSize, 10, 500))
            {
                config.CacheSize = tempCacheSize;
                viewModel.UpdateCacheSettings(config.EnableCache, tempCacheSize);
                changed = true;
            }
            
            ImGui.SameLine();
            if (ImGui.Button("Clear Cache"))
            {
                viewModel.ClearCache();
            }
        }
        
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
        ImGui.Text("Translation Statistics");
        ImGui.Separator();
        
        // Current status
        if (viewModel.IsTranslating)
        {
            ImGui.TextColored(new Vector4(0, 1, 1, 1), "● Translating...");
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
            ImGui.Text("Active Provider");
            ImGui.TableNextColumn();
            ImGui.Text(string.IsNullOrEmpty(viewModel.ActiveProvider) ? "None" : viewModel.ActiveProvider);
            
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text("Total Translations");
            ImGui.TableNextColumn();
            ImGui.Text($"{viewModel.TotalTranslations}");
            
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text("Cache Hits");
            ImGui.TableNextColumn();
            ImGui.Text($"{viewModel.CacheHits} ({viewModel.CacheHitRate:P0})");
            
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text("Failed Translations");
            ImGui.TableNextColumn();
            ImGui.Text($"{viewModel.FailedTranslations}");
            
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text("Avg Translation Time");
            ImGui.TableNextColumn();
            ImGui.Text($"{viewModel.AverageTranslationTime:F0} ms");
            
            ImGui.EndTable();
        }
        
        ImGui.Spacing();
        
        // Recent translations
        ImGui.Text("Recent Translations");
        ImGui.Separator();
        
        if (viewModel.RecentTranslations.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), "No translations yet");
        }
        else
        {
            if (ImGui.BeginChild("RecentTranslations", new Vector2(0, 0), true))
            {
                foreach (var item in viewModel.RecentTranslations.TakeLast(10).Reverse())
                {
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), 
                        $"[{item.Timestamp:HH:mm:ss}] {item.Channel}");
                    
                    ImGui.TextWrapped($"Original: {item.OriginalText}");
                    ImGui.TextColored(new Vector4(0.5f, 1, 0.5f, 1), 
                        $"Translation: {item.TranslatedText}");
                    
                    if (item.FormattingPreserved)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(0, 1, 0, 1), "[Formatted]");
                    }
                    
                    ImGui.Separator();
                }
                ImGui.EndChild();
            }
        }
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
