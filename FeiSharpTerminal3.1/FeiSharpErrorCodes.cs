namespace FeiSharpTerminal3._1;

/// <summary>
/// Stable diagnostic identifiers emitted by the lexer, parser and standard
/// library. Keep a code's meaning unchanged after it has been published.
/// </summary>
internal static class FeiSharpErrorCodes
{
    internal const string UnexpectedToken = "FS2001";
    internal const string ExpectedExpression = "FS2002";
    internal const string InvalidSyntax = "FS2003";
    internal const string MissingDelimiter = "FS2004";
    internal const string InvalidDeclaration = "FS2005";
    internal const string InvalidControlFlow = "FS2006";
    internal const string InvalidAssignment = "FS2007";

    internal const string UndefinedSymbol = "FS3004";
    internal const string InvalidArgument = "FS3005";
    internal const string TypeMismatch = "FS3006";
    internal const string IndexOutOfRange = "FS3007";
    internal const string NullReference = "FS3008";
    internal const string ReadonlyViolation = "FS3009";
    internal const string NamespaceNotImported = "FS3010";

    internal const string FileOperationFailed = "FS5001";
    internal const string NetworkRequestFailed = "FS5002";
    internal const string NetworkTimeout = "FS5003";
}
