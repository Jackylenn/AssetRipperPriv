using AssetRipper.GUI.Web.Paths;
using AssetRipper.Import;

namespace AssetRipper.GUI.Web.Pages;

public sealed class IndexPage : DefaultPage
{
	public static IndexPage Instance { get; } = new();

	public override string? GetTitle() => Localization.AssetRipperPremium;

	public override void WriteInnerContent(TextWriter writer)
	{
		using (new Div(writer).WithClass("text-center container mt-5").End())
		{
			new H1(writer).WithClass("display-4 mb-4").Close(Localization.Welcome);
			if (GameFileLoader.IsLoaded)
			{
				PathLinking.WriteLink(writer, GameFileLoader.GameBundle, Localization.ViewLoadedFiles, "btn btn-success");
			}
			else
			{
				new Button(writer).WithType("button").WithClass("btn btn-secondary").WithDisabled().Close(Localization.NoFilesLoaded);
			}
			new P(writer).WithClass("mt-4").Close(Localization.AppreciationMessage);

			// Version info
			string? version = AssetRipperRuntimeInformation.Build.Version;
			if (version is not null)
			{
				new P(writer).WithClass("text-muted mt-2").Close($"v{version} ({AssetRipperRuntimeInformation.Build.Configuration})");
			}
		}
	}
}
