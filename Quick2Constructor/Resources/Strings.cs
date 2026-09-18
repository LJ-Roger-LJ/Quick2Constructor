using System.Globalization;
using System.Resources;

namespace Quick2Constructor
{
    internal static class Strings
    {
        private static readonly ResourceManager Manager =
            new ResourceManager("Quick2Constructor.Resources.Strings", typeof(Strings).Assembly);

        public static string CommandTitle => Get(nameof(CommandTitle));
        public static string NoActiveEditor => Get(nameof(NoActiveEditor));
        public static string CannotGetWorkspace => Get(nameof(CannotGetWorkspace));
        public static string FileNotInSolution => Get(nameof(FileNotInSolution));
        public static string CannotAnalyzeDocument => Get(nameof(CannotAnalyzeDocument));
        public static string CaretNotInType => Get(nameof(CaretNotInType));
        public static string TypeHasNoExplicitConstructor => Get(nameof(TypeHasNoExplicitConstructor));
        public static string UseInCSharpFile => Get(nameof(UseInCSharpFile));
        public static string CannotOpenFile => Get(nameof(CannotOpenFile));
        public static string LocationStale => Get(nameof(LocationStale));
        public static string FilterPlaceholder => Get(nameof(FilterPlaceholder));
        public static string PickerHint => Get(nameof(PickerHint));
        public static string NoMatchingConstructors => Get(nameof(NoMatchingConstructors));

        public static string Format(string format, params object[] args)
        {
            return string.Format(CultureInfo.CurrentUICulture, format, args);
        }

        private static string Get(string name)
        {
            return Manager.GetString(name, CultureInfo.CurrentUICulture)
                ?? Manager.GetString(name, CultureInfo.InvariantCulture)
                ?? name;
        }
    }
}
