public static class JsonUtils
{
    public static string ParseJsonString(string input)
    {
        input = input.Trim();
        
        // Remove markdown code blocks if present
        if (input.StartsWith("```json") || input.StartsWith("```"))
        {
            var lines = input.Split('\n');
            input = string.Join('\n', lines.Skip(1));
        }
        if (input.EndsWith("```"))
        {
            var lastIndex = input.LastIndexOf("```");
            input = input.Substring(0, lastIndex);
        }
        input = input.Trim();
        
        // Ensure proper JSON object enclosure - add opening brace if missing
        if (!input.StartsWith("{"))
        {
            input = "{" + input;
        }
        
        // Process each line to ensure string values are properly quoted
        var lines2 = input.Split('\n');
        for (int i = 0; i < lines2.Length; i++)
        {
            var line = lines2[i];
            var trimmedLine = line.TrimStart();
            
            // Skip lines that are just braces or empty
            if (trimmedLine == "{" || trimmedLine == "}" || string.IsNullOrWhiteSpace(trimmedLine))
            {
                continue;
            }
            
            // Find the colon separator
            var colonIndex = trimmedLine.IndexOf(':');
            if (colonIndex > 0)
            {
                var key = trimmedLine.Substring(0, colonIndex).Trim();
                var valueAndRest = trimmedLine.Substring(colonIndex + 1).Trim();
                
                // Check if there's a trailing comma
                var hasComma = valueAndRest.EndsWith(",");
                var value = hasComma ? valueAndRest.Substring(0, valueAndRest.Length - 1).Trim() : valueAndRest;
                
                // If value doesn't start with a quote, it needs to be quoted
                if (!value.StartsWith("\""))
                {
                    value = "\"" + value;
                }
                
                // If value doesn't end with a quote, add it
                if (!value.EndsWith("\""))
                {
                    value = value + "\"";
                }
                
                // Reconstruct the line with proper indentation
                var indent = line.Length - trimmedLine.Length;
                lines2[i] = new string(' ', indent) + key + ": " + value + (hasComma ? "," : "");
            }
        }
        
        var result = string.Join('\n', lines2);
        
        // Ensure closing brace is present after line processing
        if (!result.TrimEnd().EndsWith("}"))
        {
            result = result + "\n}";
        }
        
        return result;
    }
}
