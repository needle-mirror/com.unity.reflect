using System;
using Unity.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Collections.Generic;
using System.Text;

public class FI_Subscribers : Instrumentation
{
    const string k_IFunctionalityProvider = "IFunctionalityProvider";
    const string k_IFunctionalitySubscriber = "IFunctionalitySubscriber";
    const string k_ConnectSubscriber = "ConnectSubscriber";
    const string k_Provider = "provider";

    const string k_ProviderPropertyImplementation = "%0 %1<%0>.provider { get; set; }\n";
    const string k_ExtensionMethodBody = "\n{0}{1}.provider.{2}({3});\n";
    const string k_GetPropMethodBody = "\nreturn {0}.provider.{1};\n";
    const string k_SetPropMethodBody = "\n{0}.provider.{1} = {2};\n";
    const string k_SubscribeMethodBody = "\n{0}.provider.{1} += {2};\n";
    const string k_UnubscribeMethodBody = "\n{0}.provider.{1} -= {2};\n";
    const string k_VariableNameTemplate = "{0}Subscriber";
    const string k_CastStatement = "\nvar {0} = {1} as IFunctionalitySubscriber<{2}>;\n";
    const string k_SetProviderStatement = "if ({0} != null) {0}.provider = this;\n";

    static readonly StringBuilder k_StringBuilder = new StringBuilder();

    class FIBaseRewriter : CSharpSyntaxRewriter
    {
        SemanticModel m_Model;

        public bool wasModified { get; private set; }

        AnalyzerMessageCallback m_MessageCallback;
        string m_FilePath;

        static readonly List<INamedTypeSymbol> k_AllInterfaces = new List<INamedTypeSymbol>();

        public FIBaseRewriter(SemanticModel model, AnalyzerMessageCallback messageCallback, string filePath)
        {
            m_Model = model;
            m_MessageCallback = messageCallback;
            m_FilePath = filePath;
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            node = base.VisitClassDeclaration(node) as ClassDeclarationSyntax;

            var symbol = m_Model.GetDeclaredSymbol(node) as INamedTypeSymbol;
            k_AllInterfaces.Clear();
            GetAllInterfaces(symbol, k_AllInterfaces);
            foreach (var @interface in k_AllInterfaces)
            {
                if (@interface.IsGenericType && @interface.Name != k_IFunctionalitySubscriber)
                    continue;

                if (@interface.TypeArguments.Length == 0)
                    continue;

                var firstArgument = @interface.TypeArguments[0] as INamedTypeSymbol;
                if (firstArgument == null)
                    continue;

                var providerType = firstArgument.Name;

                bool hasProperty = false;
                foreach (var descendant in node.DescendantNodes().OfType<PropertyDeclarationSyntax>())
                {
                    if (!descendant.Identifier.Text.Contains(k_Provider))
                        continue;

                    var propertyType = descendant.Type.ToString();

                    // Trim after < to remove generic args--we only care about name here
                    var index = propertyType.IndexOf('<');
                    if (index > 0)
                        propertyType = propertyType.Substring(0, index);

                    // Split on . in case of namespace qualified name
                    propertyType = propertyType.Split('.').Last();

                    if (propertyType != providerType)
                        continue;

                    // TODO: bring back use of GenericNameSyntax when cast does not fail on namespace qualified name
                    //var genericType = descendant.Type as GenericNameSyntax;
                    var genericType = descendant.Type.ToString();
                    if (!genericType.Contains("<"))
                        genericType = null;

                    if (genericType == null && !firstArgument.IsGenericType)
                    {
                        hasProperty = true;
                    }
                    else if (genericType != null && firstArgument.IsGenericType)
                    {
                        var matchingArguments = true;
                        // TODO: bring back use of GenericNameSyntax when this works
                        //var argString = genericType.TypeArgumentList.ToString();
                        index = genericType.IndexOf('<');
                        var argString = genericType.Substring(index + 1, genericType.Length - index - 2);

                        if (argString.Length < 1)
                            continue;

                        //TODO: this was used to strip <> from argumentlist
                        //argString = argString.Substring(1, argString.Length - 2);
                        argString = argString.Replace(" ", string.Empty);
                        argString = argString.Replace("\n", string.Empty);
                        argString = argString.Replace("\t", string.Empty);
                        var argList = argString.Split(',');
                        var typeArguments = firstArgument.TypeArguments.ToList();
                        var argCount = typeArguments.Count;
                        for (var i = 0; i < argCount; i++)
                        {
                            var argName = argList[i];
                            var typeArgName = typeArguments[i].Name;

                            var keyword = TypeNameToKeyword(typeArgName);
                            if (keyword != null && argName.Contains(keyword))
                                continue;

                            if (!argName.Contains(typeArgName))
                            {
                                matchingArguments = false;
                                break;
                            }
                        }

                        if (matchingArguments)
                            hasProperty = true;
                    }
                }

                if (hasProperty)
                    continue;

                var propDecl = Template.Compile<PropertyDeclarationSyntax>(k_ProviderPropertyImplementation, firstArgument.ToString(), k_IFunctionalitySubscriber);
                node = node.WithMembers(node.Members.Add(propDecl));
                wasModified = true;

            }

            return node;
        }

        static string TypeNameToKeyword(string type)
        {
            switch (type)
            {
                case "Boolean":
                    return "bool";
                case "Char":
                    return "char";
                case "Byte":
                    return "byte";
                case "SByte":
                    return "sbyte";
                case "Int16":
                    return "short";
                case "Int32":
                    return "int";
                case "Int64":
                    return "long";
                case "UInt16":
                    return "ushort";
                case "UInt32":
                    return "uint";
                case "UInt64":
                    return "ulong";
                case "Single":
                    return "float";
                case "Double":
                    return "double";
                case "Object":
                    return "object";
                default:
                    return null;
            }
        }
    }

    // Get all interfaces implemented by a symbol, excluding those implemented by classes it implements
    static void GetAllInterfaces(INamedTypeSymbol symbol, List<INamedTypeSymbol> interfaces)
    {
        foreach (var @interface in symbol.Interfaces)
        {
            interfaces.Add(@interface);
            GetAllInterfaces(@interface, interfaces);
        }
    }

    public override SyntaxNode ModifyDocument(SyntaxTree ast, SemanticModel model, ISymbolResolver symbolResolver, AnalyzerMessageCallback messageCallback, out bool modified)
    {
        try
        {
            var baseRewriter = new FIBaseRewriter(model, messageCallback, ast.FilePath);
            var newRoot = baseRewriter.Visit(ast.GetRoot());
            modified = baseRewriter.wasModified;

            //if (modified)
            //    messageCallback(AnalyzerMessageType.Info, "FI_Subscribers analyzer codegen output:\n" + newRoot, ast.FilePath, 0, 0);

            return newRoot;
        }
        catch (Exception e)
        {
            messageCallback(
                AnalyzerMessageType.Error,
                string.Format("Error in FI_Subscribers analyzer:\n{0}\n{1}", e.Message, e.StackTrace),
                ast.FilePath, 0, 0);
        }

        modified = false;
        return ast.GetRoot();
    }
}
