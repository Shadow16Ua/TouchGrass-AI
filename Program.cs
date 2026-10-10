using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();
app.UseCors("AllowAll");

app.MapPost("/api/generate-adventure", async (AdventureRequest request, IHttpClientFactory clientFactory) =>
{
    try
    {
        var client = clientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);

        string prompt = $@"You are an outdoor micro-adventure generator for REAL LIFE.
Generate a UNIQUE, highly contextual adventure for someone walking outside right now.
Setting: {request.Environment}
Time available: {request.DurationMinutes} minutes.

CRITICAL INSTRUCTIONS:
1. Tasks must be REAL-WORLD physical activities or observations (e.g., 'Find a red leaf', 'Spot a blue car', 'Count 3 different types of trees').
2. DO NOT invent fictional characters, neighbors, or specific names (NO 'Mr. Chen', NO 'Mrs. Miller'). The user is walking in their real neighborhood and doesn't know these fake people.
3. Keep tasks generic enough to be completed in any typical {request.Environment}.
4. Output ONLY a valid JSON object. No markdown, no explanations.

Use this JSON SCHEMA, but REPLACE the placeholders with your realistic generated content:
{{
  ""title"": ""<Generate a realistic catchy title>"",
  ""quests"": [
    {{ ""task"": ""<Generate realistic unique task 1>"", ""category"": ""<Generate category 1>"" }},
    {{ ""task"": ""<Generate realistic unique task 2>"", ""category"": ""<Generate category 2>"" }},
    {{ ""task"": ""<Generate realistic unique task 3>"", ""category"": ""<Generate category 3>"" }}
  ]
}}";

        var ollamaRequest = new
        {
            model = "gemma2:2b",
            prompt = prompt,
            stream = false,
            format = "json" 
        };

        var response = await client.PostAsJsonAsync("http://localhost:11434/api/generate", ollamaRequest);

        if (response.IsSuccessStatusCode)
        {
            var ollamaResult = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            var rawText = ollamaResult?.Response ?? "{}";

            rawText = rawText.Trim();
            if (rawText.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) rawText = rawText.Substring(7);
            else if (rawText.StartsWith("```")) rawText = rawText.Substring(3);
            if (rawText.EndsWith("```")) rawText = rawText.Substring(0, rawText.Length - 3);

            return Results.Content(rawText.Trim(), "application/json");
        }

        return Results.Problem("Failed to get a response from Ollama.");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.Run();

record AdventureRequest(string Environment, int DurationMinutes);
record OllamaResponse(string Response);