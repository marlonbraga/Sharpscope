namespace Sharpscope.Domain.Models;

/// <summary>
/// Canonical attribute keys stored on graph nodes and edges.
/// </summary>
public static class GraphAttributeKeys
{
    public const string TypeKind = "typeKind";
    public const string IsAbstract = "isAbstract";
    public const string FieldNames = "fieldNames";
    public const string FieldTypes = "fieldTypes";
    public const string FieldIsPublic = "fieldIsPublic";
    public const string DependsOnTypes = "dependsOnTypes";

    public const string MethodParameters = "parameters";
    public const string MethodSloc = "sloc";
    public const string MethodDecisionPoints = "decisionPoints";
    public const string MethodMaxNestingDepth = "maxNestingDepth";
    public const string MethodCalls = "calls";
    public const string MethodIsPublic = "isPublic";
    public const string MethodAccessedFields = "accessedFields";
    public const string MethodExternalCalls = "externalCalls";

    public const string ProjectRelativePath = "projectRelativePath";
    public const string ProjectName = "projectName";
    public const string SolutionName = "solutionName";
}
