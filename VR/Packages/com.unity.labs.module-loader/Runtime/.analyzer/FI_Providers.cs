using System;
using Unity.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

public class FI_Providers : Instrumentation
{
    const string k_IFunctionalityProvider = "IFunctionalityProvider";
    const string k_IFunctionalitySubscriber = "IFunctionalitySubscriber";
    const string k_ConnectSubscriber = "ConnectSubscriber";
    const string k_Provider = "provider";

    const string k_ProviderPropertyImplementation = "%0 %1<%0>.provider { get; set; }";
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

        public FIBaseRewriter(SemanticModel model, AnalyzerMessageCallback messageCallback, string filePath)
        {
            m_Model = model;
            m_MessageCallback = messageCallback;
            m_FilePath = filePath;
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            node = base.VisitMethodDeclaration(node) as MethodDeclarationSyntax;
            var body = node.Body;
            if (body == null)
                return node;

            var parentClass = node.Parent as ClassDeclarationSyntax;
            if (parentClass == null)
                return node;

            var parameterList = node.ParameterList;
            if (parameterList == null)
                return node;

            var parameters = parameterList.Parameters;
            var parameterCount = parameters.Count;
            if (parameterCount == 0)
                return node;

            var methodName = node.Identifier.Text;
            var firstParameter = parameterList.Parameters[0];
            var firstParameterName = firstParameter.Identifier.Text;

            if (methodName == k_ConnectSubscriber && parameterCount == 1)
            {
                node = node.WithBody(SyntaxFactory.Block());
                // TODO: share this code with the method for finding subscriber classes below
                var symbol = m_Model.GetDeclaredSymbol(parentClass) as INamedTypeSymbol;
                foreach (var @interface in symbol.AllInterfaces)
                {
                    // TODO: handle provider types with more than one level of inheritence
                    foreach (var baseInterface in @interface.Interfaces)
                    {
                        if (baseInterface.Name != k_IFunctionalityProvider)
                            continue;

                        var varName = string.Format(k_VariableNameTemplate, @interface.Name);

                        var castStatement = string.Format(k_CastStatement, varName, firstParameterName, @interface);
                        var assignment = string.Format(k_SetProviderStatement, varName);
                        node = node.AddBodyStatements(
                            SyntaxFactory.ParseStatement(castStatement),
                            SyntaxFactory.ParseStatement(assignment));

                        wasModified = true;
                    }
                }
            }

            return node;
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
            //    messageCallback(AnalyzerMessageType.Info, "FI_Providers analyzer codegen output:\n" + newRoot, ast.FilePath, 0, 0);

            return newRoot;
        }
        catch (Exception e)
        {
            messageCallback(
                AnalyzerMessageType.Error,
                string.Format("Error in FI_Providers analyzer:\n{0}\n{1}", e.Message, e.StackTrace),
                ast.FilePath, 0, 0);
        }

        modified = false;
        return ast.GetRoot();
    }
}
