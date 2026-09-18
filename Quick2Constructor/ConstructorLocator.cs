using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.LanguageServices;

namespace Quick2Constructor
{
    internal sealed class ConstructorLocation
    {
        public string Signature { get; set; }
        public string FilePath { get; set; }
        public int Line { get; set; }
        public int Character { get; set; }
        public int Length { get; set; }

        public string LocationText => $"{Path.GetFileName(FilePath)}:{Line + 1}";
    }

    internal sealed class ConstructorLookupResult
    {
        public string ErrorMessage { get; }
        public IReadOnlyList<ConstructorLocation> Constructors { get; }
        public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);

        private ConstructorLookupResult(string errorMessage, IReadOnlyList<ConstructorLocation> constructors)
        {
            ErrorMessage = errorMessage;
            Constructors = constructors ?? Array.Empty<ConstructorLocation>();
        }

        public static ConstructorLookupResult Fail(string message) =>
            new ConstructorLookupResult(message, null);

        public static ConstructorLookupResult Success(IReadOnlyList<ConstructorLocation> constructors) =>
            new ConstructorLookupResult(null, constructors);
    }

    internal static class ConstructorLocator
    {
        public static async System.Threading.Tasks.Task<ConstructorLookupResult> FindAsync(DocumentView docView)
        {
            if (docView?.TextView == null)
            {
                return ConstructorLookupResult.Fail(Strings.NoActiveEditor);
            }

            VisualStudioWorkspace workspace = await VS.GetMefServiceAsync<VisualStudioWorkspace>();
            if (workspace == null)
            {
                return ConstructorLookupResult.Fail(Strings.CannotGetWorkspace);
            }

            string filePath = docView.FilePath;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ConstructorLookupResult.Fail(Strings.FileNotInSolution);
            }

            DocumentId documentId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(filePath).FirstOrDefault()
                ?? workspace.CurrentSolution.GetDocumentIdsWithFilePath(Path.GetFullPath(filePath)).FirstOrDefault();

            if (documentId == null)
            {
                return ConstructorLookupResult.Fail(Strings.FileNotInSolution);
            }

            Document document = workspace.CurrentSolution.GetDocument(documentId);
            if (document == null)
            {
                return ConstructorLookupResult.Fail(Strings.FileNotInSolution);
            }

            SyntaxNode root = await document.GetSyntaxRootAsync();
            SemanticModel model = await document.GetSemanticModelAsync();
            if (root == null || model == null)
            {
                return ConstructorLookupResult.Fail(Strings.CannotAnalyzeDocument);
            }

            int position = docView.TextView.Caret.Position.BufferPosition.Position;
            if (position < 0)
            {
                position = 0;
            }
            else if (position > root.FullSpan.End)
            {
                position = root.FullSpan.End;
            }

            SyntaxNode node = root.FindToken(position).Parent;
            BaseTypeDeclarationSyntax typeDecl = node?.AncestorsAndSelf().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
            if (typeDecl == null || model.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol type)
            {
                return ConstructorLookupResult.Fail(Strings.CaretNotInType);
            }

            List<ConstructorLocation> locations = new List<ConstructorLocation>();
            foreach (IMethodSymbol ctor in type.InstanceConstructors.Where(c => !c.IsImplicitlyDeclared).Concat(type.StaticConstructors))
            {
                ConstructorLocation location = TryCreateLocation(ctor);
                if (location != null)
                {
                    locations.Add(location);
                }
            }

            if (locations.Count == 0)
            {
                return ConstructorLookupResult.Fail(Strings.Format(Strings.TypeHasNoExplicitConstructor, type.Name));
            }

            return ConstructorLookupResult.Success(locations);
        }

        private static ConstructorLocation TryCreateLocation(IMethodSymbol ctor)
        {
            SyntaxReference syntaxRef = ctor.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef == null)
            {
                return null;
            }

            SyntaxNode syntax = syntaxRef.GetSyntax();
            Location location = GetPreferredLocation(syntax);
            FileLinePositionSpan lineSpan = location.GetLineSpan();
            if (!lineSpan.IsValid || string.IsNullOrEmpty(lineSpan.Path))
            {
                return null;
            }

            return new ConstructorLocation
            {
                Signature = FormatSignature(ctor),
                FilePath = lineSpan.Path,
                Line = lineSpan.StartLinePosition.Line,
                Character = lineSpan.StartLinePosition.Character,
                Length = Math.Max(1, location.SourceSpan.Length)
            };
        }

        private static Location GetPreferredLocation(SyntaxNode syntax)
        {
            if (syntax is ConstructorDeclarationSyntax ctorDecl)
            {
                return ctorDecl.Identifier.GetLocation();
            }

            if (syntax is TypeDeclarationSyntax typeDecl && typeDecl.ParameterList != null)
            {
                return typeDecl.ParameterList.GetLocation();
            }

            return syntax.GetLocation();
        }

        private static string FormatSignature(IMethodSymbol ctor)
        {
            string parameters = string.Join(", ", ctor.Parameters.Select(FormatParameter));
            string signature = $"{ctor.ContainingType.Name}({parameters})";
            return ctor.IsStatic ? "static " + signature : signature;
        }

        private static string FormatParameter(IParameterSymbol parameter)
        {
            string typeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            string prefix = parameter.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                _ => parameter.IsParams ? "params " : string.Empty
            };

            return $"{prefix}{typeName} {parameter.Name}";
        }
    }
}
