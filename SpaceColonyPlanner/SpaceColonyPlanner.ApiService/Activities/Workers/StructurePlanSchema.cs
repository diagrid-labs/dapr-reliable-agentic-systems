using Google.Protobuf.WellKnownTypes;

namespace SpaceColonyPlanner.Activities.Workers;

internal static class StructurePlanSchema
{
    public static Struct Get()
    {
        var stringType = new Struct();
        stringType.Fields.Add("type", Value.ForString("string"));

        var integerType = new Struct();
        integerType.Fields.Add("type", Value.ForString("integer"));

        var stringArrayType = new Struct();
        stringArrayType.Fields.Add("type", Value.ForString("array"));
        stringArrayType.Fields.Add("items", Value.ForStruct(stringType));

        var properties = new Struct();
        properties.Fields.Add("structureType", Value.ForStruct(stringType));
        properties.Fields.Add("quantity", Value.ForStruct(integerType));
        properties.Fields.Add("materials", Value.ForStruct(stringArrayType));
        properties.Fields.Add("constructionDays", Value.ForStruct(integerType));
        properties.Fields.Add("workerHours", Value.ForStruct(integerType));
        properties.Fields.Add("prerequisites", Value.ForStruct(stringArrayType));
        properties.Fields.Add("detailedSpecification", Value.ForStruct(stringType));

        var responseFormat = new Struct();
        responseFormat.Fields.Add("type", Value.ForString("object"));
        responseFormat.Fields.Add("properties", Value.ForStruct(properties));
        responseFormat.Fields.Add("required", Value.ForList(
            Value.ForString("structureType"),
            Value.ForString("quantity"),
            Value.ForString("materials"),
            Value.ForString("constructionDays"),
            Value.ForString("workerHours"),
            Value.ForString("prerequisites"),
            Value.ForString("detailedSpecification")));

        return responseFormat;
    }
}
