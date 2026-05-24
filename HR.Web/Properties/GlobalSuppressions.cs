using System.Diagnostics.CodeAnalysis;

// HR.Web declares CLS compliance once in AssemblyInfo.cs. Codacy SonarC# analyzes
// many C# files in isolation as srcassembly.dll, so S3990 is suppressed once here.
[assembly: SuppressMessage(
    "Compatibility",
    "S3990:Assemblies should be marked with CLSCompliantAttribute",
    Justification = "Assembly CLS compliance is declared in Properties/AssemblyInfo.cs.",
    Scope = "module")]
