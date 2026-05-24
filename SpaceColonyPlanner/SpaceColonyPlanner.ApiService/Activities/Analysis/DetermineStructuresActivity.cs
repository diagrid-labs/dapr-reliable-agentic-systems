using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.Workflow;
using Google.Protobuf.WellKnownTypes;
using SpaceColonyPlanner.Models;
using System.Text.Json;

namespace SpaceColonyPlanner.Activities.Analysis;

public class DetermineStructuresActivity : WorkflowActivity<DetermineStructuresInput, List<StructureRequest>>
{
    private readonly DaprConversationClient _conversationClient;
    
    public DetermineStructuresActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<List<StructureRequest>> RunAsync(
        WorkflowActivityContext context, 
        DetermineStructuresInput input)
    {
        var options = new ConversationOptions("conversation")
        {
            Temperature = 0.7f,
            ResponseFormat = GetResponseFormat()
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are a colony planning AI. Determine what structures are 
                    needed for a successful colony. Consider:
                    - Environmental protection needs
                    - Resource availability
                    - Population requirements
                    - Colony purpose
                    
                    Available structure types:
                    - HabitatDome (housing, life support)
                    - PowerPlant (energy generation)
                    - Agriculture (food production)
                    - MiningFacility (resource extraction)
                    - ResearchLab (scientific research)
                    - DefenseSystem (protection from threats)

                    JSON structure that describes the fields:
                    {
                      ""structures"": [
                        {
                          ""structureType"": ""<HabitatDome|PowerPlant|Agriculture|MiningFacility|ResearchLab|DefenseSystem>"",
                          ""priority"": ""<Critical|High|Medium|Low>"",
                          ""quantity"": <number>,
                          ""reasoning"": ""<explanation>""
                        }
                      ]
                    }")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "ColonyPlanner",
                        Content = [
                            new MessageContent($@"Determine structures needed:

Planet Challenges: {string.Join(", ", input.PlanetAnalysis.Challenges)}
Planet Opportunities: {string.Join(", ", input.PlanetAnalysis.Opportunities)}

Colony Requirements:
- Population: {input.Requirements.InitialPopulation} → {input.Requirements.TargetPopulation}
- Purpose: {input.Requirements.Purpose}
- Timeline: {input.Requirements.YearsToComplete} years

What structures are needed?")
                        ]
                    }
                })
            ],
            options);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);
        
        var structures = json.GetProperty("structures");
        var result = new List<StructureRequest>();
        
        foreach (var item in structures.EnumerateArray())
        {
            result.Add(new StructureRequest(
                item.GetProperty("structureType").GetString()!,
                System.Enum.Parse<Priority>(item.GetProperty("priority").GetString()!),
                item.GetProperty("quantity").GetInt32(),
                item.GetProperty("reasoning").GetString()!
            ));
        }
        
        return result;
    }

    private static Struct GetResponseFormat()
    {
        var stringType = new Struct();
        stringType.Fields.Add("type", Value.ForString("string"));

        var integerType = new Struct();
        integerType.Fields.Add("type", Value.ForString("integer"));

        var structureProps = new Struct();
        structureProps.Fields.Add("structureType", Value.ForStruct(stringType));
        structureProps.Fields.Add("priority", Value.ForStruct(stringType));
        structureProps.Fields.Add("quantity", Value.ForStruct(integerType));
        structureProps.Fields.Add("reasoning", Value.ForStruct(stringType));

        var structureType = new Struct();
        structureType.Fields.Add("type", Value.ForString("object"));
        structureType.Fields.Add("properties", Value.ForStruct(structureProps));
        structureType.Fields.Add("required", Value.ForList(
            Value.ForString("structureType"),
            Value.ForString("priority"),
            Value.ForString("quantity"),
            Value.ForString("reasoning")));

        var structuresArrayType = new Struct();
        structuresArrayType.Fields.Add("type", Value.ForString("array"));
        structuresArrayType.Fields.Add("items", Value.ForStruct(structureType));

        var properties = new Struct();
        properties.Fields.Add("structures", Value.ForStruct(structuresArrayType));

        var responseFormat = new Struct();
        responseFormat.Fields.Add("type", Value.ForString("object"));
        responseFormat.Fields.Add("properties", Value.ForStruct(properties));
        responseFormat.Fields.Add("required", Value.ForList(
            Value.ForString("structures")));

        return responseFormat;
    }
}
