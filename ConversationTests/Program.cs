using Microsoft.AspNetCore.Mvc;
using Dapr.AspNetCore;
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using AnomalyAnalysis.Models;
using Google.Protobuf.WellKnownTypes;
using Google.Protobuf;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprClient();
builder.Services.AddDaprConversationClient();


var app = builder.Build();

// Start analyzing a spatial anomaly
app.MapPost("/test", async (
    [FromBody] SpatialAnomaly spatialAnomaly,
    [FromServices] DaprConversationClient conversationClient) =>
{
    var conversationOptions = new ConversationOptions("conversation")
        {
            Temperature = 0.7
        };
        
        var response = await conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are Lt. Commander Data's sensor analysis subroutine. 
                            Process raw sensor data from the Enterprise's long-range scanners. 
                            Convert electromagnetic readings, subspace distortions, and quantum 
                            fluctuations into structured scientific data with key measurements 
                            (wavelength, frequency, intensity, spatial coordinates). The output should be json.")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "DataAnalysis",
                        Content = [
                            new MessageContent($"Process sensor data: {spatialAnomaly.RawSensorData}")
                        ]
                    }
                })
            ],
            conversationOptions);
        
    var output = response.Outputs.First().Choices.First().Message.Content;

    return Results.Ok(output);
});



app.Run();
