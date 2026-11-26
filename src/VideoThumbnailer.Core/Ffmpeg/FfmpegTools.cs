namespace VideoThumbnailer.Core.Ffmpeg;

public enum FfmpegOrigin { Custom, Managed, SystemPath }

public sealed record FfmpegTools(string FfmpegPath, string FfprobePath, FfmpegOrigin Origin);
