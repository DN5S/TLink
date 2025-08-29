using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace TLink.Utils;

public class SeStringProcessor
{
    public static XmlEncodedMessage Encode(SeString seString)
    {
        var xmlBuilder = new StringBuilder();
        var payloadMap = new Dictionary<int, Payload>();
        var payloadIndex = 0;
        var openTags = new Stack<(string tagType, string closeTag, int id)>();
        
        foreach (var payload in seString.Payloads)
        {
            switch (payload)
            {
                case TextPayload text:
                    xmlBuilder.Append(HttpUtility.HtmlEncode(text.Text));
                    break;
                
                case UIForegroundPayload foreground when foreground.ColorKey != 0:
                    if (openTags.Count != 0 && openTags.Peek().closeTag == "</c>")
                    {
                        // Close the previous color first
                        xmlBuilder.Append(openTags.Pop().closeTag);
                    }
                    // Now start the new color
                    var colorId = payloadIndex++;
                    xmlBuilder.Append($"<c id=\"{colorId}\">");
                    openTags.Push(("color", "</c>", colorId));
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
                    // Start of a glow - but check if we're already in a glow and need to close it first
                    if (openTags.Count != 0 && openTags.Peek().closeTag == "</glow>")
                    {
                        // Close the previous glow first
                        xmlBuilder.Append(openTags.Pop().closeTag);
                    }
                    // Now start the new glow
                    var glowId = payloadIndex++;
                    xmlBuilder.Append($"<glow id=\"{glowId}\">");
                    openTags.Push(("glow", "</glow>", glowId));
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
                        openTags.Push(("em", "</em>", emId));
                        payloadMap[emId] = emphasis;
                    }
                    break;
                
                case ItemPayload item:
                    // Items are usually followed by text that names the item
                    var itemId = payloadIndex++;
                    xmlBuilder.Append($"<item id=\"{itemId}\">");
                    openTags.Push(("item", "</item>", itemId));
                    payloadMap[itemId] = item;
                    break;
                
                case PlayerPayload player:
                    // Players are usually followed by the player name text
                    var playerId = payloadIndex++;
                    xmlBuilder.Append($"<player id=\"{playerId}\">");
                    openTags.Push(("player", "</player>", playerId));
                    payloadMap[playerId] = player;
                    break;
                
                case MapLinkPayload mapLink:
                    var mapId = payloadIndex++;
                    xmlBuilder.Append($"<map id=\"{mapId}\">");
                    openTags.Push(("map", "</map>", mapId));
                    payloadMap[mapId] = mapLink;
                    break;
                
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
        
        try
        {
            // Fix various XML corruptions that DeepL might introduce
            
            // 1. Remove text content from tags that should be empty (glow/c tags with IDs that reset colors)
            // Check a payload map to see if these are reset payloads (ColorKey = 0)
            foreach (var kvp in payloadMap)
            {
                var id = kvp.Key;
                var payload = kvp.Value;
                
                // Check if this is a reset payload (color or glow with ColorKey = 0)
                if (payload is UIForegroundPayload { ColorKey: 0 } ||
                    payload is UIGlowPayload { ColorKey: 0 })
                {
                    // Remove any text content from these tags as they should be empty
                    xmlText = Regex.Replace(xmlText, $"""<(\w+)\s+id="{id}"[^>]*>[^<]+</\1>""", $"""<$1 id="{id}"></$1>""");
                }
            }
            
            // 2. Clean up spaces in empty tags that DeepL might have added
            // This is a more aggressive pattern that catches various whitespace combinations
            xmlText = Regex.Replace(xmlText, @"<(\w+)([^>]*)>\s*</\1>", "<$1$2></$1>");
            
            // 2b. Also clean up self-closing tags with spaces
            xmlText = Regex.Replace(xmlText, @"<(\w+)([^>]*)/\s*>", "<$1$2/>");
            
            // 3. Fix duplicate item/map/player tags (DeepL sometimes duplicates them)
            // Pattern to match any tag with an id attribute
            var tagPattern = """<(item|map|player)\s+id="(\d+)"[^>]*>(.*?)</\1>""";
            var tagMatches = Regex.Matches(xmlText, tagPattern);
            var seenTags = new Dictionary<string, int>();
            
            foreach (Match match in tagMatches)
            {
                var tagType = match.Groups[1].Value;
                var tagId = match.Groups[2].Value;
                var key = $"{tagType}:{tagId}";
                
                if (seenTags.TryGetValue(key, out var count))
                {
                    // This is a duplicate tag with the same ID
                    // DeepL sometimes creates these incorrectly
                    // Remove the duplicate by replacing it with just its content
                    if (count > 0)
                    {
                        var fullMatch = match.Value;
                        var content = match.Groups[3].Value;
                        xmlText = xmlText.Replace(fullMatch, content);
                    }
                    seenTags[key] = count + 1;
                }
                else
                {
                    seenTags[key] = 1;
                }
            }
            
            // 4. Remove any item/map/player tags that DeepL incorrectly inserted
            // These would have IDs that don't exist in our payload map
            foreach (Match match in Regex.Matches(xmlText, tagPattern))
            {
                var tagId = int.Parse(match.Groups[2].Value);
                if (!payloadMap.ContainsKey(tagId))
                {
                    // This tag was incorrectly added by DeepL, remove it
                    xmlText = xmlText.Replace(match.Value, match.Groups[3].Value);
                }
            }
            
            // Wrap in a root element to make it valid XML
            var wrappedXml = $"<root>{xmlText}</root>";
            var doc = XDocument.Parse(wrappedXml);
            
            // Process the root element's nodes
            if (doc.Root != null) ProcessNodes(doc.Root.Nodes(), payloads, payloadMap);
        }
        catch
        {
            // If XML parsing fails, fall back to treating it as plain text
            payloads.Add(new TextPayload(xmlText));
        }
        
        return new SeString(payloads);
    }
    
    private static void ProcessNodes(IEnumerable<XNode> nodes, List<Payload> payloads, Dictionary<int, Payload> payloadMap)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case XText text:
                    // Plain text node
                    var textValue = text.Value;
                    // Only add text payloads if they contain actual content (not just whitespace)
                    // This prevents issues where DeepL adds spaces in empty tags
                    if (!string.IsNullOrWhiteSpace(textValue))
                    {
                        payloads.Add(new TextPayload(textValue));
                    }
                    break;
                    
                case XElement element:
                    // Element with ID attribute
                    var idAttr = element.Attribute("id");
                    if (idAttr != null && int.TryParse(idAttr.Value, out var id))
                    {
                        // Add the opening payload
                        if (payloadMap.TryGetValue(id, out var payload))
                        {
                            payloads.Add(payload);
                            
                            // Check if this is a self-closing element (icon, auto, x)
                            var isSelfClosing = element.Name.LocalName is "icon" or "auto" or "x";
                            
                            // Process child nodes (if any and not self-closing)
                            if (!isSelfClosing && element.Nodes().Any())
                            {
                                ProcessNodes(element.Nodes(), payloads, payloadMap);
                            }
                            
                            // Add a closing payload for container tags (not self-closing)
                            if (!isSelfClosing && !element.IsEmpty)
                            {
                                AddClosingPayload(payloads, payload);
                            }
                        }
                        else
                        {
                            // ID isn't found - process children anyway to not lose content
                            if (element.Nodes().Any())
                            {
                                ProcessNodes(element.Nodes(), payloads, payloadMap);
                            }
                        }
                    }
                    else
                    {
                        // Element without ID - just process children
                        if (element.Nodes().Any())
                        {
                            ProcessNodes(element.Nodes(), payloads, payloadMap);
                        }
                    }
                    break;
            }
        }
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
                // Use Dalamud's predefined LinkTerminator for proper link closure
                payloads.Add(RawPayload.LinkTerminator);
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
