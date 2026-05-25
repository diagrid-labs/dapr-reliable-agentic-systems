using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.Workflow;
using Google.Protobuf.WellKnownTypes;
using SpaceColonyPlanner.Models;
using System.Text.Json;

namespace SpaceColonyPlanner.Activities.Analysis;

public class SynthesizePlanActivity : WorkflowActivity<SynthesizePlanInput, ColonyMasterPlan>
{
    private readonly DaprConversationClient _conversationClient;
    
    public SynthesizePlanActivity(DaprConversationClient conversationClient)
    {
        _conversationClient = conversationClient;
    }
    
    public override async Task<ColonyMasterPlan> RunAsync(
        WorkflowActivityContext context, 
        SynthesizePlanInput input)
    {
        // Aggregate materials
        var allMaterials = new Dictionary<string, int>();
        foreach (var plan in input.StructurePlans)
        {
            foreach (var material in plan.Materials)
            {
                if (allMaterials.ContainsKey(material))
                    allMaterials[material] += plan.Quantity;
                else
                    allMaterials[material] = plan.Quantity;
            }
        }
        
        // Create construction timeline by analyzing prerequisites
        var options = new ConversationOptions("conversation")
        {
            Temperature = 0.7,
            ResponseFormat = GetResponseFormat()
        };
        
        var response = await _conversationClient.ConverseAsync(
            [
                new ConversationInput(new List<IConversationMessage>
                {
                    new SystemMessage
                    {
                        Content = [
                            new MessageContent(@"You are a construction project manager. Organize structures
                    into construction phases based on prerequisites and efficiency.

                    JSON structure that describes the fields:
                    {
                      ""timeline"": [
                        {
                          ""phaseNumber"": <number>,
                          ""name"": ""<phase name>"",
                          ""structures"": [""<structure1>"", ""<structure2>""],
                          ""durationDays"": <number>
                        }
                      ],
                      ""successFactors"": ""<success factors description>"",
                      ""riskAssessment"": ""<risk assessment description>""
                    }

                    Keep `successFactors` and `riskAssessment` <=60 words each. Return only the JSON object with no surrounding prose.")
                        ]
                    },
                    new UserMessage
                    {
                        Name = "ProjectManager",
                        Content = [
                            new MessageContent($@"Organize construction timeline:
Structures: {JsonSerializer.Serialize(input.StructurePlans)}

Create phased construction plan.")
                        ]
                    }
                })
            ],
            options);
        
        var json = JsonSerializer.Deserialize<JsonElement>(
            response.Outputs.First().Choices.First().Message.Content);

        var timeline = new List<ConstructionPhase>();
        foreach (var phase in json.GetProperty("timeline").EnumerateArray())
        {
            timeline.Add(new ConstructionPhase(
                phase.GetProperty("phaseNumber").GetInt32(),
                phase.GetProperty("name").GetString()!,
                JsonSerializer.Deserialize<List<string>>(
                    phase.GetProperty("structures").GetRawText())!,
                phase.GetProperty("durationDays").GetInt32()
            ));
        }
        
        return new ColonyMasterPlan(
            input.PlanetId,
            input.StructurePlans,
            timeline.Sum(p => p.DurationDays),
            allMaterials,
            timeline,
            json.GetProperty("successFactors").GetString()!,
            json.GetProperty("riskAssessment").GetString()!
        );
    }

    private static Struct GetResponseFormat()
    {
        var stringType = new Struct();
        stringType.Fields.Add("type", Value.ForString("string"));

        var integerType = new Struct();
        integerType.Fields.Add("type", Value.ForString("integer"));

        var stringArrayType = new Struct();
        stringArrayType.Fields.Add("type", Value.ForString("array"));
        stringArrayType.Fields.Add("items", Value.ForStruct(stringType));

        var phaseProps = new Struct();
        phaseProps.Fields.Add("phaseNumber", Value.ForStruct(integerType));
        phaseProps.Fields.Add("name", Value.ForStruct(stringType));
        phaseProps.Fields.Add("structures", Value.ForStruct(stringArrayType));
        phaseProps.Fields.Add("durationDays", Value.ForStruct(integerType));

        var phaseType = new Struct();
        phaseType.Fields.Add("type", Value.ForString("object"));
        phaseType.Fields.Add("properties", Value.ForStruct(phaseProps));
        phaseType.Fields.Add("required", Value.ForList(
            Value.ForString("phaseNumber"),
            Value.ForString("name"),
            Value.ForString("structures"),
            Value.ForString("durationDays")));

        var timelineArrayType = new Struct();
        timelineArrayType.Fields.Add("type", Value.ForString("array"));
        timelineArrayType.Fields.Add("items", Value.ForStruct(phaseType));

        var properties = new Struct();
        properties.Fields.Add("timeline", Value.ForStruct(timelineArrayType));
        properties.Fields.Add("successFactors", Value.ForStruct(stringType));
        properties.Fields.Add("riskAssessment", Value.ForStruct(stringType));

        var responseFormat = new Struct();
        responseFormat.Fields.Add("type", Value.ForString("object"));
        responseFormat.Fields.Add("properties", Value.ForStruct(properties));
        responseFormat.Fields.Add("required", Value.ForList(
            Value.ForString("timeline"),
            Value.ForString("successFactors"),
            Value.ForString("riskAssessment")));

        return responseFormat;
    }
}
