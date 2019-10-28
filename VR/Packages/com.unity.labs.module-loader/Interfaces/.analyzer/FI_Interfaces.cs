using System;
using Unity.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Text;

public class FI_Interfaces : Instrumentation
{
    const string k_IFunctionalitySubscriber = "IFunctionalitySubscriber";
    const string k_Provider = "provider";

    const string k_ProviderProperty = "TProvider provider { get; set; }\n";
    const string k_ExtensionMethodBody = "\n{0}{1}.provider.{2}({3});\n";
    const string k_GetPropMethodBody = "\nreturn {0}.provider.{1};\n";
    const string k_SetPropMethodBody = "\n{0}.provider.{1} = {2};\n";
    const string k_SubscribeMethodBody = "\n{0}.provider.{1} += {2};\n";
    const string k_UnubscribeMethodBody = "\n{0}.provider.{1} -= {2};\n";

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

        public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            node = base.VisitInterfaceDeclaration(node) as InterfaceDeclarationSyntax;

            var afterKeyword = node.Keyword.GetNextToken().Text;
            switch (afterKeyword)
            {
                case k_IFunctionalitySubscriber:
                    if (node.TypeParameterList == null)
                        break;

                    if (node.DescendantNodes().OfType<PropertyDeclarationSyntax>().Where(x => x.Identifier.ValueText == k_Provider).Any())
                        break; // provider property is already implemented

                    var propertyDeclaration = Template.Compile<PropertyDeclarationSyntax>(k_ProviderProperty);
                    node = node.WithMembers(node.Members.Add(propertyDeclaration));
                    wasModified = true;
                    break;
            }

            return node;
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
            if (firstParameter.Modifiers.ToString() == "this")
            {
                // TODO: share this code with the method for finding subscriber classes below
                var symbol = m_Model.GetDeclaredSymbol(firstParameter).Type;
                foreach (var @interface in symbol.AllInterfaces)
                {
                    // TODO: handle provider types with more than one level of inheritence
                    foreach (var baseInterface in @interface.Interfaces)
                    {
                        if (baseInterface.Name == k_IFunctionalitySubscriber)
                        {
                            if (methodName.StartsWith("SubscribeTo", StringComparison.Ordinal))
                            {
                                if (parameters.Count != 2)
                                {
                                    //TODO: Error for wrong number of parameters
                                    continue;
                                }

                                var eventName = methodName.Substring(11);
                                node = node.WithBody(SyntaxFactory.Block(
                                    SyntaxFactory.ParseStatement(
                                        string.Format(
                                            k_SubscribeMethodBody,
                                            firstParameterName,
                                            eventName,
                                            parameters[1].Identifier.Text
                                        )
                                    )
                                ));
                            }
                            else if (methodName.StartsWith("UnsubscribeFrom", StringComparison.Ordinal))
                            {
                                if (parameters.Count != 2)
                                {
                                    //TODO: Error for wrong number of parameters
                                    continue;
                                }

                                var eventName = methodName.Substring(15);
                                node = node.WithBody(SyntaxFactory.Block(
                                    SyntaxFactory.ParseStatement(
                                        string.Format(
                                            k_UnubscribeMethodBody,
                                            firstParameterName,
                                            eventName,
                                            parameters[1].Identifier.Text
                                        )
                                    )
                                ));
                            }
                            else if (methodName.StartsWith("GetProp", StringComparison.Ordinal))
                            {
                                if (parameters.Count != 1)
                                {
                                    //TODO: Error for wrong number of parameters
                                    continue;
                                }

                                var propName = methodName.Substring(7);
                                node = node.WithBody(SyntaxFactory.Block(
                                    SyntaxFactory.ParseStatement(
                                        string.Format(
                                            k_GetPropMethodBody,
                                            firstParameterName,
                                            propName
                                        )
                                    )
                                ));
                            }
                            else if (methodName.StartsWith("SetProp", StringComparison.Ordinal))
                            {
                                if (parameters.Count != 2)
                                {
                                    //TODO: Error for wrong number of parameters
                                    continue;
                                }

                                var propName = methodName.Substring(7);
                                node = node.WithBody(SyntaxFactory.Block(
                                    SyntaxFactory.ParseStatement(
                                        string.Format(
                                            k_SetPropMethodBody,
                                            firstParameterName,
                                            propName,
                                            parameters[1].Identifier.Text
                                        )
                                    )
                                ));
                            }
                            else
                            {
                                k_StringBuilder.Length = 0;
                                for (var i = 1; i < parameterCount; i++)
                                {
                                    var parameter = parameters[i];
                                    foreach (var modifier in parameter.Modifiers)
                                        k_StringBuilder.Append(modifier + " ");

                                    k_StringBuilder.Append(parameter.Identifier);
                                    k_StringBuilder.Append(", ");
                                }

                                if (k_StringBuilder.Length > 1)
                                    k_StringBuilder.Length -= 2;

                                var @return = string.Empty;
                                if (node.ReturnType.ToString() != "void")
                                    @return = "return ";

                                node = node.WithBody(SyntaxFactory.Block(
                                    SyntaxFactory.ParseStatement(
                                        string.Format(
                                            k_ExtensionMethodBody,
                                            @return,
                                            firstParameterName,
                                            methodName,
                                            k_StringBuilder
                                        )
                                    )
                                ));
                            }

                            wasModified = true;
                            break;
                        }
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
            //    messageCallback(AnalyzerMessageType.Info, "FI_Interfaces analyzer codegen output:\n" + newRoot, ast.FilePath, 0, 0);

            return newRoot;
        }
        catch (Exception e)
        {
            messageCallback(
                AnalyzerMessageType.Error,
                string.Format("Error in FI_Interfaces analyzer:\n{0}\n{1}", e.Message, e.StackTrace),
                ast.FilePath, 0, 0);
        }

        modified = false;
        return ast.GetRoot();
    }
}
