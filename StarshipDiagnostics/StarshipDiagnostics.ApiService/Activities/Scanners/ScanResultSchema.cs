using Google.Protobuf.WellKnownTypes;

namespace StarshipDiagnostics.Activities.Scanners;

internal static class ScanResultSchema
{
    public static Struct Get()
    {
        var stringType = new Struct();
        stringType.Fields.Add("type", Value.ForString("string"));

        var numberType = new Struct();
        numberType.Fields.Add("type", Value.ForString("number"));

        var stringArrayType = new Struct();
        stringArrayType.Fields.Add("type", Value.ForString("array"));
        stringArrayType.Fields.Add("items", Value.ForStruct(stringType));

        var properties = new Struct();
        properties.Fields.Add("status", Value.ForStruct(stringType));
        properties.Fields.Add("healthPercentage", Value.ForStruct(numberType));
        properties.Fields.Add("issues", Value.ForStruct(stringArrayType));
        properties.Fields.Add("recommendations", Value.ForStruct(stringArrayType));
        properties.Fields.Add("detailedAnalysis", Value.ForStruct(stringType));

        var responseFormat = new Struct();
        responseFormat.Fields.Add("type", Value.ForString("object"));
        responseFormat.Fields.Add("properties", Value.ForStruct(properties));
        responseFormat.Fields.Add("required", Value.ForList(
            Value.ForString("status"),
            Value.ForString("healthPercentage"),
            Value.ForString("issues"),
            Value.ForString("recommendations"),
            Value.ForString("detailedAnalysis")));

        return responseFormat;
    }
}
