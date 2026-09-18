using System.Collections.Generic;

namespace Quick2Constructor
{
    [Command(PackageIds.MyCommand)]
    internal sealed class GotoConstructorCommand : BaseCommand<GotoConstructorCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            DocumentView view = ThreadHelper.JoinableTaskFactory.Run(VS.Documents.GetActiveDocumentViewAsync);
            bool isCSharp = IsCSharpDocument(view);
            Command.Visible = isCSharp;
            Command.Enabled = isCSharp;
            Command.Text = Strings.CommandTitle;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await Package.JoinableTaskFactory.SwitchToMainThreadAsync();

            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            if (!IsCSharpDocument(docView))
            {
                await VS.StatusBar.ShowMessageAsync(Strings.UseInCSharpFile);
                return;
            }

            ConstructorLookupResult result = await ConstructorLocator.FindAsync(docView);
            if (!result.IsSuccess)
            {
                await VS.StatusBar.ShowMessageAsync(result.ErrorMessage);
                return;
            }

            ConstructorLocation target = result.Constructors.Count == 1
                ? result.Constructors[0]
                : PickConstructor(result.Constructors);

            if (target == null)
            {
                return;
            }

            await ConstructorNavigator.NavigateAsync(target);
        }

        private static ConstructorLocation PickConstructor(IReadOnlyList<ConstructorLocation> constructors)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ConstructorPickWindow window = new ConstructorPickWindow(constructors);
            return window.ShowModal() == true ? window.Selected : null;
        }

        private static bool IsCSharpDocument(DocumentView view)
        {
            return view?.FilePath != null
                && view.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
        }
    }
}
