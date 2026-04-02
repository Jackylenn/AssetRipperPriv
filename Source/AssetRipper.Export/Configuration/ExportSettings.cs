using AssetRipper.Import.Logging;

namespace AssetRipper.Export.Configuration;

public sealed record class ExportSettings
{
	/// <summary>
	/// The file format that audio clips get exported in. Recommended: Ogg
	/// </summary>
	public AudioExportFormat AudioExportFormat { get; set; } = AudioExportFormat.Default;

	/// <summary>
	/// The file format that images (like textures) get exported in.
	/// </summary>
	public ImageExportFormat ImageExportFormat { get; set; } = ImageExportFormat.Png;

	/// <summary>
	/// The file format that images (like textures) get exported in.
	/// </summary>
	public LightmapTextureExportFormat LightmapTextureExportFormat { get; set; } = LightmapTextureExportFormat.Yaml;

	/// <summary>
	/// How are MonoScripts exported? Recommended: Decompiled
	/// </summary>
	public ScriptExportMode ScriptExportMode { get; set; } = ScriptExportMode.Hybrid;

	/// <summary>
	/// The C# language version of decompiled scripts.
	/// </summary>
	public ScriptLanguageVersion ScriptLanguageVersion { get; set; } = ScriptLanguageVersion.AutoSafe;

	/// <summary>
	/// If true, type references in scripts are fully qualified.
	/// </summary>
	public bool ScriptTypesFullyQualified { get; set; } = false;

	/// <summary>
	/// How to export shaders?
	/// </summary>
	public ShaderExportMode ShaderExportMode { get; set; } = ShaderExportMode.Dummy;

	/// <summary>
	/// Should sprites be exported as a texture? Recommended: Native
	/// </summary>
	public SpriteExportMode SpriteExportMode { get; set; } = SpriteExportMode.Yaml;

	/// <summary>
	/// How are text assets exported?
	/// </summary>
	public TextExportMode TextExportMode { get; set; } = TextExportMode.Parse;

	public bool ExportUnreadableAssets { get; set; } = false;

	/// <summary>
	/// Skip exporting audio clips.
	/// </summary>
	public bool SkipAudioClips { get; set; } = false;

	/// <summary>
	/// Skip exporting textures and images.
	/// </summary>
	public bool SkipTextures { get; set; } = false;

	/// <summary>
	/// Skip exporting meshes.
	/// </summary>
	public bool SkipMeshes { get; set; } = false;

	/// <summary>
	/// Skip exporting shaders.
	/// </summary>
	public bool SkipShaders { get; set; } = false;

	/// <summary>
	/// Skip exporting fonts.
	/// </summary>
	public bool SkipFonts { get; set; } = false;

	/// <summary>
	/// Skip exporting video clips.
	/// </summary>
	public bool SkipVideos { get; set; } = false;

	public bool SaveSettingsToDisk { get; set; }

	public string? LanguageCode { get; set; }

	public void Log()
	{
		Logger.Info(LogCategory.General, $"{nameof(AudioExportFormat)}: {AudioExportFormat}");
		Logger.Info(LogCategory.General, $"{nameof(ImageExportFormat)}: {ImageExportFormat}");
		Logger.Info(LogCategory.General, $"{nameof(LightmapTextureExportFormat)}: {LightmapTextureExportFormat}");
		Logger.Info(LogCategory.General, $"{nameof(ScriptExportMode)}: {ScriptExportMode}");
		Logger.Info(LogCategory.General, $"{nameof(ScriptLanguageVersion)}: {ScriptLanguageVersion}");
		Logger.Info(LogCategory.General, $"{nameof(ShaderExportMode)}: {ShaderExportMode}");
		Logger.Info(LogCategory.General, $"{nameof(SpriteExportMode)}: {SpriteExportMode}");
		Logger.Info(LogCategory.General, $"{nameof(TextExportMode)}: {TextExportMode}");
		Logger.Info(LogCategory.General, $"{nameof(ExportUnreadableAssets)}: {ExportUnreadableAssets}");
		Logger.Info(LogCategory.General, $"{nameof(SkipAudioClips)}: {SkipAudioClips}");
		Logger.Info(LogCategory.General, $"{nameof(SkipTextures)}: {SkipTextures}");
		Logger.Info(LogCategory.General, $"{nameof(SkipMeshes)}: {SkipMeshes}");
		Logger.Info(LogCategory.General, $"{nameof(SkipShaders)}: {SkipShaders}");
		Logger.Info(LogCategory.General, $"{nameof(SkipFonts)}: {SkipFonts}");
		Logger.Info(LogCategory.General, $"{nameof(SkipVideos)}: {SkipVideos}");
	}
}
