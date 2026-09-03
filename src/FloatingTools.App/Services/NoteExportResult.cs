namespace FloatingTools.App.Services;

public sealed record NoteExportResult(byte[] Content, bool HasMissingOrUnreadableImages);
