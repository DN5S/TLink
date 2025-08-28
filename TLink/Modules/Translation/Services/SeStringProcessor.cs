using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace TLink.Modules.Translation.Services;

/// <summary>
/// Converts SeString to semantic XML format for translation and back.
/// Preserves the context of wrapping payloads (colors, emphasis) and standalone payloads.
/// </summary>
public class SeStringProcessor
{
    // Patterns for different semantic tags
    private static readonly Regex ColorTagPattern = new(@"<c\s+id=""(\d+)"">(.*?)</c>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex EmphasisTagPattern = new(@"<em\s+id=""(\d+)"">(.*?)</em>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex IconTagPattern = new(@"<icon\s+id=""(\d+)""\s*/>", RegexOptions.Compiled);
    private static readonly Regex ItemTagPattern = new(@"<item\s+id=""(\d+)"">(.*?)</item>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex PlayerTagPattern = new(@"<player\s+id=""(\d+)"">(.*?)</player>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex MapTagPattern = new(@"<map\s+id=""(\d+)"">(.*?)</map>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex GenericTagPattern = new(@"<x\s+id=""(\d+)""\s*/>", RegexOptions.Compiled);
    
    public XmlEncodedMessage Encode(SeString seString)
    {
        var xmlBuilder = new StringBuilder();
        var payloadMap = new Dictionary<int, Payload>();
        var payloadIndex = 0;
        var openTags = new Stack<(string closeTag, int id)>();
        
        foreach (var payload in seString.Payloads)
        {
            switch (payload)
            {
                case TextPayload text:
                    xmlBuilder.Append(HttpUtility.HtmlEncode(text.Text));
                    break;
                
                // --- Wrapping Payloads (with context) ---
                
                case UIForegroundPayload foreground when foreground.ColorKey != 0:
                    // Start of color
                    var colorId = payloadIndex++;
                    xmlBuilder.Append($"<c id=\"{colorId}\">");
                    openTags.Push(("</c>", colorId));
                    payloadMap[colorId] = foreground;
                    break;
                
                case UIForegroundPayload { ColorKey: 0 }:
                    // Color reset - close the most recent color tag
                    if (openTags.Count != 0 && openTags.Peek().closeTag == "</c>")
                    {
                        xmlBuilder.Append(openTags.Pop().closeTag);
                    }
                    break;
                
                case UIGlowPayload glow when glow.ColorKey != 0:
                    // Start of a glow
                    var glowId = payloadIndex++;
                    xmlBuilder.Append($"<glow id=\"{glowId}\">");
                    openTags.Push(("</glow>", glowId));
                    payloadMap[glowId] = glow;
                    break;
                
                case UIGlowPayload { ColorKey: 0 }:
                    // Glow reset
                    if (openTags.Count != 0 && openTags.Peek().closeTag == "</glow>")
                    {
                        xmlBuilder.Append(openTags.Pop().closeTag);
                    }
                    break;
                
                case EmphasisItalicPayload emphasis:
                    if (!emphasis.IsEnabled)
                    {
                        // End of emphasis
                        if (openTags.Count != 0 && openTags.Peek().closeTag == "</em>")
                        {
                            xmlBuilder.Append(openTags.Pop().closeTag);
                        }
                    }
                    else
                    {
                        // Start of emphasis
                        var emId = payloadIndex++;
                        xmlBuilder.Append($"<em id=\"{emId}\">");
                        openTags.Push(("</em>", emId));
                        payloadMap[emId] = emphasis;
                    }
                    break;
                
                // --- Compound Payloads (icon and text as a unit) ---
                
                case ItemPayload item:
                    // Items are usually followed by text that names the item
                    var itemId = payloadIndex++;
                    xmlBuilder.Append($"<item id=\"{itemId}\">");
                    openTags.Push(("</item>", itemId));
                    payloadMap[itemId] = item;
                    break;
                
                case PlayerPayload player:
                    // Players are usually followed by the player name text
                    var playerId = payloadIndex++;
                    xmlBuilder.Append($"<player id=\"{playerId}\">");
                    openTags.Push(("</player>", playerId));
                    payloadMap[playerId] = player;
                    break;
                
                case MapLinkPayload mapLink:
                    var mapId = payloadIndex++;
                    xmlBuilder.Append($"<map id=\"{mapId}\">");
                    openTags.Push(("</map>", mapId));
                    payloadMap[mapId] = mapLink;
                    break;
                
                // --- Standalone Payloads (self-closing) ---
                
                case IconPayload icon:
                    var iconId = payloadIndex++;
                    xmlBuilder.Append($"<icon id=\"{iconId}\"/>");
                    payloadMap[iconId] = icon;
                    break;
                
                case NewLinePayload:
                    xmlBuilder.Append('\n');
                    break;
                
                case SeHyphenPayload:
                    xmlBuilder.Append('-');
                    break;
                
                case AutoTranslatePayload auto:
                    // Auto-translate should not be translated
                    var autoId = payloadIndex++;
                    xmlBuilder.Append($"<auto id=\"{autoId}\"/>");
                    payloadMap[autoId] = auto;
                    break;
                
                case RawPayload raw:
                    // Check if this is an end marker for items/players
                    if (IsEndMarker(raw) && openTags.Any())
                    {
                        var top = openTags.Peek();
                        if (top.closeTag is "</item>" or "</player>" or "</map>")
                        {
                            xmlBuilder.Append(openTags.Pop().closeTag);
                        }
                    }
                    else
                    {
                        // Generic raw payload
                        var rawId = payloadIndex++;
                        xmlBuilder.Append($"<x id=\"{rawId}\"/>");
                        payloadMap[rawId] = raw;
                    }
                    break;
                
                default:
                    // Unknown payload type - use generic tag
                    var genericId = payloadIndex++;
                    xmlBuilder.Append($"<x id=\"{genericId}\"/>");
                    payloadMap[genericId] = payload;
                    break;
            }
        }
        
        // Close any remaining open tags
        while (openTags.Count != 0)
        {
            xmlBuilder.Append(openTags.Pop().closeTag);
        }
        
        return new XmlEncodedMessage
        {
            XmlText = xmlBuilder.ToString(),
            PayloadMap = payloadMap,
            OriginalSeString = seString
        };
    }
    
    public SeString Decode(string xmlText, Dictionary<int, Payload> payloadMap)
    {
        var payloads = new List<Payload>();
        
        // Process the XML text to reconstruct payloads
        var position = 0;
        while (position < xmlText.Length)
        {
            // Find the next tag
            var nextTag = FindNextTag(xmlText, position);
            
            if (nextTag == null)
            {
                // No more tags - add remaining text
                var remainingText = xmlText[position..];
                if (!string.IsNullOrEmpty(remainingText))
                {
                    payloads.Add(new TextPayload(HttpUtility.HtmlDecode(remainingText)));
                }
                break;
            }
            
            // Add text before the tag
            if (nextTag.Value.startIndex > position)
            {
                var textBeforeTag = xmlText.Substring(position, nextTag.Value.startIndex - position);
                if (!string.IsNullOrEmpty(textBeforeTag))
                {
                    payloads.Add(new TextPayload(HttpUtility.HtmlDecode(textBeforeTag)));
                }
            }
            
            // Process the tag
            if (payloadMap.TryGetValue(nextTag.Value.id, out var payload))
            {
                payloads.Add(payload);
                
                // For wrapping tags, add the content and closing payload
                if (!string.IsNullOrEmpty(nextTag.Value.content))
                {
                    // Recursively decode the content
                    var innerPayloads = Decode(nextTag.Value.content, payloadMap);
                    payloads.AddRange(innerPayloads.Payloads);
                    
                    // Add closing payload based on type
                    AddClosingPayload(payloads, payload);
                }
            }
            
            position = nextTag.Value.endIndex;
        }
        
        return new SeString(payloads);
    }
    
    private static (int startIndex, int endIndex, int id, string? content)? FindNextTag(string xml, int startPosition)
    {
        var minIndex = int.MaxValue;
        (int startIndex, int endIndex, int id, string? content)? result = null;
        
        // Check all tag patterns
        var patterns = new[]
        {
            (ColorTagPattern, true),
            (EmphasisTagPattern, true),
            (ItemTagPattern, true),
            (PlayerTagPattern, true),
            (MapTagPattern, true),
            (IconTagPattern, false),
            (GenericTagPattern, false)
        };
        
        foreach (var (pattern, hasContent) in patterns)
        {
            var match = pattern.Match(xml, startPosition);
            if (match.Success && match.Index < minIndex)
            {
                minIndex = match.Index;
                var id = int.Parse(match.Groups[1].Value);
                var content = hasContent && match.Groups.Count > 2 ? match.Groups[2].Value : null;
                result = (match.Index, match.Index + match.Length, id, content);
            }
        }
        
        return result;
    }
    
    private static void AddClosingPayload(List<Payload> payloads, Payload openingPayload)
    {
        switch (openingPayload)
        {
            case UIForegroundPayload:
                payloads.Add(new UIForegroundPayload(0)); // Reset color
                break;
            case UIGlowPayload:
                payloads.Add(new UIGlowPayload(0)); // Reset glow
                break;
            case EmphasisItalicPayload:
                payloads.Add(new EmphasisItalicPayload(false)); // End emphasis
                break;
            case ItemPayload:
            case PlayerPayload:
            case MapLinkPayload:
                // These are typically closed by RawPayload end markers
                payloads.Add(new RawPayload([0x02, 0x27, 0x03])); // Generic end marker
                break;
        }
    }
    
    private static bool IsEndMarker(RawPayload raw)
    {
        // Common end marker patterns for items/players/maps
        var data = raw.Data;
        if (data.Length >= 3)
        {
            // Check for common end patterns like 0x02, 0x27, 0x03
            return data[0] == 0x02 && data[^1] == 0x03;
        }
        return false;
    }
}

public class XmlEncodedMessage
{
    public string XmlText { get; init; } = string.Empty;
    public Dictionary<int, Payload> PayloadMap { get; init; } = new();
    public SeString OriginalSeString { get; init; } = new();
}
